using EffectsLatencyTester;
using System.Runtime.InteropServices;
using EffectsLatencyTester.Core;
using NAudio.Wave;
using NAudio.Wave.Asio;

namespace EffectsLatencyTester.Audio.Windows;

public sealed class AsioAudioBackend : IAudioBackend
{
    private static readonly int[] SampleRateCandidates =
    [
        8000, 11025, 16000, 22050, 32000, 44100, 48000,
        88200, 96000, 176400, 192000, 352800, 384000, 768000,
    ];

    private static readonly int[] CommonBufferSizes =
        [16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768];

    public string Name => "ASIO";

    public IReadOnlyList<AudioDeviceInfo> EnumerateDevices()
    {
        var result = new List<AudioDeviceInfo>();
        string[] driverNames;
        try
        {
            driverNames = AsioOut.GetDriverNames();
        }
        catch
        {
            return result;
        }

        foreach (var driverName in driverNames)
        {
            try
            {
                var capabilities = Inspect(driverName);
                result.Add(new AudioDeviceInfo(
                    driverName,
                    driverName,
                    Name,
                    true,
                    I18n.DriverReady,
                    capabilities));
            }
            catch (Exception exception)
            {
                result.Add(new AudioDeviceInfo(
                    driverName,
                    driverName,
                    Name,
                    false,
                    exception.Message,
                    null));
            }
        }

        return result;
    }

    public IAudioDuplexStream OpenStream(AudioStreamOptions options)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("ASIO is only available on Windows.");
        }

        return new AsioDuplexStream(options);
    }

    private static AudioDeviceCapabilities Inspect(string driverName)
    {
        AsioDriver? basicDriver = null;
        AsioDriverExt? driver = null;
        try
        {
            basicDriver = AsioDriver.GetAsioDriverByName(driverName);
            driver = new AsioDriverExt(basicDriver);
            var capabilities = driver.Capabilities;
            var currentSampleRate = NormalizeSampleRate(capabilities.SampleRate);
            var sampleRates = SampleRateCandidates
                .Where(rate => driver.IsSampleRateSupported(rate))
                .ToHashSet();
            if (currentSampleRate > 0)
            {
                sampleRates.Add(currentSampleRate);
            }

            var inputChannels = capabilities.InputChannelInfos
                .Select((channel, index) => new AudioChannelInfo(
                    index,
                    string.IsNullOrWhiteSpace(channel.name) ? $"Input {index + 1}" : channel.name.Trim()))
                .ToArray();
            var outputChannels = capabilities.OutputChannelInfos
                .Select((channel, index) => new AudioChannelInfo(
                    index,
                    string.IsNullOrWhiteSpace(channel.name) ? $"Output {index + 1}" : channel.name.Trim()))
                .ToArray();

            return new AudioDeviceCapabilities(
                driverName,
                currentSampleRate,
                capabilities.BufferPreferredSize,
                capabilities.BufferMinSize,
                capabilities.BufferMaxSize,
                capabilities.BufferGranularity,
                sampleRates.OrderBy(rate => rate).ToArray(),
                BuildBufferSizes(
                    capabilities.BufferMinSize,
                    capabilities.BufferMaxSize,
                    capabilities.BufferPreferredSize,
                    capabilities.BufferGranularity),
                inputChannels,
                outputChannels);
        }
        finally
        {
            if (driver is not null)
            {
                driver.ReleaseDriver();
            }
            else
            {
                basicDriver?.ReleaseComAsioDriver();
            }
        }
    }

    private static int NormalizeSampleRate(double sampleRate)
    {
        return sampleRate <= 0 || sampleRate > int.MaxValue ? 0 : (int)Math.Round(sampleRate);
    }

    private static IReadOnlyList<int> BuildBufferSizes(int min, int max, int preferred, int granularity)
    {
        if (min <= 0 || max < min)
        {
            return preferred > 0 ? [preferred] : [];
        }

        var result = new SortedSet<int>();
        if (granularity == -1)
        {
            for (var size = 1; size <= max; size *= 2)
            {
                if (size >= min)
                {
                    result.Add(size);
                }

                if (size > max / 2)
                {
                    break;
                }
            }
        }
        else if (granularity > 0)
        {
            for (var size = min; size <= max; size += granularity)
            {
                result.Add(size);
                if (size > max - granularity)
                {
                    break;
                }
            }
        }
        else
        {
            foreach (var size in CommonBufferSizes)
            {
                if (size >= min && size <= max)
                {
                    result.Add(size);
                }
            }
        }

        result.Add(Math.Clamp(preferred, min, max));
        result.Add(min);
        result.Add(max);
        return result.ToArray();
    }

    private sealed class AsioDuplexStream : IAudioDuplexStream
    {
        private readonly AsioDriver? basicDriver;
        private readonly AsioDriverExt asio;
        private readonly int bufferSize;
        private readonly AsioSampleType inputSampleType;
        private readonly AsioSampleType outputSampleType;
        private readonly float[] inputSamples;
        private readonly float[] outputSamples;
        private readonly byte[] waveBuffer;
        private readonly float[] floatSamples;
        private readonly int[] intSamples;
        private readonly short[] shortSamples;
        private readonly byte[] int24Samples;
        private AudioProcessCallback? callback;
        private Exception? callbackError;
        private bool started;
        private bool disposed;

        public AsioDuplexStream(AudioStreamOptions options)
        {
            try
            {
                basicDriver = AsioDriver.GetAsioDriverByName(options.DeviceId);
                asio = new AsioDriverExt(basicDriver);
                if (!asio.IsSampleRateSupported(options.SampleRate))
                {
                    throw new AudioBackendException($"Sample rate {options.SampleRate} is not supported.");
                }

                var capabilities = asio.Capabilities;
                if (options.InputChannel < 0 || options.InputChannel >= capabilities.InputChannelInfos.Length ||
                    options.OutputChannel < 0 || options.OutputChannel >= capabilities.OutputChannelInfos.Length)
                {
                    throw new AudioBackendException("The selected audio channel is not available.");
                }

                if (options.BufferSize < capabilities.BufferMinSize || options.BufferSize > capabilities.BufferMaxSize)
                {
                    throw new AudioBackendException("The selected buffer size is not supported.");
                }

                if (capabilities.SampleRate != options.SampleRate)
                {
                    asio.SetSampleRate(options.SampleRate);
                    capabilities = asio.Capabilities;
                }

                capabilities.BufferPreferredSize = options.BufferSize;
                bufferSize = options.BufferSize;
                inputSampleType = capabilities.InputChannelInfos[options.InputChannel].type;
                outputSampleType = capabilities.OutputChannelInfos[options.OutputChannel].type;
                inputSamples = new float[bufferSize];
                outputSamples = new float[bufferSize];
                waveBuffer = new byte[bufferSize * sizeof(float)];
                floatSamples = new float[bufferSize];
                intSamples = new int[bufferSize];
                shortSamples = new short[bufferSize];
                int24Samples = new byte[bufferSize * 3];

                asio.FillBufferCallback = OnBuffer;
                var actualBufferSize = asio.CreateBuffers(1, 1, useMaxBufferSize: false);
                if (actualBufferSize != bufferSize)
                {
                    throw new AudioBackendException($"ASIO selected buffer size {actualBufferSize} instead of {bufferSize}.");
                }

                asio.SetChannelOffset(options.OutputChannel, options.InputChannel);
                SampleRate = options.SampleRate;
            }
            catch
            {
                try
                {
                    if (asio is not null)
                    {
                        asio.ReleaseDriver();
                    }
                    else
                    {
                        basicDriver?.ReleaseComAsioDriver();
                    }
                }
                catch
                {
                }

                throw;
            }
        }

        public int SampleRate { get; }
        public int BufferSize => bufferSize;

        public void Start(AudioProcessCallback processCallback)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            callback = processCallback ?? throw new ArgumentNullException(nameof(processCallback));
            callbackError = null;
            asio.Start();
            started = true;
        }

        public void Stop()
        {
            if (!started)
            {
                return;
            }

            asio.Stop();
            started = false;
            if (callbackError is not null)
            {
                throw new AudioBackendException("ASIO callback failed.", callbackError);
            }
        }

        private void OnBuffer(IntPtr[] inputBuffers, IntPtr[] outputBuffers)
        {
            try
            {
                Array.Clear(inputSamples);
                Array.Clear(outputSamples);
                if (inputBuffers.Length > 0)
                {
                    var audio = new AsioAudioAvailableEventArgs(
                        inputBuffers,
                        outputBuffers,
                        bufferSize,
                        inputSampleType);
                    audio.GetAsInterleavedSamples(inputSamples);
                }

                callback?.Invoke(inputSamples, outputSamples, bufferSize);
                WriteOutput(outputBuffers);
            }
            catch (Exception exception)
            {
                Interlocked.CompareExchange(ref callbackError, exception, null);
                ClearOutput(outputBuffers);
            }
        }

        private void WriteOutput(IntPtr[] outputBuffers)
        {
            if (outputBuffers.Length == 0)
            {
                return;
            }

            Array.Copy(outputSamples, floatSamples, bufferSize);
            switch (outputSampleType)
            {
                case AsioSampleType.Float32LSB:
                    Marshal.Copy(floatSamples, 0, outputBuffers[0], bufferSize);
                    break;
                case AsioSampleType.Int32LSB:
                    for (var index = 0; index < bufferSize; index++)
                    {
                        intSamples[index] = ClampToInt(floatSamples[index]);
                    }

                    Marshal.Copy(intSamples, 0, outputBuffers[0], bufferSize);
                    break;
                case AsioSampleType.Int16LSB:
                    for (var index = 0; index < bufferSize; index++)
                    {
                        shortSamples[index] = ClampToShort(floatSamples[index]);
                    }

                    Marshal.Copy(shortSamples, 0, outputBuffers[0], bufferSize);
                    break;
                case AsioSampleType.Int24LSB:
                    for (var index = 0; index < bufferSize; index++)
                    {
                        var sample = ClampTo24Bit(floatSamples[index]);
                        var offset = index * 3;
                        int24Samples[offset] = (byte)sample;
                        int24Samples[offset + 1] = (byte)(sample >> 8);
                        int24Samples[offset + 2] = (byte)(sample >> 16);
                    }

                    Marshal.Copy(int24Samples, 0, outputBuffers[0], int24Samples.Length);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported ASIO output format: {outputSampleType}");
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
            Array.Clear(waveBuffer);
            Marshal.Copy(waveBuffer, 0, outputBuffers[0], bufferSize * bytesPerSample);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                if (started)
                {
                    asio.Stop();
                }
            }
            catch
            {
            }
            finally
            {
                asio.FillBufferCallback = null;
                asio.ReleaseDriver();
                callback = null;
            }
        }

        private static int ClampToInt(float sample) =>
            (int)(Math.Clamp(float.IsFinite(sample) ? sample : 0f, -1f, 1f) * 2147483647.0);

        private static short ClampToShort(float sample) =>
            (short)(Math.Clamp(float.IsFinite(sample) ? sample : 0f, -1f, 1f) * 32767.0);

        private static int ClampTo24Bit(float sample) =>
            (int)(Math.Clamp(float.IsFinite(sample) ? sample : 0f, -1f, 1f) * 8388607.0);
    }
}