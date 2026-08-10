using System.Windows;
using NAudio.Wave;

namespace LatencyTester;

public partial class MainWindow : Window
{
    private double? baselineMilliseconds;
    private AsioDeviceCapabilities? selectedCapabilities;
    private bool updatingWaveformScrollBar;

    public MainWindow()
    {
        InitializeComponent();
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

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshDrivers();
    }

    private void RefreshDrivers()
    {
        try
        {
            ResetBaseline();
            ClearDeviceOptions();
            DriverComboBox.ItemsSource = AsioOut.GetDriverNames()
                .Select(name => new AsioDriverItem(name))
                .ToList();

            if (DriverComboBox.Items.Count > 0)
            {
                DriverComboBox.SelectedIndex = 0;
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
            selectedCapabilities = AsioDeviceInspector.Inspect(driver.Name);
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
        var result = await MeasureAsync(I18n.BaselineStatus);
        if (result is not { HasResult: true })
        {
            return;
        }

        baselineMilliseconds = result.Value.LatencyMilliseconds;
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

        var result = await MeasureAsync(I18n.EffectStatus);
        if (result is not { HasResult: true })
        {
            return;
        }

        var pedalboardMilliseconds = result.Value.LatencyMilliseconds - baselineMilliseconds.Value;
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
        }
    }

    private async Task<LatencyResult?> MeasureAsync(string status)
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

    private void ClearDeviceOptions()
    {
        selectedCapabilities = null;
        SampleRateComboBox.ItemsSource = null;
        BufferSizeComboBox.ItemsSource = null;
        OutputChannelComboBox.ItemsSource = null;
        InputChannelComboBox.ItemsSource = null;
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
