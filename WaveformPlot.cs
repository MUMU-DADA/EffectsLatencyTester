using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LatencyTester;

public sealed class WaveformPlot : FrameworkElement
{
    private static readonly Pen GridPen = new(new SolidColorBrush(Color.FromRgb(224, 228, 232)), 1);
    private static readonly Pen AxisPen = new(new SolidColorBrush(Color.FromRgb(130, 138, 146)), 1);
    private static readonly Pen BorderPen = new(new SolidColorBrush(Color.FromRgb(206, 212, 218)), 1);
    private static readonly Pen CursorPen = new(new SolidColorBrush(Color.FromRgb(42, 42, 42)), 1);
    private static readonly Pen OutputPen = new(new SolidColorBrush(Color.FromRgb(219, 86, 86)), 1.25);
    private static readonly Pen InputPen = new(new SolidColorBrush(Color.FromRgb(48, 116, 196)), 1.25);
    private static readonly Brush LabelBrush = new SolidColorBrush(Color.FromRgb(80, 88, 96));
    private static readonly Brush PlotBackground = new SolidColorBrush(Color.FromRgb(250, 251, 252));

    private const double PlotLeft = 48;
    private const double PlotTop = 36;
    private const double PlotRight = 14;
    private const double PlotBottom = 34;
    private const double PanelGap = 10;

    private float[] outputSamples = [];
    private float[] inputSamples = [];
    private int sampleRate;
    private double? latencyMilliseconds;
    private double? cursorX;

    public WaveformPlot()
    {
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        ClipToBounds = true;
        IsHitTestVisible = true;
    }

    public void SetWaveforms(
        IReadOnlyList<float> output,
        IReadOnlyList<float> input,
        int sampleRate,
        double? latencyMilliseconds)
    {
        outputSamples = output.ToArray();
        inputSamples = input.ToArray();
        this.sampleRate = sampleRate;
        this.latencyMilliseconds = latencyMilliseconds;
        cursorX = null;
        InvalidateVisual();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var point = e.GetPosition(this);
        var panels = GetPanels();
        var data = panels[0].Data;
        if (point.X >= data.Left && point.X <= data.Right &&
            point.Y >= panels[0].Panel.Top && point.Y <= panels[^1].Panel.Bottom)
        {
            cursorX = point.X;
        }
        else
        {
            cursorX = null;
        }

        InvalidateVisual();
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        cursorX = null;
        InvalidateVisual();
        base.OnMouseLeave(e);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var bounds = new Rect(RenderSize);
        drawingContext.DrawRectangle(PlotBackground, null, bounds);

        if (outputSamples.Length == 0 && inputSamples.Length == 0)
        {
            DrawText(drawingContext, "完成一次测试后显示输入/输出波形", new Point(18, 18), 13, LabelBrush);
            return;
        }

        var panels = GetPanels();
        var totalSamples = Math.Max(outputSamples.Length, inputSamples.Length);
        DrawText(drawingContext, "最近一次测试", new Point(PlotLeft, 8), 13, LabelBrush);
        if (latencyMilliseconds is not null)
        {
            DrawText(
                drawingContext,
                $"检测延迟 {latencyMilliseconds.Value:F2} ms",
                new Point(Math.Max(PlotLeft + 110, bounds.Width - 150), 8),
                12,
                LabelBrush);
        }

        DrawPanel(drawingContext, panels[0], "输入波形", inputSamples, InputPen, totalSamples, false);
        DrawPanel(drawingContext, panels[1], "输出波形", outputSamples, OutputPen, totalSamples, false);

        DrawPanel(drawingContext, panels[2], "叠加波形（输入 / 输出）", [], null, totalSamples, true);
        DrawWaveform(drawingContext, panels[2].Data, inputSamples, totalSamples, InputPen);
        DrawWaveform(drawingContext, panels[2].Data, outputSamples, totalSamples, OutputPen);

        DrawCursor(drawingContext, panels, totalSamples);
    }

    private void DrawPanel(
        DrawingContext drawingContext,
        PlotPanel panel,
        string title,
        IReadOnlyList<float> samples,
        Pen? waveformPen,
        int totalSamples,
        bool showTimeAxis)
    {
        drawingContext.DrawRectangle(null, BorderPen, panel.Panel);
        DrawText(drawingContext, title, new Point(panel.Panel.Left + 6, panel.Panel.Top + 3), 11, LabelBrush);
        DrawGrid(drawingContext, panel.Data, showTimeAxis, totalSamples);
        if (waveformPen is not null)
        {
            DrawWaveform(drawingContext, panel.Data, samples, totalSamples, waveformPen);
        }
    }

    private void DrawGrid(DrawingContext drawingContext, Rect data, bool showTimeAxis, int totalSamples)
    {
        for (var i = 0; i <= 4; i++)
        {
            var y = data.Top + data.Height * i / 4;
            drawingContext.DrawLine(i == 2 ? AxisPen : GridPen, new Point(data.Left, y), new Point(data.Right, y));
            var value = 1.0 - i * 0.5;
            DrawText(drawingContext, value.ToString("0.0", CultureInfo.InvariantCulture), new Point(4, y - 8), 9, LabelBrush);
        }

        const int timeTicks = 5;
        for (var i = 0; i <= timeTicks; i++)
        {
            var x = data.Left + data.Width * i / timeTicks;
            drawingContext.DrawLine(GridPen, new Point(x, data.Top), new Point(x, data.Bottom));
            if (showTimeAxis)
            {
                var totalSeconds = sampleRate > 0 ? totalSamples / (double)sampleRate : 0;
                DrawText(
                    drawingContext,
                    $"{totalSeconds * i / timeTicks:0.00}s",
                    new Point(x - 14, data.Bottom + 6),
                    9,
                    LabelBrush);
            }
        }
    }

    private static void DrawWaveform(
        DrawingContext drawingContext,
        Rect data,
        IReadOnlyList<float> samples,
        int totalSamples,
        Pen pen)
    {
        if (samples.Count == 0 || totalSamples <= 0 || data.Width < 2 || data.Height < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var pixelColumns = Math.Max(2, (int)Math.Ceiling(data.Width));
            for (var column = 0; column < pixelColumns; column++)
            {
                var timelineStart = (long)column * totalSamples / pixelColumns;
                var timelineEnd = Math.Max(
                    timelineStart + 1,
                    (long)(column + 1) * totalSamples / pixelColumns);
                var start = (int)Math.Min(samples.Count, timelineStart * samples.Count / totalSamples);
                var end = (int)Math.Min(
                    samples.Count,
                    Math.Max(start + 1, timelineEnd * samples.Count / totalSamples));

                var peak = 0f;
                for (var index = start; index < end; index++)
                {
                    if (Math.Abs(samples[index]) > Math.Abs(peak))
                    {
                        peak = samples[index];
                    }
                }

                var x = data.Left + data.Width * column / Math.Max(1, pixelColumns - 1);
                var y = data.Top + (1 - Math.Clamp((double)peak, -1, 1)) * data.Height / 2;
                if (column == 0)
                {
                    context.BeginFigure(new Point(x, y), false, false);
                }
                else
                {
                    context.LineTo(new Point(x, y), true, false);
                }
            }
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, pen, geometry);
    }

    private void DrawCursor(DrawingContext drawingContext, IReadOnlyList<PlotPanel> panels, int totalSamples)
    {
        if (cursorX is null || totalSamples <= 0 || sampleRate <= 0)
        {
            return;
        }

        var x = cursorX.Value;
        foreach (var panel in panels)
        {
            drawingContext.DrawLine(CursorPen, new Point(x, panel.Data.Top), new Point(x, panel.Data.Bottom));
        }

        var normalized = Math.Clamp((x - panels[0].Data.Left) / panels[0].Data.Width, 0, 1);
        var sampleIndex = (int)Math.Round(normalized * (totalSamples - 1));
        var time = sampleIndex / (double)sampleRate;
        var input = SampleAt(inputSamples, normalized);
        var output = SampleAt(outputSamples, normalized);
        var label = $"t={time:0.000}s  输入={input:0.000}  输出={output:0.000}";
        var labelX = x + 8;
        if (labelX > RenderSize.Width - 230)
        {
            labelX = Math.Max(PlotLeft, x - 230);
        }

        DrawText(drawingContext, label, new Point(labelX, 22), 10, LabelBrush);
    }

    private PlotPanel[] GetPanels()
    {
        var width = Math.Max(1, RenderSize.Width - PlotLeft - PlotRight);
        var availableHeight = Math.Max(3, RenderSize.Height - PlotTop - PlotBottom - PanelGap * 2);
        var panelHeight = Math.Max(1, availableHeight / 3);
        var panels = new PlotPanel[3];
        for (var index = 0; index < panels.Length; index++)
        {
            var panelTop = PlotTop + index * (panelHeight + PanelGap);
            var panel = new Rect(PlotLeft, panelTop, width, panelHeight);
            var axisSpace = index == panels.Length - 1 ? 24 : 7;
            var data = new Rect(
                panel.Left,
                panel.Top + 19,
                panel.Width,
                Math.Max(1, panel.Height - 19 - axisSpace));
            panels[index] = new PlotPanel(panel, data);
        }

        return panels;
    }

    private static float SampleAt(IReadOnlyList<float> samples, double normalized)
    {
        if (samples.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Round(Math.Clamp(normalized, 0, 1) * (samples.Count - 1));
        return samples[index];
    }

    private void DrawText(DrawingContext drawingContext, string text, Point origin, double fontSize, Brush brush)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            fontSize,
            brush,
            dpi);
        drawingContext.DrawText(formatted, origin);
    }

    private readonly record struct PlotPanel(Rect Panel, Rect Data);
}
