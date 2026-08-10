using System.Buffers.Binary;
using System.Runtime.InteropServices;
using NAudio.Wave;
using NAudio.Wave.Asio;

namespace LatencyTester;

internal readonly record struct LatencyResult(
    bool HasResult,
    int LatencySamples,
    double LatencyMilliseconds,
    int SampleRate,
    float[] OutputSamples,
    float[] InputSamples);

internal static class LatencyMeasurement
{
    private const int TestDurationMilliseconds = 3200;
    private const int PulseOffsetMilliseconds = 500;
    private const int PulseSpacingMilliseconds = 1000;
    private const float DetectionThreshold = 0.12f;
    private const int DetectionRefractorySamples = 1000;

    public static async Task<LatencyResult> RunAsync(
        string driverName,
        int sampleRate,
        int bufferSize,
        int outputChannel,
        int inputChannel)
    {
        if (!AsioOut.GetDriverNames().Contains(driverName))
        {
            throw new InvalidOperationException(I18n.AsioDriverGone);
        }

        AsioDriver? basicDriver = null;
        AsioDriverExt? asio = null;
        var started = false;

        try
        {
            basicDriver = AsioDriver.GetAsioDriverByName(driverName);
            asio = new AsioDriverExt(basicDriver);
            if (!asio.IsSampleRateSupported(sampleRate))
            {
                throw new InvalidOperationException(I18n.Format(nameof(I18n.UnsupportedSampleRate), sampleRate));
            }

            var capabilities = asio.Capabilities;
            if (bufferSize < capabilities.BufferMinSize || bufferSize > capabilities.BufferMaxSize)
            {
                throw new InvalidOperationException(I18n.Format(nameof(I18n.BufferUnsupported),
                    bufferSize, capabilities.BufferMinSize, capabilities.BufferMaxSize));
            }

            if (capabilities.SampleRate != sampleRate)
            {
                asio.SetSampleRate(sampleRate);
                capabilities = asio.Capabilities;
            }

            // AsioDriverExt exposes the preferred size as a mutable capability.
            // Replacing it before CreateBuffers lets us request a specific valid size
            // while keeping NAudio's ASIO callback and channel mapping code.
            capabilities.BufferPreferredSize = bufferSize;

            var provider = new PulseWaveProvider(
                sampleRate,
                sampleRate * TestDurationMilliseconds / 1000);
            const int inputChannels = 1;
            var capture = new CaptureBuffer(sampleRate * TestDurationMilliseconds / 1000, bufferSize);
            var callback = new AsioBufferCallback(
                provider,
                capture,
                bufferSize,
                capabilities.InputChannelInfos[inputChannel].type,
                capabilities.OutputChannelInfos[outputChannel].type);

            asio.FillBufferCallback = callback.OnBuffer;
            var actualBufferSize = asio.CreateBuffers(1, inputChannels, useMaxBufferSize: false);
            if (actualBufferSize != bufferSize)
            {
                throw new InvalidOperationException(I18n.Format(nameof(I18n.BufferActualMismatch),
                    actualBufferSize, bufferSize));
            }

            asio.SetChannelOffset(outputChannel, inputChannel);

            // Arm the timeline immediately before starting the ASIO stream.
            provider.Start();
            asio.Start();
            started = true;
            await Task.Delay(TestDurationMilliseconds).ConfigureAwait(true);
            asio.Stop();
            started = false;

            if (callback.CallbackError is not null)
            {
                throw new InvalidOperationException(I18n.AudioCallbackFailed, callback.CallbackError);
            }

            return capture.FindLatency(provider.PulsePositions, sampleRate) with
            {
                OutputSamples = provider.GetSamples(),
            };
        }
        finally
        {
            if (asio is not null)
            {
                if (started)
                {
                    try
                    {
                        asio.Stop();
                    }
                    catch
                    {
                        // The driver may already have stopped after a callback failure.
                    }
                }

                asio.FillBufferCallback = null;
                asio.ReleaseDriver();
            }
            else
            {
                basicDriver?.ReleaseComAsioDriver();
            }
        }
    }

    private sealed class AsioBufferCallback
    {
        private readonly PulseWaveProvider provider;
        private readonly CaptureBuffer capture;
        private readonly int bufferSize;
        private readonly AsioSampleType inputSampleType;
        private readonly AsioSampleType outputSampleType;
        private readonly byte[] waveBuffer;
        private readonly float[] floatSamples;
        private readonly int[] intSamples;
        private readonly short[] shortSamples;
        private readonly byte[] int24Samples;
        private Exception? callbackError;

        public AsioBufferCallback(
            PulseWaveProvider provider,
            CaptureBuffer capture,
            int bufferSize,
            AsioSampleType inputSampleType,
            AsioSampleType outputSampleType)
        {
            this.provider = provider;
            this.capture = capture;
            this.bufferSize = bufferSize;
            this.inputSampleType = inputSampleType;
            this.outputSampleType = outputSampleType;
            waveBuffer = new byte[bufferSize * sizeof(float)];
            floatSamples = new float[bufferSize];
            intSamples = new int[bufferSize];
            shortSamples = new short[bufferSize];
            int24Samples = new byte[bufferSize * 3];
        }

        public Exception? CallbackError => Volatile.Read(ref callbackError);

        public void OnBuffer(IntPtr[] inputBuffers, IntPtr[] outputBuffers)
        {
            try
            {
                var audio = new AsioAudioAvailableEventArgs(
                    inputBuffers,
                    outputBuffers,
                    bufferSize,
                    inputSampleType);
                capture.OnAudioAvailable(this, audio);

                var read = provider.Read(waveBuffer, 0, waveBuffer.Length);
                if (read < waveBuffer.Length)
                {
                    Array.Clear(waveBuffer, read, waveBuffer.Length - read);
                }

                Buffer.BlockCopy(waveBuffer, 0, floatSamples, 0, waveBuffer.Length);
                WriteOutput(outputBuffers);
            }
            catch (Exception ex)
            {
                Interlocked.CompareExchange(ref callbackError, ex, null);
                ClearOutput(outputBuffers);
            }
        }

        private void WriteOutput(IntPtr[] outputBuffers)
        {
            if (outputBuffers.Length == 0)
            {
                return;
            }

            switch (outputSampleType)
            {
                case AsioSampleType.Float32LSB:
                    Marshal.Copy(floatSamples, 0, outputBuffers[0], bufferSize);
                    break;
                case AsioSampleType.Int32LSB:
                    for (var i = 0; i < bufferSize; i++)
                    {
                        intSamples[i] = ClampToInt(floatSamples[i]);
                    }

                    Marshal.Copy(intSamples, 0, outputBuffers[0], bufferSize);
                    break;
                case AsioSampleType.Int16LSB:
                    for (var i = 0; i < bufferSize; i++)
                    {
                        shortSamples[i] = ClampToShort(floatSamples[i]);
                    }

                    Marshal.Copy(shortSamples, 0, outputBuffers[0], bufferSize);
                    break;
                case AsioSampleType.Int24LSB:
                    for (var i = 0; i < bufferSize; i++)
                    {
                        var sample = ClampTo24Bit(floatSamples[i]);
                        var offset = i * 3;
                        int24Samples[offset] = (byte)sample;
                        int24Samples[offset + 1] = (byte)(sample >> 8);
                        int24Samples[offset + 2] = (byte)(sample >> 16);
                    }

                    Marshal.Copy(int24Samples, 0, outputBuffers[0], int24Samples.Length);
                    break;
                default:
                    throw new NotSupportedException(I18n.Format(nameof(I18n.UnsupportedOutputFormat), outputSampleType));
            }
        }

        private void ClearOutput(IntPtr[] outputBuffers)
        {
            if (outputBuffers.Length == 0)
            {
                return;
            }

            var bytesPerSample = outputSampleType switch
            {
                AsioSampleType.Int16LSB => 2,
                AsioSampleType.Int24LSB => 3,
                _ => 4,
            };
            Marshal.Copy(new byte[bufferSize * bytesPerSample], 0, outputBuffers[0], bufferSize * bytesPerSample);
        }

        private static int ClampToInt(float sample)
        {
            var value = Math.Clamp(double.IsNaN(sample) ? 0 : sample, -1.0, 1.0);
            return (int)(value * 2147483647.0);
        }

        private static short ClampToShort(float sample)
        {
            var value = Math.Clamp(double.IsNaN(sample) ? 0 : sample, -1.0, 1.0);
            return (short)(value * 32767.0);
        }

        private static int ClampTo24Bit(float sample)
        {
            var value = Math.Clamp(double.IsNaN(sample) ? 0 : sample, -1.0, 1.0);
            return (int)(value * 8388607.0);
        }
    }

    private sealed class PulseWaveProvider : IWaveProvider
    {
        private long samplePosition;
        private int started;

        private readonly float[] outputSamples;

        public PulseWaveProvider(int sampleRate, int capacitySamples)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
            outputSamples = new float[Math.Max(capacitySamples, 1)];
            PulsePositions =
            [
                sampleRate * PulseOffsetMilliseconds / 1000,
                sampleRate * (PulseOffsetMilliseconds + PulseSpacingMilliseconds) / 1000,
                sampleRate * (PulseOffsetMilliseconds + 2 * PulseSpacingMilliseconds) / 1000,
            ];
        }

        public WaveFormat WaveFormat { get; }

        public int[] PulsePositions { get; }

        public float[] GetSamples()
        {
            var length = Math.Min((int)Math.Max(samplePosition, 0), outputSamples.Length);
            return outputSamples.AsSpan(0, length).ToArray();
        }

        public void Start()
        {
            samplePosition = 0;
            Volatile.Write(ref started, 1);
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            if (Volatile.Read(ref started) == 0)
            {
                return count;
            }

            var sampleCount = count / sizeof(float);
            for (var i = 0; i < sampleCount; i++)
            {
                var position = samplePosition + i;
                var value = position == PulsePositions[0] ||
                            position == PulsePositions[1] ||
                            position == PulsePositions[2]
                    ? 0.8f
                    : 0.0f;
                if (position >= 0 && position < outputSamples.Length)
                {
                    outputSamples[(int)position] = value;
                }

                if (value != 0)
                {
                    var byteOffset = offset + i * sizeof(float);
                    BinaryPrimitives.WriteInt32LittleEndian(
                        buffer.AsSpan(byteOffset, sizeof(float)), BitConverter.SingleToInt32Bits(value));
                }
            }

            samplePosition += sampleCount;
            return count;
        }
    }

    private sealed class CaptureBuffer
    {
        private readonly float[] samples;
        private readonly float[] callbackBuffer;
        private int writtenSamples;
        private Exception? callbackError;

        public CaptureBuffer(int capacitySamples, int callbackSamples)
        {
            samples = new float[capacitySamples];
            callbackBuffer = new float[Math.Max(callbackSamples, 1)];
        }

        public void OnAudioAvailable(object? sender, AsioAudioAvailableEventArgs e)
        {
            try
            {
                var sampleCount = e.SamplesPerBuffer * e.InputBuffers.Length;
                if (sampleCount > callbackBuffer.Length)
                {
                    callbackError = new InvalidOperationException(I18n.InputBufferChanged);
                    return;
                }

                e.GetAsInterleavedSamples(callbackBuffer);
                var destination = Volatile.Read(ref writtenSamples);
                var copyCount = Math.Min(sampleCount, samples.Length - destination);
                if (copyCount > 0)
                {
                    Array.Copy(callbackBuffer, 0, samples, destination, copyCount);
                    Volatile.Write(ref writtenSamples, destination + copyCount);
                }
            }
            catch (Exception ex)
            {
                callbackError = ex;
            }
        }

        public LatencyResult FindLatency(IReadOnlyList<int> expectedPulses, int sampleRate)
        {
            if (callbackError is not null)
            {
                throw new InvalidOperationException(I18n.AudioCallbackFailed, callbackError);
            }

            var length = Volatile.Read(ref writtenSamples);
            var detections = new List<int>();
            var nextAllowed = 0;
            for (var i = 1; i < length; i++)
            {
                if (i < nextAllowed || Math.Abs(samples[i]) < DetectionThreshold || samples[i] < samples[i - 1])
                {
                    continue;
                }

                detections.Add(i);
                nextAllowed = i + DetectionRefractorySamples;
            }

            var latencies = new List<int>();
            foreach (var detection in detections)
            {
                var expected = expectedPulses
                    .OrderBy(pulse => Math.Abs(detection - pulse))
                    .First();
                var latency = detection - expected;
                if (latency >= 0 && latency < sampleRate / 2)
                {
                    latencies.Add(latency);
                }
            }

            if (latencies.Count == 0)
            {
                return new LatencyResult(false, 0, 0, sampleRate, Array.Empty<float>(), GetSamples());
            }

            latencies.Sort();
            var median = latencies[latencies.Count / 2];
            return new LatencyResult(
                true,
                median,
                median * 1000.0 / sampleRate,
                sampleRate,
                Array.Empty<float>(),
                GetSamples());
        }

        private float[] GetSamples()
        {
            var length = Math.Min(Volatile.Read(ref writtenSamples), samples.Length);
            return samples.AsSpan(0, Math.Max(length, 0)).ToArray();
        }
    }
}
