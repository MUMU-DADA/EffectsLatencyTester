using EffectsLatencyTester.Core;

namespace EffectsLatencyTester.Audio.Unsupported;

public abstract class UnsupportedAudioBackend : IAudioBackend
{
    protected UnsupportedAudioBackend(string name, string platform)
    {
        Name = name;
        Platform = platform;
    }

    public string Name { get; }
    protected string Platform { get; }

    public IReadOnlyList<AudioDeviceInfo> EnumerateDevices() => [];

    public IAudioDuplexStream OpenStream(AudioStreamOptions options)
    {
        throw new PlatformNotSupportedException(
            $"The {Name} backend for {Platform} is not available on this platform.");
    }
}
