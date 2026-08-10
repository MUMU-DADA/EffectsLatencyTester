using NAudio.Wave.Asio;

namespace LatencyTester;

internal sealed record AsioChannelChoice(int Index, string Name)
{
    public string DisplayName => $"{Index}: {Name}";
}

internal sealed record AsioDeviceCapabilities(
    string Name,
    int CurrentSampleRate,
    int PreferredBufferSize,
    int BufferMinSize,
    int BufferMaxSize,
    int BufferGranularity,
    IReadOnlyList<int> SupportedSampleRates,
    IReadOnlyList<int> SupportedBufferSizes,
    IReadOnlyList<AsioChannelChoice> InputChannels,
    IReadOnlyList<AsioChannelChoice> OutputChannels);

internal static class AsioDeviceInspector
{
    private static readonly int[] SampleRateCandidates =
    [
        8000, 11025, 16000, 22050, 32000, 44100, 48000,
        88200, 96000, 176400, 192000, 352800, 384000, 768000,
    ];

    private static readonly int[] CommonBufferSizes =
    [16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768];

    public static AsioDeviceCapabilities Inspect(string driverName)
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
                .Select((channel, index) => new AsioChannelChoice(index, string.IsNullOrWhiteSpace(channel.name)
                    ? $"Input {index + 1}"
                    : channel.name.Trim()))
                .ToArray();
            var outputChannels = capabilities.OutputChannelInfos
                .Select((channel, index) => new AsioChannelChoice(index, string.IsNullOrWhiteSpace(channel.name)
                    ? $"Output {index + 1}"
                    : channel.name.Trim()))
                .ToArray();

            return new AsioDeviceCapabilities(
                driverName,
                currentSampleRate,
                capabilities.BufferPreferredSize,
                capabilities.BufferMinSize,
                capabilities.BufferMaxSize,
                capabilities.BufferGranularity,
                sampleRates.OrderBy(rate => rate).ToArray(),
                BuildBufferSizes(capabilities.BufferMinSize, capabilities.BufferMaxSize,
                    capabilities.BufferPreferredSize, capabilities.BufferGranularity),
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
        if (sampleRate <= 0 || sampleRate > int.MaxValue)
        {
            return 0;
        }

        return (int)Math.Round(sampleRate);
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
            // A granularity of zero means the driver accepts arbitrary sizes.
            // Showing representative values is more useful than thousands of
            // entries in a combo box.
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
}
