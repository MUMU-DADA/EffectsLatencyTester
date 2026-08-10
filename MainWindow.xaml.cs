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
                StatusTextBlock.Text = $"已发现 {DriverComboBox.Items.Count} 个 ASIO 驱动";
            }
            else
            {
                DetailsTextBox.Clear();
                StatusTextBlock.Text = "未发现 ASIO 驱动";
            }
        }
        catch (Exception ex)
        {
            DetailsTextBox.Text = ex.ToString();
            StatusTextBlock.Text = "读取 ASIO 驱动失败";
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
                $"名称: {capabilities.Name}{Environment.NewLine}" +
                $"状态: 驱动可打开{Environment.NewLine}" +
                $"当前采样率: {capabilities.CurrentSampleRate} Hz{Environment.NewLine}" +
                $"Buffer: {capabilities.BufferMinSize} - {capabilities.BufferMaxSize} samples" +
                $"，首选 {capabilities.PreferredBufferSize}{Environment.NewLine}" +
                $"初始选择: {defaultSampleRate?.Value ?? 0} Hz / {defaultBufferSize?.Value ?? 0} samples{Environment.NewLine}" +
                $"支持采样率: {string.Join(", ", capabilities.SupportedSampleRates)} Hz{Environment.NewLine}" +
                $"输出通道数: {capabilities.OutputChannels.Count}{Environment.NewLine}" +
                $"输入通道数: {capabilities.InputChannels.Count}";
            StatusTextBlock.Text = "设备参数已加载";
        }
        catch (Exception ex)
        {
            ClearDeviceOptions();
            var error = DescribeDriverError(ex);
            DetailsTextBox.Text = $"名称: {driver.Name}{Environment.NewLine}" +
                                  $"状态: {error}";
            StatusTextBlock.Text = error.Replace(Environment.NewLine, " ");
        }
    }

    private async void MeasureBaselineButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await MeasureAsync("正在测量声卡直连基准，请确认输出已直接接回输入...");
        if (result is not { HasResult: true })
        {
            return;
        }

        baselineMilliseconds = result.Value.LatencyMilliseconds;
        BaselineTextBlock.Text = $"声卡直连基准：{result.Value.LatencyMilliseconds:F2} ms（{result.Value.LatencySamples} samples）";
        StatusTextBlock.Text = "直连基准已记录。现在把效果器板接入后，再点击“测量效果器回路”。";
    }

    private async void MeasureEffectButton_Click(object sender, RoutedEventArgs e)
    {
        if (baselineMilliseconds is null)
        {
            StatusTextBlock.Text = "请先测量声卡直连基准";
            return;
        }

        var result = await MeasureAsync("正在测量效果器回路，请确认效果器已接线且音量较低...");
        if (result is not { HasResult: true })
        {
            return;
        }

        var pedalboardMilliseconds = result.Value.LatencyMilliseconds - baselineMilliseconds.Value;
        StatusTextBlock.Text = pedalboardMilliseconds >= 0
            ? $"效果器板延迟：{pedalboardMilliseconds:F2} ms（总往返 {result.Value.LatencyMilliseconds:F2} ms）"
            : $"效果器回路结果低于基准：{pedalboardMilliseconds:F2} ms，请检查接线或重复测量";
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
            StatusTextBlock.Text = "请先选择 ASIO 驱动";
            return null;
        }

        if (selectedCapabilities is null ||
            SampleRateComboBox.SelectedItem is not SampleRateItem sampleRate ||
            BufferSizeComboBox.SelectedItem is not BufferSizeItem bufferSize ||
            OutputChannelComboBox.SelectedItem is not AsioChannelChoice outputChannel ||
            InputChannelComboBox.SelectedItem is not AsioChannelChoice inputChannel)
        {
            StatusTextBlock.Text = "请先选择有效的采样率、输入通道和输出通道";
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
                StatusTextBlock.Text = "未检测到返回脉冲，请检查输入输出通道、接线和音量";
                return null;
            }

            return result;
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"测量失败：{DescribeDriverError(ex)}";
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
            return "驱动已注册，但当前没有找到硬件；请连接并启动设备，或关闭正在占用它的音频程序";
        }

        return $"无法打开驱动{Environment.NewLine}{message}";
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
        BaselineTextBlock.Text = "尚未记录声卡直连基准";
    }

    private sealed record AsioDriverItem(string Name);

    private sealed record SampleRateItem(int Value)
    {
        public string DisplayName => $"{Value} Hz";
    }

    private sealed record BufferSizeItem(int Value)
    {
        public string DisplayName => $"{Value} samples";
    }
}
