using Microsoft.Win32;
using System.Windows;
using NAudio.Wave;

namespace EffectsLatencyTester;

public partial class MainWindow : Window
{
    private double? baselineMilliseconds;
    private AsioDeviceCapabilities? selectedCapabilities;
    private string? pendingDriverName;
    private AsioDeviceCapabilities? pendingDriverCapabilities;
    private MeasurementExportData? currentExportData;
    private bool updatingWaveformScrollBar;

    public MainWindow()
    {
        InitializeComponent();
        ThemeComboBox.SelectedValue = ThemeManager.CurrentMode.ToString();
        DriverComboBox.SelectionChanged += DriverComboBox_SelectionChanged;
        SampleRateComboBox.SelectionChanged += MeasurementSetting_SelectionChanged;
        BufferSizeComboBox.SelectionChanged += MeasurementSetting_SelectionChanged;
        OutputChannelComboBox.SelectionChanged += MeasurementSetting_SelectionChanged;
        InputChannelComboBox.SelectionChanged += MeasurementSetting_SelectionChanged;
        WaveformPlot.ViewportChanged += WaveformPlot_ViewportChanged;
        WaveformScrollBar.ValueChanged += WaveformScrollBar_ValueChanged;
        RefreshDrivers();
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
            WaveformScrollBar.Visibility = needsHorizontalScroll
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        finally
        {
            updatingWaveformScrollBar = false;
        }
    }

    private void WaveformScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!updatingWaveformScrollBar)
        {
            WaveformPlot.SetHorizontalOffset(e.NewValue);
        }
    }

    private void ThemeComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedValue is string value &&
            Enum.TryParse<ThemeMode>(value, ignoreCase: true, out var mode) &&
            mode != ThemeManager.CurrentMode)
        {
            ThemeManager.SetTheme(mode);
        }
    }
    private void RefreshButton_Click(object sender, RoutedEventArgs e)
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
            pendingDriverName = null;
            pendingDriverCapabilities = null;
            var driverItems = AsioOut.GetDriverNames()
                .Select(name => new AsioDriverItem(name))
                .ToList();
            DriverComboBox.ItemsSource = driverItems;

            if (DriverComboBox.Items.Count > 0)
            {
                var preferredDriver = driverItems
                    .Select((driver, index) => new
                    {
                        Driver = driver,
                        Index = index,
                        Capabilities = TryInspectDriver(driver.Name),
                    })
                    .FirstOrDefault(candidate => candidate.Capabilities is not null);

                if (preferredDriver is not null)
                {
                    pendingDriverName = preferredDriver.Driver.Name;
                    pendingDriverCapabilities = preferredDriver.Capabilities;
                    DriverComboBox.SelectedIndex = preferredDriver.Index;
                }
                else
                {
                    DriverComboBox.SelectedIndex = 0;
                }

                StatusTextBlock.Text = I18n.Format(nameof(I18n.FoundDrivers), DriverComboBox.Items.Count);
            }
            else
            {
                DetailsTextBox.Clear();
                StatusTextBlock.Text = I18n.NoDrivers;
            }
        }
        catch (Exception ex)
        {
            DetailsTextBox.Text = I18n.Format(nameof(I18n.ReadDriversFailedDetails), ex.Message);
            StatusTextBlock.Text = I18n.ReadDriversFailed;
        }
    }

    private void DriverComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DriverComboBox.SelectedItem is not AsioDriverItem driver)
        {
            ClearDeviceOptions();
            DetailsTextBox.Clear();
            return;
        }

        try
        {
            ResetBaseline();
            ClearCurrentExport();
            selectedCapabilities = TakePendingCapabilities(driver.Name) ?? AsioDeviceInspector.Inspect(driver.Name);
            var capabilities = selectedCapabilities!;

            var sampleRateItems = capabilities.SupportedSampleRates
                .Select(rate => new SampleRateItem(rate))
                .ToList();
            SampleRateComboBox.ItemsSource = sampleRateItems;
            var defaultSampleRate = sampleRateItems
                .FirstOrDefault(rate => rate.Value == capabilities.CurrentSampleRate)
                ?? sampleRateItems.FirstOrDefault();
            SampleRateComboBox.SelectedItem = defaultSampleRate;

            var bufferSizeItems = capabilities.SupportedBufferSizes
                .Select(size => new BufferSizeItem(size))
                .ToList();
            BufferSizeComboBox.ItemsSource = bufferSizeItems;
            var defaultBufferSize = bufferSizeItems
                .FirstOrDefault(size => size.Value == capabilities.PreferredBufferSize)
                ?? bufferSizeItems.FirstOrDefault();
            BufferSizeComboBox.SelectedItem = defaultBufferSize;

            OutputChannelComboBox.ItemsSource = capabilities.OutputChannels;
            InputChannelComboBox.ItemsSource = capabilities.InputChannels;
            OutputChannelComboBox.SelectedIndex = capabilities.OutputChannels.Count > 0 ? 0 : -1;
            InputChannelComboBox.SelectedIndex = capabilities.InputChannels.Count > 0 ? 0 : -1;

            DetailsTextBox.Text =
                string.Join(
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
        catch (Exception ex)
        {
            ClearDeviceOptions();
            var error = DescribeDriverError(ex);
            DetailsTextBox.Text = string.Join(
                Environment.NewLine,
                I18n.Format(nameof(I18n.NameLabel), driver.Name),
                I18n.Format(nameof(I18n.StateLabel), error));
            StatusTextBlock.Text = error.Replace(Environment.NewLine, " ");
        }
    }

    private async void MeasureBaselineButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await MeasureAsync(I18n.BaselineStatus, isBaseline: true);
        if (result is not { HasResult: true })
        {
            return;
        }

        baselineMilliseconds = result.Value.LatencyMilliseconds;
        if (currentExportData is not null)
        {
            currentExportData = currentExportData with
            {
                BaselineMilliseconds = baselineMilliseconds,
            };
        }

        BaselineTextBlock.Text = I18n.Format(nameof(I18n.BaselineRecorded),
            result.Value.LatencyMilliseconds, result.Value.LatencySamples);
        StatusTextBlock.Text = I18n.BaselineNext;
    }

    private async void MeasureEffectButton_Click(object sender, RoutedEventArgs e)
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

        var pedalboardMilliseconds = result.Value.LatencyMilliseconds - baselineMilliseconds.Value;
        if (currentExportData is not null)
        {
            currentExportData = currentExportData with
            {
                BaselineMilliseconds = baselineMilliseconds,
                EffectsBoardLatencyMilliseconds = pedalboardMilliseconds,
            };
        }

        StatusTextBlock.Text = pedalboardMilliseconds >= 0
            ? I18n.Format(nameof(I18n.EffectLatency), pedalboardMilliseconds,
                result.Value.LatencyMilliseconds)
            : I18n.Format(nameof(I18n.EffectBelowBaseline), pedalboardMilliseconds);
    }

    private void MeasurementSetting_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0)
        {
            ResetBaseline();
            ClearCurrentExport();
        }
    }

    private async Task<LatencyResult?> MeasureAsync(string status, bool isBaseline)
    {
        if (DriverComboBox.SelectedItem is not AsioDriverItem driver)
        {
            StatusTextBlock.Text = I18n.PleaseChooseDriver;
            return null;
        }

        if (selectedCapabilities is null ||
            SampleRateComboBox.SelectedItem is not SampleRateItem sampleRate ||
            BufferSizeComboBox.SelectedItem is not BufferSizeItem bufferSize ||
            OutputChannelComboBox.SelectedItem is not AsioChannelChoice outputChannel ||
            InputChannelComboBox.SelectedItem is not AsioChannelChoice inputChannel)
        {
            StatusTextBlock.Text = I18n.PleaseChooseSettings;
            return null;
        }

        MeasureBaselineButton.IsEnabled = false;
        MeasureEffectButton.IsEnabled = false;
        if (isBaseline)
        {
            ClearCurrentExport();
        }
        else
        {
            ClearEffectsExport();
        }
        ExportCurrentButton.IsEnabled = false;
        DriverComboBox.IsEnabled = false;
        StatusTextBlock.Text = status;

        try
        {
            var result = await LatencyMeasurement.RunAsync(
                driver.Name,
                sampleRate.Value,
                bufferSize.Value,
                outputChannel.Index,
                inputChannel.Index);
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
                    driver.Name,
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
            else if (currentExportData is not null)
            {
                currentExportData = currentExportData with
                {
                    LastTestName = I18n.EffectButton,
                    EffectsResult = result,
                };
            }

            UpdateExportButtonState();

            return result;
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = I18n.Format(nameof(I18n.MeasurementFailed), DescribeDriverError(ex));
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

    private static string DescribeDriverError(Exception ex)
    {
        var message = ex.Message;
        if (message.Contains("Can not found a device", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("Please connect the device", StringComparison.OrdinalIgnoreCase))
        {
            return I18n.DriverUnavailable;
        }

        return I18n.Format(nameof(I18n.DriverCannotOpen), Environment.NewLine, message);
    }

    private static AsioDeviceCapabilities? TryInspectDriver(string driverName)
    {
        try
        {
            return AsioDeviceInspector.Inspect(driverName);
        }
        catch
        {
            return null;
        }
    }

    private AsioDeviceCapabilities? TakePendingCapabilities(string driverName)
    {
        if (!string.Equals(pendingDriverName, driverName, StringComparison.Ordinal))
        {
            pendingDriverName = null;
            pendingDriverCapabilities = null;
            return null;
        }

        var capabilities = pendingDriverCapabilities;
        pendingDriverName = null;
        pendingDriverCapabilities = null;
        return capabilities;
    }

    private void ClearDeviceOptions()
    {
        selectedCapabilities = null;
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

    private void ExportCurrentButton_Click(object sender, RoutedEventArgs e)
    {
        if (currentExportData is null)
        {
            StatusTextBlock.Text = I18n.ExportNoData;
            return;
        }

        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".zip",
            FileName = $"effects-latency-{DateTime.Now:yyyyMMdd-HHmmss}.zip",
            Filter = I18n.ExportDialogFilter,
            OverwritePrompt = true,
            Title = I18n.ExportDialogTitle,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            MeasurementExport.CreateZip(dialog.FileName, currentExportData);
            StatusTextBlock.Text = I18n.Format(nameof(I18n.ExportSuccess), dialog.FileName);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = I18n.Format(nameof(I18n.ExportFailed), ex.Message);
        }
    }

    private void ResetBaseline()
    {
        baselineMilliseconds = null;
        BaselineTextBlock.Text = I18n.BaselineNotRecorded;
    }

    private sealed record AsioDriverItem(string Name);

    private sealed record SampleRateItem(int Value)
    {
        public string DisplayName => I18n.Format(nameof(I18n.SampleRateUnit), Value);
    }

    private sealed record BufferSizeItem(int Value)
    {
        public string DisplayName => I18n.Format(nameof(I18n.BufferSamples), Value);
    }
}
