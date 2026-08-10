using System.Globalization;
using System.Runtime.InteropServices;
using EffectsLatencyTester.Core;
using EffectsLatencyTester;
using PortAudioSharp;
using PaStream = PortAudioSharp.Stream;
using Pa = PortAudioSharp.PortAudio;

namespace EffectsLatencyTester.Audio.PortAudio;

/// <summary>
/// PortAudio adapter used by the non-Windows targets. PortAudio selects the
/// native host API (Core Audio on macOS, ALSA/PipeWire/JACK on Linux).
/// </summary>
public sealed class PortAudioBackend : IAudioBackend
{
    private static readonly int[] SampleRateCandidates =
    [
        8000, 11025, 16000, 22050, 32000, 44100, 48000,
        88200, 96000, 176400, 192000, 352800, 384000,
    ];

    private static readonly int[] CommonBufferSizes =
        [16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192];

    private static readonly object InitializationLock = new();
    private static bool initialized;

    public PortAudioBackend(string hostApiDescription)
    {
        Name = $"PortAudio ({hostApiDescription})";
        EnsureInitialized();
    }

    public string Name { get; }

    public IReadOnlyList<AudioDeviceInfo> EnumerateDevices()
    {
        EnsureInitialized();
        var devices = new List<AudioDeviceInfo>();
        var count = Pa.DeviceCount;
        for (var index = 0; index < count; index++)
        {
            try
            {
                var info = Pa.GetDeviceInfo(index);
                var capabilities = Inspect(index, info);
                var isDuplex = info.maxInputChannels > 0 && info.maxOutputChannels > 0;
                var isAvailable = isDuplex && capabilities.SupportedSampleRates.Count > 0;
                var status = isAvailable
                    ? I18n.DriverReady
                    : isDuplex
                        ? I18n.NoCompatibleSampleRate
                        : I18n.NoDuplexAudioDevice;

                devices.Add(new AudioDeviceInfo(
                    index.ToString(CultureInfo.InvariantCulture),
                    string.IsNullOrWhiteSpace(info.name) ? $"PortAudio device {index}" : info.name,
                    Name,
                    isAvailable,
                    status,
                    capabilities));
            }
            catch (Exception exception)
            {
                devices.Add(new AudioDeviceInfo(
                    index.ToString(CultureInfo.InvariantCulture),
                    $"PortAudio device {index}",
                    Name,
                    false,
                    exception.Message,
                    null));
            }
        }

        return devices;
    }

    public IAudioDuplexStream OpenStream(AudioStreamOptions options)
    {
        EnsureInitialized();
        if (!int.TryParse(options.DeviceId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var deviceIndex))
        {
            throw new AudioBackendException($"Invalid PortAudio device id: {options.DeviceId}");
        }

        return new PortAudioDuplexStream(deviceIndex, options);
    }

    private static void EnsureInitialized()
    {
        lock (InitializationLock)
        {
            if (initialized)
            {
                return;
            }

            try
            {
                Pa.Initialize();
                initialized = true;
            }
            catch (DllNotFoundException exception)
            {
                throw new AudioBackendException(
                    "PortAudio native library was not found for this runtime identifier.", exception);
            }
            catch (PortAudioException exception)
            {
                throw new AudioBackendException(
                    $"PortAudio initialization failed: {exception.Message}", exception);
            }
        }
    }

    private static AudioDeviceCapabilities Inspect(int deviceIndex, DeviceInfo info)
    {
        var defaultSampleRate = NormalizeSampleRate(info.defaultSampleRate);
        if (defaultSampleRate <= 0)
        {
            defaultSampleRate = 48000;
        }

        var preferredBufferSize = EstimatePreferredBufferSize(info.defaultLowInputLatency, defaultSampleRate);
        var sampleRates = new SortedSet<int>();
        foreach (var rate in SampleRateCandidates)
        {
            if (info.maxInputChannels > 0 && info.maxOutputChannels > 0 &&
                CanOpen(deviceIndex, info, rate, Pa.FramesPerBufferUnspecified))
            {
                sampleRates.Add(rate);
            }
        }

        sampleRates.Add(defaultSampleRate);
        var bufferSizes = new SortedSet<int>();
        foreach (var size in CommonBufferSizes)
        {
            if (info.maxInputChannels > 0 && info.maxOutputChannels > 0 &&
                CanOpen(deviceIndex, info, defaultSampleRate, (uint)size))
            {
                bufferSizes.Add(size);
            }
        }

        bufferSizes.Add(preferredBufferSize);
        var inputChannels = Enumerable.Range(0, Math.Max(info.maxInputChannels, 0))
            .Select(index => new AudioChannelInfo(index, I18n.Format(nameof(I18n.InputFallback), index + 1)))
            .ToArray();
        var outputChannels = Enumerable.Range(0, Math.Max(info.maxOutputChannels, 0))
            .Select(index => new AudioChannelInfo(index, I18n.Format(nameof(I18n.OutputFallback), index + 1)))
            .ToArray();

        return new AudioDeviceCapabilities(
            string.IsNullOrWhiteSpace(info.name) ? $"PortAudio device {deviceIndex}" : info.name,
            defaultSampleRate,
            preferredBufferSize,
            bufferSizes.Count == 0 ? CommonBufferSizes[0] : bufferSizes.Min,
            bufferSizes.Count == 0 ? CommonBufferSizes[^1] : bufferSizes.Max,
            0,
            sampleRates.ToArray(),
            bufferSizes.ToArray(),
            inputChannels,
            outputChannels);
    }

    private static bool CanOpen(int deviceIndex, DeviceInfo info, int sampleRate, uint framesPerBuffer)
    {
        if (info.maxInputChannels < 1 || info.maxOutputChannels < 1)
        {
            return false;
        }

        var inputParameters = CreateParameters(deviceIndex, 1, info.defaultLowInputLatency);
        var outputParameters = CreateParameters(deviceIndex, 1, info.defaultLowOutputLatency);
        PaStream.Callback callback = ProbeCallback;
        try
        {
            using var stream = new PaStream(
                inParams: inputParameters,
                outParams: outputParameters,
                sampleRate: sampleRate,
                framesPerBuffer: framesPerBuffer,
                streamFlags: StreamFlags.ClipOff,
                callback: callback,
                userData: IntPtr.Zero);
            return true;
        }
        catch (PortAudioException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static StreamCallbackResult ProbeCallback(
        IntPtr input,
        IntPtr output,
        uint frameCount,
        ref StreamCallbackTimeInfo timeInfo,
        StreamCallbackFlags statusFlags,
        IntPtr userData) => StreamCallbackResult.Continue;

    private static StreamParameters CreateParameters(int device, int channelCount, double suggestedLatency) =>
        new()
        {
            device = device,
            channelCount = channelCount,
            sampleFormat = SampleFormat.Float32,
            suggestedLatency = double.IsFinite(suggestedLatency) && suggestedLatency >= 0
                ? suggestedLatency
                : 0,
            hostApiSpecificStreamInfo = IntPtr.Zero,
        };

    private static int NormalizeSampleRate(double sampleRate) =>
        sampleRate > 0 && sampleRate <= int.MaxValue
            ? (int)Math.Round(sampleRate)
            : 0;

    private static int EstimatePreferredBufferSize(double latencySeconds, int sampleRate)
    {
        var estimatedFrames = double.IsFinite(latencySeconds) && latencySeconds > 0
            ? (int)Math.Round(latencySeconds * sampleRate)
            : 256;
        var nearest = CommonBufferSizes
            .OrderBy(size => Math.Abs(size - estimatedFrames))
            .First();
        return nearest;
    }

    private sealed class PortAudioDuplexStream : IAudioDuplexStream
    {
        private readonly PaStream stream;
        private readonly int inputChannel;
        private readonly int outputChannel;
        private readonly int inputChannelCount;
        private readonly int outputChannelCount;
        private readonly float[] inputInterleaved;
        private readonly float[] outputInterleaved;
        private readonly float[] inputSamples;
        private readonly float[] outputSamples;
        private readonly PaStream.Callback callback;
        private AudioProcessCallback? processCallback;
        private Exception? callbackError;
        private bool started;
        private bool disposed;

        public PortAudioDuplexStream(int deviceIndex, AudioStreamOptions options)
        {
            if (options.SampleRate <= 0 || options.BufferSize <= 0)
            {
                throw new AudioBackendException("Sample rate and buffer size must be positive.");
            }

            var info = Pa.GetDeviceInfo(deviceIndex);
            if (options.InputChannel < 0 || options.InputChannel >= info.maxInputChannels ||
                options.OutputChannel < 0 || options.OutputChannel >= info.maxOutputChannels)
            {
                throw new AudioBackendException("The selected audio channel is not available.");
            }

            inputChannel = options.InputChannel;
            outputChannel = options.OutputChannel;
            inputChannelCount = inputChannel + 1;
            outputChannelCount = outputChannel + 1;
            inputInterleaved = new float[checked(options.BufferSize * inputChannelCount)];
            outputInterleaved = new float[checked(options.BufferSize * outputChannelCount)];
            inputSamples = new float[options.BufferSize];
            outputSamples = new float[options.BufferSize];

            var inputParameters = CreateParameters(deviceIndex, inputChannelCount, info.defaultLowInputLatency);
            var outputParameters = CreateParameters(deviceIndex, outputChannelCount, info.defaultLowOutputLatency);
            callback = OnCallback;
            try
            {
                stream = new PaStream(
                    inParams: inputParameters,
                    outParams: outputParameters,
                    sampleRate: options.SampleRate,
                    framesPerBuffer: checked((uint)options.BufferSize),
                    streamFlags: StreamFlags.ClipOff,
                    callback: callback,
                    userData: IntPtr.Zero);
            }
            catch (PortAudioException exception)
            {
                throw new AudioBackendException(
                    $"PortAudio could not open the selected device: {exception.Message}", exception);
            }

            SampleRate = options.SampleRate;
            BufferSize = options.BufferSize;
        }

        public int SampleRate { get; }
        public int BufferSize { get; }

        public void Start(AudioProcessCallback processCallback)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            this.processCallback = processCallback ?? throw new ArgumentNullException(nameof(processCallback));
            callbackError = null;
            try
            {
                stream.Start();
                started = true;
            }
            catch (PortAudioException exception)
            {
                throw new AudioBackendException("PortAudio could not start the stream.", exception);
            }
        }

        public void Stop()
        {
            if (!started)
            {
                return;
            }

            try
            {
                stream.Stop();
            }
            catch (PortAudioException exception)
            {
                throw new AudioBackendException("PortAudio could not stop the stream.", exception);
            }
            finally
            {
                started = false;
            }

            if (callbackError is not null)
            {
                throw new AudioBackendException("PortAudio callback failed.", callbackError);
            }
        }

        private StreamCallbackResult OnCallback(
            IntPtr input,
            IntPtr output,
            uint frameCount,
            ref StreamCallbackTimeInfo timeInfo,
            StreamCallbackFlags statusFlags,
            IntPtr userData)
        {
            try
            {
                var frames = checked((int)frameCount);
                if (frames > BufferSize)
                {
                    throw new AudioBackendException(
                        $"PortAudio returned {frames} frames, exceeding the selected buffer size {BufferSize}.");
                }

                var inputFloatCount = checked(frames * inputChannelCount);
                var outputFloatCount = checked(frames * outputChannelCount);
                Array.Clear(outputInterleaved, 0, outputFloatCount);
                if (input != IntPtr.Zero)
                {
                    Marshal.Copy(input, inputInterleaved, 0, inputFloatCount);
                }
                else
                {
                    Array.Clear(inputInterleaved, 0, inputFloatCount);
                }

                for (var frame = 0; frame < frames; frame++)
                {
                    inputSamples[frame] = inputInterleaved[frame * inputChannelCount + inputChannel];
                    outputSamples[frame] = 0;
                }

                processCallback?.Invoke(inputSamples, outputSamples, frames);
                for (var frame = 0; frame < frames; frame++)
                {
                    outputInterleaved[frame * outputChannelCount + outputChannel] = outputSamples[frame];
                }

                if (output != IntPtr.Zero)
                {
                    Marshal.Copy(outputInterleaved, 0, output, outputFloatCount);
                }

                return StreamCallbackResult.Continue;
            }
            catch (Exception exception)
            {
                Interlocked.CompareExchange(ref callbackError, exception, null);
                if (output != IntPtr.Zero)
                {
                    var count = Math.Min((long)frameCount * outputChannelCount, outputInterleaved.LongLength);
                    Array.Clear(outputInterleaved, 0, (int)count);
                    Marshal.Copy(outputInterleaved, 0, output, (int)count);
                }

                return StreamCallbackResult.Abort;
            }
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
                    stream.Stop();
                }
            }
            catch
            {
                // Preserve the original measurement error during disposal.
            }
            finally
            {
                started = false;
                processCallback = null;
                stream.Dispose();
            }
        }
    }
}