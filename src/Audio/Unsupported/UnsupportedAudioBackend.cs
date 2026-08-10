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
            $"The {Name} backend for {Platform} has not been enabled in this build yet.");
    }
}

public sealed class CoreAudioBackend : UnsupportedAudioBackend
{
    public CoreAudioBackend()
        : base("Core Audio", "macOS")
    {
    }
}

public sealed class LinuxAudioBackend : UnsupportedAudioBackend
{
    public LinuxAudioBackend()
        : base("ALSA/PipeWire/JACK", "Linux")
    {
    }
}