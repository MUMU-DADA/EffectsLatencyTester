using EffectsLatencyTester.Audio.PortAudio;
using EffectsLatencyTester.Audio.Unsupported;
using EffectsLatencyTester.Audio.Windows;
using EffectsLatencyTester.Core;

namespace EffectsLatencyTester.Audio;

public static class AudioBackendFactory
{
    public static IAudioBackend CreateForCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
        {
            return new AsioAudioBackend();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new PortAudioBackend("Core Audio");
        }

        if (OperatingSystem.IsLinux())
        {
            return new PortAudioBackend("ALSA/PipeWire/JACK");
        }

        return new UnsupportedAudioBackendForUnknownPlatform();
    }

    private sealed class UnsupportedAudioBackendForUnknownPlatform : UnsupportedAudioBackend
    {
        public UnsupportedAudioBackendForUnknownPlatform()
            : base("Unsupported audio backend", Environment.OSVersion.Platform.ToString())
        {
        }
    }
}