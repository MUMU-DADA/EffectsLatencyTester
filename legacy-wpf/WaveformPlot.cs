using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace EffectsLatencyTester;

public sealed class WaveformViewportChangedEventArgs : EventArgs
{
    public WaveformViewportChangedEventArgs(double offset, double span)
    {
        Offset = offset;
        Span = span;
    }

    public double Offset { get; }

    public double Span { get; }
}

public sealed class WaveformPlot : FrameworkElement
{
    private Pen gridPen = null!;
    private Pen axisPen = null!;
    private Pen borderPen = null!;
    private Pen cursorPen = null!;
    private Pen time1Pen = null!;
    private Pen time2Pen = null!;
    private Pen outputPen = null!;
    private Pen inputPen = null!;
    private Brush labelBrush = null!;
    private Brush mutedLabelBrush = null!;
    private Brush plotBackground = null!;

    private const double PlotLeft = 52;
    private const double PlotTop = 36;
    private const double PlotRight = 16;
    private const double PlotBottom = 36;
    private const double PanelGap = 10;
    // Allow zooming down to a very small fraction of the complete capture.
    // This is useful for inspecting individual pulse edges and sample-level timing.
    private const double MinimumViewportSpan = 0.00005;

    private float[] outputSamples = [];
    private float[] inputSamples = [];
    private int sampleRate;
    private double? latencyMilliseconds;
    private double? cursorX;
    private double viewOffset;
    private double viewSpan = 1;
    private bool isPanning;
    private bool panMoved;
    private double panStartX;
    private double panStartOffset;
    private double? time1;
    private double? time2;

    public WaveformPlot()
    {
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;
        ClipToBounds = true;
        IsHitTestVisible = true;
        ApplyTheme(ThemeManager.CurrentPalette);
        ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
    }

    private void ThemeManager_ThemeChanged(object? sender, ThemePalette palette)
    {
        ApplyTheme(palette);
        InvalidateVisual();
    }

    private void ApplyTheme(ThemePalette palette)
    {
        gridPen = CreatePen(palette.PlotGrid, 1);
        axisPen = CreatePen(palette.PlotAxis, 1);
        borderPen = CreatePen(palette.PlotBorder, 1);
        cursorPen = CreatePen(palette.PlotCursor, 1);
        time1Pen = CreatePen(palette.PlotTime1, 1.25);
        time2Pen = CreatePen(palette.PlotTime2, 1.25);
        outputPen = CreatePen(palette.PlotOutput, 1.5);
        inputPen = CreatePen(palette.PlotInput, 1.5);
        labelBrush = CreateBrush(palette.PlotLabel);
        mutedLabelBrush = CreateBrush(palette.PlotMutedLabel);
        plotBackground = CreateBrush(palette.PlotBackground);
    }

    private static Pen CreatePen(Color color, double thickness)
    {
        var brush = CreateBrush(color);
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public event EventHandler<WaveformViewportChangedEventArgs>? ViewportChanged;

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
        time1 = null;
        time2 = null;
        SetViewport(0, 1, raiseEvent: true);
        InvalidateVisual();
    }

    public void SetHorizontalOffset(double offset)
    {
        SetViewport(offset, viewSpan, raiseEvent: true);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var point = e.GetPosition(this);
        if (IsInsideAnyPanel(point))
        {
            isPanning = true;
            panMoved = false;
            panStartX = point.X;
            panStartOffset = viewOffset;
            CaptureMouse();
            e.Handled = true;
        }

        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (isPanning)
        {
            var point = e.GetPosition(this);
            var wasClick = !panMoved && IsInsideAnyPanel(point);
            isPanning = false;
            ReleaseMouseCapture();
            if (wasClick)
            {
                RecordTimeClick(point.X);
            }

            e.Handled = true;
        }

        base.OnMouseLeftButtonUp(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var point = e.GetPosition(this);
        var panels = GetPanels();
        var data = panels[0].Data;
        var inHorizontalPlot = point.X >= data.Left && point.X <= data.Right;

        if (isPanning)
        {
            if (Math.Abs(point.X - panStartX) >= 2)
            {
                panMoved = true;
            }

            var delta = (panStartX - point.X) / Math.Max(1, data.Width) * viewSpan;
            SetViewport(panStartOffset + delta, viewSpan, raiseEvent: true);
            cursorX = Math.Clamp(point.X, data.Left, data.Right);
        }
        else if (inHorizontalPlot && IsInsideAnyPanel(point))
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
        if (!isPanning)
        {
            cursorX = null;
            InvalidateVisual();
        }

        base.OnMouseLeave(e);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var hasSamples = inputSamples.Length > 0 || outputSamples.Length > 0;
        if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && hasSamples)
        {
            var panels = GetPanels();
            var data = panels[0].Data;
            var point = e.GetPosition(this);
            if (point.X >= data.Left && point.X <= data.Right && IsInsideAnyPanel(point))
            {
                var anchor = Math.Clamp((point.X - data.Left) / data.Width, 0, 1);
                var factor = e.Delta > 0 ? 0.8 : 1.25;
                var newSpan = Math.Clamp(viewSpan * factor, MinimumViewportSpan, 1);
                var absoluteAnchor = viewOffset + anchor * viewSpan;
                var newOffset = absoluteAnchor - anchor * newSpan;
                SetViewport(newOffset, newSpan, raiseEvent: true);
                cursorX = point.X;
                InvalidateVisual();
                e.Handled = true;
            }
        }
        else if (hasSamples)
        {
            var panels = GetPanels();
            var data = panels[0].Data;
            var point = e.GetPosition(this);
            if (point.X >= data.Left && point.X <= data.Right && IsInsideAnyPanel(point))
            {
                // Normal wheel behavior pans horizontally by a quarter of the
                // currently visible window. Ctrl+wheel remains reserved for zoom.
                var direction = e.Delta > 0 ? -1 : 1;
                SetViewport(viewOffset + direction * viewSpan * 0.25, viewSpan, raiseEvent: true);
                cursorX = point.X;
                InvalidateVisual();
                e.Handled = true;
            }
        }

        base.OnMouseWheel(e);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var bounds = new Rect(RenderSize);
        drawingContext.DrawRectangle(plotBackground, null, bounds);

        if (outputSamples.Length == 0 && inputSamples.Length == 0)
        {
            DrawText(drawingContext, I18n.WaveformEmpty, new Point(18, 18), 13, labelBrush);
            return;
        }

        var panels = GetPanels();
        var totalSamples = Math.Max(outputSamples.Length, inputSamples.Length);
        var header = I18n.RecentTest;
        if (latencyMilliseconds is not null)
        {
            header += $"    {I18n.Format(nameof(I18n.DetectedLatency), latencyMilliseconds.Value)}";
        }

        if (time1 is not null)
        {
            header += $"    {I18n.Format(nameof(I18n.Time1Value), ToMilliseconds(time1.Value))}";
        }

        if (time2 is not null)
        {
            header += $"    {I18n.Format(nameof(I18n.Time2DeltaValue), ToMilliseconds(time2.Value), Math.Abs(ToMilliseconds(time2.Value) - ToMilliseconds(time1!.Value)))}";
        }

        DrawText(drawingContext, header, new Point(PlotLeft, 8), 12, labelBrush);

        DrawPanel(drawingContext, panels[0], I18n.InputWaveform, inputSamples, inputPen, totalSamples, false);
        DrawPanel(drawingContext, panels[1], I18n.OutputWaveform, outputSamples, outputPen, totalSamples, false);
        DrawPanel(drawingContext, panels[2], I18n.CombinedWaveform, [], null, totalSamples, true);
        DrawWaveform(drawingContext, panels[2].Data, inputSamples, totalSamples, inputPen);
        DrawWaveform(drawingContext, panels[2].Data, outputSamples, totalSamples, outputPen);

        DrawTimeMarker(drawingContext, panels, totalSamples, time1, I18n.Time1Short, time1Pen);
        DrawTimeMarker(drawingContext, panels, totalSamples, time2, I18n.Time2Short, time2Pen);
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
        drawingContext.DrawRectangle(null, borderPen, panel.Panel);
        DrawText(drawingContext, title, new Point(panel.Panel.Left + 6, panel.Panel.Top + 3), 11, labelBrush);
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
            drawingContext.DrawLine(i == 2 ? axisPen : gridPen, new Point(data.Left, y), new Point(data.Right, y));
            var value = 1.0 - i * 0.5;
            DrawText(drawingContext, value.ToString("0.0", CultureInfo.InvariantCulture), new Point(4, y - 8), 9, mutedLabelBrush);
        }

        const int timeTicks = 5;
        var totalSeconds = sampleRate > 0 ? totalSamples / (double)sampleRate : 0;
        for (var i = 0; i <= timeTicks; i++)
        {
            var relative = i / (double)timeTicks;
            var x = data.Left + data.Width * relative;
            drawingContext.DrawLine(gridPen, new Point(x, data.Top), new Point(x, data.Bottom));
            if (showTimeAxis)
            {
                var seconds = (viewOffset + viewSpan * relative) * totalSeconds;
                DrawText(drawingContext, I18n.Format(nameof(I18n.TimeSeconds), seconds), new Point(x - 14, data.Bottom + 6), 9, mutedLabelBrush);
            }
        }
    }

    private void DrawWaveform(
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
                var timelineStart = viewOffset + (long)column * viewSpan / pixelColumns;
                var timelineEnd = viewOffset + (double)(column + 1) * viewSpan / pixelColumns;
                var start = (int)Math.Clamp(timelineStart * samples.Count, 0.0, samples.Count);
                var end = (int)Math.Clamp(Math.Max(timelineEnd * samples.Count, start + 1), 0.0, samples.Count);

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
            drawingContext.DrawLine(cursorPen, new Point(x, panel.Data.Top), new Point(x, panel.Data.Bottom));
        }

        var normalized = Math.Clamp((x - panels[0].Data.Left) / panels[0].Data.Width, 0, 1);
        var absolutePosition = viewOffset + normalized * viewSpan;
        var time = absolutePosition * totalSamples / sampleRate;
        var input = SampleAt(inputSamples, absolutePosition);
        var output = SampleAt(outputSamples, absolutePosition);
        var label = I18n.Format(nameof(I18n.Cursor), time, input, output);
        var labelX = x + 8;
        if (labelX > RenderSize.Width - 230)
        {
            labelX = Math.Max(PlotLeft, x - 230);
        }

        DrawText(drawingContext, label, new Point(labelX, 22), 10, labelBrush);
    }

    private void DrawTimeMarker(
        DrawingContext drawingContext,
        IReadOnlyList<PlotPanel> panels,
        int totalSamples,
        double? marker,
        string label,
        Pen pen)
    {
        if (marker is null || totalSamples <= 0 || marker < viewOffset || marker > viewOffset + viewSpan)
        {
            return;
        }

        var x = panels[0].Data.Left + (marker.Value - viewOffset) / viewSpan * panels[0].Data.Width;
        foreach (var panel in panels)
        {
            drawingContext.DrawLine(pen, new Point(x, panel.Data.Top), new Point(x, panel.Data.Bottom));
        }

        DrawText(drawingContext, label, new Point(x + 3, panels[0].Panel.Top + 3), 10, pen.Brush);
    }

    private void RecordTimeClick(double x)
    {
        if (sampleRate <= 0 || Math.Max(outputSamples.Length, inputSamples.Length) == 0)
        {
            return;
        }

        var panels = GetPanels();
        var normalized = Math.Clamp((x - panels[0].Data.Left) / panels[0].Data.Width, 0, 1);
        var absolutePosition = viewOffset + normalized * viewSpan;
        if (time1 is null || time2 is not null)
        {
            time1 = absolutePosition;
            time2 = null;
        }
        else
        {
            time2 = absolutePosition;
        }

        InvalidateVisual();
    }

    private void SetViewport(double offset, double span, bool raiseEvent)
    {
        viewSpan = Math.Clamp(span, MinimumViewportSpan, 1);
        viewOffset = Math.Clamp(offset, 0, 1 - viewSpan);
        InvalidateVisual();

        if (raiseEvent)
        {
            ViewportChanged?.Invoke(this, new WaveformViewportChangedEventArgs(viewOffset, viewSpan));
        }
    }

    private bool IsInsideAnyPanel(Point point)
    {
        var panels = GetPanels();
        return point.X >= panels[0].Data.Left && point.X <= panels[0].Data.Right &&
               point.Y >= panels[0].Panel.Top && point.Y <= panels[^1].Panel.Bottom;
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

    private static float SampleAt(IReadOnlyList<float> samples, double absolutePosition)
    {
        if (samples.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Round(Math.Clamp(absolutePosition, 0, 1) * (samples.Count - 1));
        return samples[index];
    }

    private double ToMilliseconds(double absolutePosition)
    {
        var totalSamples = Math.Max(outputSamples.Length, inputSamples.Length);
        return sampleRate > 0 ? absolutePosition * totalSamples * 1000.0 / sampleRate : 0;
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
