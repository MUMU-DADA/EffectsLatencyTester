using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using EffectsLatencyTester.Audio;
using EffectsLatencyTester.Core;

namespace EffectsLatencyTester;

public partial class MainWindow : Window
{
    private readonly IAudioBackend audioBackend;
    private double? baselineMilliseconds;
    private AudioDeviceInfo? selectedDevice;
    private MeasurementExportData? currentExportData;
    private bool updatingWaveformScrollBar;

    public MainWindow()
    {
        InitializeComponent();
        audioBackend = AudioBackendFactory.CreateForCurrentPlatform();
        Title = I18n.AppTitle;
        ConfigureLocalizedText();
        ConfigureThemeSelector();

        DriverComboBox.SelectionChanged += DriverComboBox_SelectionChanged;
        SampleRateComboBox.SelectionChanged += MeasurementSetting_SelectionChanged;
        BufferSizeComboBox.SelectionChanged += MeasurementSetting_SelectionChanged;
        OutputChannelComboBox.SelectionChanged += MeasurementSetting_SelectionChanged;
        InputChannelComboBox.SelectionChanged += MeasurementSetting_SelectionChanged;
        WaveformPlot.ViewportChanged += WaveformPlot_ViewportChanged;
        WaveformScrollBar.ValueChanged += WaveformScrollBar_ValueChanged;
        RefreshDrivers();
    }

    private void ConfigureLocalizedText()
    {
        TitleTextBlock.Text = I18n.AppTitle;
        ThemeLabelTextBlock.Text = I18n.ThemeLabel;
        DescriptionTextBlock.Text = I18n.Description;
        WaveformGroupTextBlock.Text = I18n.WaveformGroup;
        AsioDeviceGroupTextBlock.Text = I18n.AsioDeviceGroup;
        RefreshButton.Content = I18n.Refresh;
        SampleRateLabel.Text = I18n.SampleRate;
        BufferSizeLabel.Text = I18n.BufferSize;
        OutputChannelLabel.Text = I18n.OutputChannel;
        InputChannelLabel.Text = I18n.InputChannel;
        MeasureBaselineButton.Content = I18n.BaselineButton;
        MeasureEffectButton.Content = I18n.EffectButton;
        ExportCurrentButton.Content = I18n.ExportResult;
        StatusTextBlock.Text = I18n.InitialLoading;
        BaselineTextBlock.Text = I18n.BaselineNotRecorded;
    }

    private void ConfigureThemeSelector()
    {
        ThemeComboBox.ItemsSource = new[]
        {
            new ThemeOption(ThemeMode.Dark, I18n.ThemeDark),
            new ThemeOption(ThemeMode.Light, I18n.ThemeLight),
        };
        ThemeComboBox.SelectedIndex = ThemeManager.CurrentMode == ThemeMode.Dark ? 0 : 1;
        ThemeComboBox.SelectionChanged += ThemeComboBox_SelectionChanged;
    }

    private void WaveformPlot_ViewportChanged(object? sender, WaveformViewportChangedEventArgs e)
    {
        updatingWaveformScrollBar = true;
        try
        {
            WaveformScrollBar.ViewportSize = e.Span;
            WaveformScrollBar.LargeChange = e.Span;
            WaveformScrollBar.SmallChange = Math.Max(e.Span / 10, 0.00001);
            WaveformScrollBar.Value = e.Offset;
            var needsHorizontalScroll = e.Span < 0.999999;
            WaveformScrollBar.IsEnabled = needsHorizontalScroll;
            WaveformScrollBar.IsVisible = needsHorizontalScroll;
        }
        finally
        {
            updatingWaveformScrollBar = false;
        }
    }

    private void WaveformScrollBar_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!updatingWaveformScrollBar)
        {
            WaveformPlot.SetHorizontalOffset(e.NewValue);
        }
    }

    private void ThemeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is ThemeOption option && option.Mode != ThemeManager.CurrentMode)
        {
            ThemeManager.SetTheme(option.Mode);
        }
    }

    private void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        RefreshDrivers();
    }

    private void RefreshDrivers()
    {
        try
        {
            ResetBaseline();
            ClearCurrentExport();
            ClearDeviceOptions();
            var devices = audioBackend.EnumerateDevices().ToList();
            DriverComboBox.ItemsSource = devices;
            if (devices.Count == 0)
            {
                DetailsTextBox.Text = $"Backend: {audioBackend.Name}";
                StatusTextBlock.Text = I18n.NoDrivers;
                return;
            }

            var preferredIndex = devices.FindIndex(device => device.IsAvailable);
            DriverComboBox.SelectedIndex = preferredIndex >= 0 ? preferredIndex : 0;
            StatusTextBlock.Text = I18n.Format(nameof(I18n.FoundDrivers), devices.Count);
        }
        catch (Exception exception)
        {
            DetailsTextBox.Text = I18n.Format(nameof(I18n.ReadDriversFailedDetails), exception.Message);
            StatusTextBlock.Text = I18n.ReadDriversFailed;
        }
    }

    private void DriverComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DriverComboBox.SelectedItem is not AudioDeviceInfo device)
        {
            ClearDeviceOptions();
            DetailsTextBox.Text = string.Empty;
            return;
        }

        selectedDevice = device;
        ResetBaseline();
        ClearCurrentExport();
        if (!device.IsAvailable || device.Capabilities is null)
        {
            ClearDeviceOptions();
            DetailsTextBox.Text = string.Join(
                Environment.NewLine,
                I18n.Format(nameof(I18n.NameLabel), device.Name),
                I18n.Format(nameof(I18n.StateLabel), device.Status));
            StatusTextBlock.Text = device.Status;
            return;
        }

        try
        {
            var capabilities = device.Capabilities;
            var sampleRateItems = capabilities.SupportedSampleRates
                .Select(rate => new SampleRateItem(rate))
                .ToList();
            SampleRateComboBox.ItemsSource = sampleRateItems;
            SampleRateComboBox.SelectedItem = sampleRateItems
                .FirstOrDefault(rate => rate.Value == capabilities.CurrentSampleRate)
                ?? sampleRateItems.FirstOrDefault();

            var bufferSizeItems = capabilities.SupportedBufferSizes
                .Select(size => new BufferSizeItem(size))
                .ToList();
            BufferSizeComboBox.ItemsSource = bufferSizeItems;
            BufferSizeComboBox.SelectedItem = bufferSizeItems
                .FirstOrDefault(size => size.Value == capabilities.PreferredBufferSize)
                ?? bufferSizeItems.FirstOrDefault();

            OutputChannelComboBox.ItemsSource = capabilities.OutputChannels;
            InputChannelComboBox.ItemsSource = capabilities.InputChannels;
            OutputChannelComboBox.SelectedIndex = capabilities.OutputChannels.Count > 0 ? 0 : -1;
            InputChannelComboBox.SelectedIndex = capabilities.InputChannels.Count > 0 ? 0 : -1;

            var defaultSampleRate = SampleRateComboBox.SelectedItem as SampleRateItem;
            var defaultBufferSize = BufferSizeComboBox.SelectedItem as BufferSizeItem;
            DetailsTextBox.Text = string.Join(
                Environment.NewLine,
                I18n.Format(nameof(I18n.NameLabel), capabilities.Name),
                I18n.Format(nameof(I18n.StateLabel), I18n.DriverReady),
                I18n.Format(nameof(I18n.CurrentSampleRate), capabilities.CurrentSampleRate),
                I18n.Format(nameof(I18n.BufferRange), capabilities.BufferMinSize,
                    capabilities.BufferMaxSize, capabilities.PreferredBufferSize),
                I18n.Format(nameof(I18n.InitialSelection), defaultSampleRate?.Value ?? 0,
                    defaultBufferSize?.Value ?? 0),
                I18n.Format(nameof(I18n.SupportedSampleRates),
                    string.Join(", ", capabilities.SupportedSampleRates)),
                I18n.Format(nameof(I18n.OutputChannelCount), capabilities.OutputChannels.Count),
                I18n.Format(nameof(I18n.InputChannelCount), capabilities.InputChannels.Count));
            StatusTextBlock.Text = I18n.DeviceParametersLoaded;
        }
        catch (Exception exception)
        {
            ClearDeviceOptions();
            DetailsTextBox.Text = I18n.Format(nameof(I18n.DriverCannotOpen), Environment.NewLine, exception.Message);
            StatusTextBlock.Text = exception.Message;
        }
    }

    private async void MeasureBaselineButton_Click(object? sender, RoutedEventArgs e)
    {
        var result = await MeasureAsync(I18n.BaselineStatus, isBaseline: true);
        if (result is not { HasResult: true })
        {
            return;
        }

        baselineMilliseconds = result.Value.LatencyMilliseconds;
        if (currentExportData is not null)
        {
            currentExportData = currentExportData with { BaselineMilliseconds = baselineMilliseconds };
        }

        BaselineTextBlock.Text = I18n.Format(nameof(I18n.BaselineRecorded),
            result.Value.LatencyMilliseconds, result.Value.LatencySamples);
        StatusTextBlock.Text = I18n.BaselineNext;
    }

    private async void MeasureEffectButton_Click(object? sender, RoutedEventArgs e)
    {
        if (baselineMilliseconds is null)
        {
            StatusTextBlock.Text = I18n.PleaseBaseline;
            return;
        }

        var result = await MeasureAsync(I18n.EffectStatus, isBaseline: false);
        if (result is not { HasResult: true })
        {
            return;
        }

        var effectsLatency = result.Value.LatencyMilliseconds - baselineMilliseconds.Value;
        if (currentExportData is not null)
        {
            currentExportData = currentExportData with
            {
                BaselineMilliseconds = baselineMilliseconds,
                EffectsBoardLatencyMilliseconds = effectsLatency,
                EffectsResult = result,
            };
        }

        StatusTextBlock.Text = effectsLatency >= 0
            ? I18n.Format(nameof(I18n.EffectLatency), effectsLatency, result.Value.LatencyMilliseconds)
            : I18n.Format(nameof(I18n.EffectBelowBaseline), effectsLatency);
    }

    private void MeasurementSetting_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
        {
            ResetBaseline();
            ClearCurrentExport();
        }
    }

    private async Task<LatencyResult?> MeasureAsync(string status, bool isBaseline)
    {
        if (selectedDevice is not { IsAvailable: true } device || device.Capabilities is null)
        {
            StatusTextBlock.Text = I18n.PleaseChooseDriver;
            return null;
        }

        if (SampleRateComboBox.SelectedItem is not SampleRateItem sampleRate ||
            BufferSizeComboBox.SelectedItem is not BufferSizeItem bufferSize ||
            OutputChannelComboBox.SelectedItem is not AudioChannelInfo outputChannel ||
            InputChannelComboBox.SelectedItem is not AudioChannelInfo inputChannel)
        {
            StatusTextBlock.Text = I18n.PleaseChooseSettings;
            return null;
        }

        MeasureBaselineButton.IsEnabled = false;
        MeasureEffectButton.IsEnabled = false;
        DriverComboBox.IsEnabled = false;
        ExportCurrentButton.IsEnabled = false;
        StatusTextBlock.Text = status;
        if (isBaseline)
        {
            ClearCurrentExport();
        }
        else
        {
            ClearEffectsExport();
        }

        try
        {
            var options = new AudioStreamOptions(
                device.Id,
                sampleRate.Value,
                bufferSize.Value,
                outputChannel.Index,
                inputChannel.Index);
            var result = await LatencyMeasurement.RunAsync(audioBackend, options);
            WaveformPlot.SetWaveforms(
                result.OutputSamples,
                result.InputSamples,
                result.SampleRate,
                result.HasResult ? result.LatencyMilliseconds : null);
            if (!result.HasResult)
            {
                StatusTextBlock.Text = I18n.NoPulse;
                return null;
            }

            if (isBaseline)
            {
                currentExportData = new MeasurementExportData(
                    I18n.BaselineButton,
                    device.Name,
                    sampleRate.Value,
                    bufferSize.Value,
                    outputChannel.Index,
                    outputChannel.Name,
                    inputChannel.Index,
                    inputChannel.Name,
                    result.LatencyMilliseconds,
                    null,
                    result,
                    null);
            }

            UpdateExportButtonState();
            return result;
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = I18n.Format(nameof(I18n.MeasurementFailed), exception.Message);
            return null;
        }
        finally
        {
            MeasureBaselineButton.IsEnabled = true;
            MeasureEffectButton.IsEnabled = true;
            DriverComboBox.IsEnabled = true;
            UpdateExportButtonState();
        }
    }

    private async void ExportCurrentButton_Click(object? sender, RoutedEventArgs e)
    {
        if (currentExportData is null)
        {
            StatusTextBlock.Text = I18n.ExportNoData;
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            return;
        }

        var storageFile = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = $"effects-latency-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            DefaultExtension = "zip",
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType("ZIP archive") { Patterns = ["*.zip"] },
            ],
        });
        var destinationPath = storageFile?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            return;
        }

        try
        {
            MeasurementExport.CreateZip(destinationPath, currentExportData);
            StatusTextBlock.Text = I18n.Format(nameof(I18n.ExportSuccess), destinationPath);
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = I18n.Format(nameof(I18n.ExportFailed), exception.Message);
        }
    }

    private void ClearDeviceOptions()
    {
        selectedDevice = null;
        SampleRateComboBox.ItemsSource = null;
        BufferSizeComboBox.ItemsSource = null;
        OutputChannelComboBox.ItemsSource = null;
        InputChannelComboBox.ItemsSource = null;
    }

    private void ClearCurrentExport()
    {
        currentExportData = null;
        ExportCurrentButton.IsEnabled = false;
    }

    private void ClearEffectsExport()
    {
        if (currentExportData is null)
        {
            ExportCurrentButton.IsEnabled = false;
            return;
        }

        currentExportData = currentExportData with
        {
            EffectsBoardLatencyMilliseconds = null,
            EffectsResult = null,
        };
        ExportCurrentButton.IsEnabled = currentExportData.BaselineResult is { HasResult: true };
    }

    private void UpdateExportButtonState()
    {
        ExportCurrentButton.IsEnabled =
            currentExportData?.BaselineResult is { HasResult: true } ||
            currentExportData?.EffectsResult is { HasResult: true };
    }

    private void ResetBaseline()
    {
        baselineMilliseconds = null;
        BaselineTextBlock.Text = I18n.BaselineNotRecorded;
    }

    private sealed record ThemeOption(ThemeMode Mode, string DisplayName);
    private sealed record SampleRateItem(int Value)
    {
        public string DisplayName => I18n.Format(nameof(I18n.SampleRateUnit), Value);
    }

    private sealed record BufferSizeItem(int Value)
    {
        public string DisplayName => I18n.Format(nameof(I18n.BufferSamples), Value);
    }
}