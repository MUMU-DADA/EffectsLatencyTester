using System.Globalization;
using System.Resources;

namespace EffectsLatencyTester;

public static class I18n
{
    private static readonly ResourceManager ResourceManager =
        new("EffectsLatencyTester.Strings", typeof(I18n).Assembly);

    public static string AppTitle => Get(nameof(AppTitle));
    public static string Description => Get(nameof(Description));
    public static string ThemeLabel => Get(nameof(ThemeLabel));
    public static string ThemeDark => Get(nameof(ThemeDark));
    public static string ThemeLight => Get(nameof(ThemeLight));
    public static string WaveformGroup => Get(nameof(WaveformGroup));
    public static string AsioDeviceGroup => Get(nameof(AsioDeviceGroup));
    public static string Refresh => Get(nameof(Refresh));
    public static string SampleRate => Get(nameof(SampleRate));
    public static string BufferSize => Get(nameof(BufferSize));
    public static string BufferTooltip => Get(nameof(BufferTooltip));
    public static string OutputChannel => Get(nameof(OutputChannel));
    public static string InputChannel => Get(nameof(InputChannel));
    public static string BaselineButton => Get(nameof(BaselineButton));
    public static string EffectButton => Get(nameof(EffectButton));
    public static string BaselineNotRecorded => Get(nameof(BaselineNotRecorded));
    public static string InitialLoading => Get(nameof(InitialLoading));
    public static string RecentTest => Get(nameof(RecentTest));
    public static string DetectedLatency => Get(nameof(DetectedLatency));
    public static string Time1Value => Get(nameof(Time1Value));
    public static string Time2DeltaValue => Get(nameof(Time2DeltaValue));
    public static string Time1Short => Get(nameof(Time1Short));
    public static string Time2Short => Get(nameof(Time2Short));
    public static string InputWaveform => Get(nameof(InputWaveform));
    public static string OutputWaveform => Get(nameof(OutputWaveform));
    public static string CombinedWaveform => Get(nameof(CombinedWaveform));
    public static string WaveformEmpty => Get(nameof(WaveformEmpty));
    public static string Cursor => Get(nameof(Cursor));
    public static string TimeSeconds => Get(nameof(TimeSeconds));
    public static string NameLabel => Get(nameof(NameLabel));
    public static string StateLabel => Get(nameof(StateLabel));
    public static string DriverReady => Get(nameof(DriverReady));
    public static string CurrentSampleRate => Get(nameof(CurrentSampleRate));
    public static string BufferRange => Get(nameof(BufferRange));
    public static string InitialSelection => Get(nameof(InitialSelection));
    public static string SupportedSampleRates => Get(nameof(SupportedSampleRates));
    public static string OutputChannelCount => Get(nameof(OutputChannelCount));
    public static string InputChannelCount => Get(nameof(InputChannelCount));
    public static string DeviceParametersLoaded => Get(nameof(DeviceParametersLoaded));
    public static string FoundDrivers => Get(nameof(FoundDrivers));
    public static string NoDrivers => Get(nameof(NoDrivers));
    public static string ReadDriversFailed => Get(nameof(ReadDriversFailed));
    public static string ReadDriversFailedDetails => Get(nameof(ReadDriversFailedDetails));
    public static string BaselineStatus => Get(nameof(BaselineStatus));
    public static string EffectStatus => Get(nameof(EffectStatus));
    public static string BaselineRecorded => Get(nameof(BaselineRecorded));
    public static string BaselineNext => Get(nameof(BaselineNext));
    public static string PleaseBaseline => Get(nameof(PleaseBaseline));
    public static string EffectLatency => Get(nameof(EffectLatency));
    public static string EffectBelowBaseline => Get(nameof(EffectBelowBaseline));
    public static string PleaseChooseDriver => Get(nameof(PleaseChooseDriver));
    public static string PleaseChooseSettings => Get(nameof(PleaseChooseSettings));
    public static string NoPulse => Get(nameof(NoPulse));
    public static string MeasurementFailed => Get(nameof(MeasurementFailed));
    public static string DriverUnavailable => Get(nameof(DriverUnavailable));
    public static string DriverCannotOpen => Get(nameof(DriverCannotOpen));
    public static string AsioDriverGone => Get(nameof(AsioDriverGone));
    public static string UnsupportedSampleRate => Get(nameof(UnsupportedSampleRate));
    public static string BufferUnsupported => Get(nameof(BufferUnsupported));
    public static string BufferActualMismatch => Get(nameof(BufferActualMismatch));
    public static string AudioCallbackFailed => Get(nameof(AudioCallbackFailed));
    public static string InputBufferChanged => Get(nameof(InputBufferChanged));
    public static string UnsupportedOutputFormat => Get(nameof(UnsupportedOutputFormat));
    public static string InputFallback => Get(nameof(InputFallback));
    public static string OutputFallback => Get(nameof(OutputFallback));
    public static string BufferSamples => Get(nameof(BufferSamples));
    public static string SampleRateUnit => Get(nameof(SampleRateUnit));
    public static string ExportResult => Get(nameof(ExportResult));
    public static string ExportDialogTitle => Get(nameof(ExportDialogTitle));
    public static string ExportDialogFilter => Get(nameof(ExportDialogFilter));
    public static string ExportSuccess => Get(nameof(ExportSuccess));
    public static string ExportFailed => Get(nameof(ExportFailed));
    public static string ExportNoData => Get(nameof(ExportNoData));
    public static string AlreadyRunning => Get(nameof(AlreadyRunning));

    public static void Initialize(IReadOnlyList<string> args)
    {
        var requestedCulture = GetRequestedCulture(args);
        if (requestedCulture is null)
        {
            UseSystemCulture();
            return;
        }

        if (!TrySetCulture(requestedCulture))
        {
            SetCulture(CultureInfo.GetCultureInfo("en"));
        }
    }

    public static void UseSystemCulture()
    {
        if (CultureInfo.CurrentUICulture == CultureInfo.InvariantCulture)
        {
            SetCulture(CultureInfo.InstalledUICulture);
        }
    }

    private static bool TrySetCulture(string cultureName)
    {
        try
        {
            SetCulture(CultureInfo.GetCultureInfo(cultureName));
            return true;
        }
        catch (CultureNotFoundException)
        {
            return false;
        }
    }

    private static void SetCulture(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    private static string? GetRequestedCulture(IReadOnlyList<string> args)
    {
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument.StartsWith("--lang=", StringComparison.OrdinalIgnoreCase) ||
                argument.StartsWith("--language=", StringComparison.OrdinalIgnoreCase))
            {
                var separator = argument.IndexOf('=');
                var value = argument[(separator + 1)..].Trim();
                return value.Length > 0 ? value : null;
            }

            if (string.Equals(argument, "--lang", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "--language", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 < args.Count)
                {
                    var value = args[++index].Trim();
                    if (!value.StartsWith("-", StringComparison.Ordinal) && value.Length > 0)
                    {
                        return value;
                    }
                }
            }
        }

        return null;
    }

    public static string Get(string key)
    {
        return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }

    public static string Format(string key, params object?[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }
}
