namespace EffectsLatencyTester.Core;

public sealed record AudioChannelInfo(int Index, string Name)
{
    public string DisplayName => $"{Index}: {Name}";
}

public sealed record AudioDeviceCapabilities(
    string Name,
    int CurrentSampleRate,
    int PreferredBufferSize,
    int BufferMinSize,
    int BufferMaxSize,
    int BufferGranularity,
    IReadOnlyList<int> SupportedSampleRates,
    IReadOnlyList<int> SupportedBufferSizes,
    IReadOnlyList<AudioChannelInfo> InputChannels,
    IReadOnlyList<AudioChannelInfo> OutputChannels);

public sealed record AudioDeviceInfo(
    string Id,
    string Name,
    string BackendName,
    bool IsAvailable,
    string Status,
    AudioDeviceCapabilities? Capabilities)
{
    public string DisplayName => Name;
}

public sealed record AudioStreamOptions(
    string DeviceId,
    int SampleRate,
    int BufferSize,
    int OutputChannel,
    int InputChannel);

public delegate void AudioProcessCallback(float[] inputSamples, float[] outputSamples, int sampleCount);

public interface IAudioDuplexStream : IDisposable
{
    int SampleRate { get; }
    int BufferSize { get; }
    void Start(AudioProcessCallback callback);
    void Stop();
}

public interface IAudioBackend
{
    string Name { get; }
    IReadOnlyList<AudioDeviceInfo> EnumerateDevices();
    IAudioDuplexStream OpenStream(AudioStreamOptions options);
}

public sealed class AudioBackendException : Exception
{
    public AudioBackendException(string message)
        : base(message)
    {
    }

    public AudioBackendException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}