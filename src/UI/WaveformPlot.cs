using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;

namespace EffectsLatencyTester;

public sealed class WaveformViewportChangedEventArgs(double offset, double span) : EventArgs
{
    public double Offset { get; } = offset;
    public double Span { get; } = span;
}

public sealed class WaveformPlot : Control
{
    private const double PlotLeft = 52;
    private const double PlotTop = 36;
    private const double PlotRight = 16;
    private const double PlotBottom = 36;
    private const double PanelGap = 10;
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
    private ThemePalette palette = ThemePalette.Dark;

    public WaveformPlot()
    {
        ClipToBounds = true;
        IsHitTestVisible = true;
        ApplyTheme(ThemeManager.CurrentPalette);
        ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;
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

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.PointerUpdateKind == PointerUpdateKind.LeftButtonPressed)
        {
            var position = point.Position;
            if (IsInsideAnyPanel(position))
            {
                isPanning = true;
                panMoved = false;
                panStartX = position.X;
                panStartOffset = viewOffset;
                e.Pointer.Capture(this);
                e.Handled = true;
            }
        }

        base.OnPointerPressed(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (isPanning)
        {
            var position = e.GetPosition(this);
            var wasClick = !panMoved && IsInsideAnyPanel(position);
            isPanning = false;
            e.Pointer.Capture(null);
            if (wasClick)
            {
                RecordTimeClick(position.X);
            }

            e.Handled = true;
        }

        base.OnPointerReleased(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
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
        base.OnPointerMoved(e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        if (!isPanning)
        {
            cursorX = null;
            InvalidateVisual();
        }

        base.OnPointerExited(e);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var hasSamples = inputSamples.Length > 0 || outputSamples.Length > 0;
        if (!hasSamples)
        {
            base.OnPointerWheelChanged(e);
            return;
        }

        var panels = GetPanels();
        var data = panels[0].Data;
        var point = e.GetPosition(this);
        if (point.X < data.Left || point.X > data.Right || !IsInsideAnyPanel(point))
        {
            base.OnPointerWheelChanged(e);
            return;
        }

        var direction = e.Delta.Y > 0 ? -1 : 1;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var anchor = Math.Clamp((point.X - data.Left) / data.Width, 0, 1);
            var factor = e.Delta.Y > 0 ? 0.8 : 1.25;
            var newSpan = Math.Clamp(viewSpan * factor, MinimumViewportSpan, 1);
            var absoluteAnchor = viewOffset + anchor * viewSpan;
            SetViewport(absoluteAnchor - anchor * newSpan, newSpan, raiseEvent: true);
        }
        else
        {
            SetViewport(viewOffset + direction * viewSpan * 0.25, viewSpan, raiseEvent: true);
        }

        cursorX = point.X;
        InvalidateVisual();
        e.Handled = true;
        base.OnPointerWheelChanged(e);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = new Rect(Bounds.Size);
        var background = new SolidColorBrush(palette.PlotBackground);
        context.DrawRectangle(background, null, bounds);

        if (outputSamples.Length == 0 && inputSamples.Length == 0)
        {
            DrawText(context, I18n.WaveformEmpty, new Point(18, 18), 13, palette.PlotLabel);
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

        DrawText(context, header, new Point(PlotLeft, 8), 12, palette.PlotLabel);
        DrawPanel(context, panels[0], I18n.InputWaveform, inputSamples, palette.PlotInput, totalSamples, false);
        DrawPanel(context, panels[1], I18n.OutputWaveform, outputSamples, palette.PlotOutput, totalSamples, false);
        DrawPanel(context, panels[2], I18n.CombinedWaveform, [], palette.PlotOutput, totalSamples, true);
        DrawWaveform(context, panels[2].Data, inputSamples, totalSamples, palette.PlotInput);
        DrawWaveform(context, panels[2].Data, outputSamples, totalSamples, palette.PlotOutput);
        DrawTimeMarker(context, panels, totalSamples, time1, I18n.Time1Short, palette.PlotTime1);
        DrawTimeMarker(context, panels, totalSamples, time2, I18n.Time2Short, palette.PlotTime2);
        DrawCursor(context, panels, totalSamples);
    }

    private void ThemeManager_ThemeChanged(object? sender, ThemePalette nextPalette)
    {
        ApplyTheme(nextPalette);
        InvalidateVisual();
    }

    private void ApplyTheme(ThemePalette nextPalette)
    {
        palette = nextPalette;
    }

    private void DrawPanel(
        DrawingContext context,
        PlotPanel panel,
        string title,
        IReadOnlyList<float> samples,
        Color waveformColor,
        int totalSamples,
        bool showTimeAxis)
    {
        context.DrawRectangle(null, new Pen(new SolidColorBrush(palette.PlotBorder), 1), panel.Panel);
        DrawText(context, title, new Point(panel.Panel.Left + 6, panel.Panel.Top + 3), 11, palette.PlotLabel);
        DrawGrid(context, panel.Data, showTimeAxis, totalSamples);
        if (samples.Count > 0)
        {
            DrawWaveform(context, panel.Data, samples, totalSamples, waveformColor);
        }
    }

    private void DrawGrid(DrawingContext context, Rect data, bool showTimeAxis, int totalSamples)
    {
        var gridPen = new Pen(new SolidColorBrush(palette.PlotGrid), 1);
        var axisPen = new Pen(new SolidColorBrush(palette.PlotAxis), 1);
        for (var index = 0; index <= 4; index++)
        {
            var y = data.Top + data.Height * index / 4;
            context.DrawLine(index == 2 ? axisPen : gridPen, new Point(data.Left, y), new Point(data.Right, y));
            var value = 1.0 - index * 0.5;
            DrawText(context, value.ToString("0.0", CultureInfo.InvariantCulture), new Point(4, y - 8), 9, palette.PlotMutedLabel);
        }

        const int timeTicks = 5;
        var totalSeconds = sampleRate > 0 ? totalSamples / (double)sampleRate : 0;
        for (var index = 0; index <= timeTicks; index++)
        {
            var relative = index / (double)timeTicks;
            var x = data.Left + data.Width * relative;
            context.DrawLine(gridPen, new Point(x, data.Top), new Point(x, data.Bottom));
            if (showTimeAxis)
            {
                var seconds = (viewOffset + viewSpan * relative) * totalSeconds;
                DrawText(context, I18n.Format(nameof(I18n.TimeSeconds), seconds), new Point(x - 14, data.Bottom + 6), 9, palette.PlotMutedLabel);
            }
        }
    }

    private void DrawWaveform(DrawingContext context, Rect data, IReadOnlyList<float> samples, int totalSamples, Color color)
    {
        if (samples.Count == 0 || totalSamples <= 0 || data.Width < 2 || data.Height < 2)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var geometryContext = geometry.Open())
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
                    geometryContext.BeginFigure(new Point(x, y), isFilled: false);
                }
                else
                {
                    geometryContext.LineTo(new Point(x, y));
                }
            }
        }

        context.DrawGeometry(null, new Pen(new SolidColorBrush(color), 1.5), geometry);
    }

    private void DrawCursor(DrawingContext context, IReadOnlyList<PlotPanel> panels, int totalSamples)
    {
        if (cursorX is null || totalSamples <= 0 || sampleRate <= 0)
        {
            return;
        }

        var pen = new Pen(new SolidColorBrush(palette.PlotCursor), 1);
        foreach (var panel in panels)
        {
            context.DrawLine(pen, new Point(cursorX.Value, panel.Data.Top), new Point(cursorX.Value, panel.Data.Bottom));
        }

        var normalized = Math.Clamp((cursorX.Value - panels[0].Data.Left) / panels[0].Data.Width, 0, 1);
        var absolutePosition = viewOffset + normalized * viewSpan;
        var time = absolutePosition * totalSamples / sampleRate;
        var input = SampleAt(inputSamples, absolutePosition);
        var output = SampleAt(outputSamples, absolutePosition);
        var label = I18n.Format(nameof(I18n.Cursor), time, input, output);
        var labelX = cursorX.Value + 8;
        if (labelX > Bounds.Width - 230)
        {
            labelX = Math.Max(PlotLeft, cursorX.Value - 230);
        }

        DrawText(context, label, new Point(labelX, 22), 10, palette.PlotLabel);
    }

    private void DrawTimeMarker(DrawingContext context, IReadOnlyList<PlotPanel> panels, int totalSamples, double? marker, string label, Color color)
    {
        if (marker is null || totalSamples <= 0 || marker < viewOffset || marker > viewOffset + viewSpan)
        {
            return;
        }

        var x = panels[0].Data.Left + (marker.Value - viewOffset) / viewSpan * panels[0].Data.Width;
        var pen = new Pen(new SolidColorBrush(color), 1.25);
        foreach (var panel in panels)
        {
            context.DrawLine(pen, new Point(x, panel.Data.Top), new Point(x, panel.Data.Bottom));
        }

        DrawText(context, label, new Point(x + 3, panels[0].Panel.Top + 3), 10, color);
    }

    private void RecordTimeClick(double x)
    {
        var totalSamples = Math.Max(outputSamples.Length, inputSamples.Length);
        if (sampleRate <= 0 || totalSamples == 0)
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
        var width = Math.Max(1, Bounds.Width - PlotLeft - PlotRight);
        var availableHeight = Math.Max(3, Bounds.Height - PlotTop - PlotBottom - PanelGap * 2);
        var panelHeight = Math.Max(1, availableHeight / 3);
        var panels = new PlotPanel[3];
        for (var index = 0; index < panels.Length; index++)
        {
            var panelTop = PlotTop + index * (panelHeight + PanelGap);
            var panel = new Rect(PlotLeft, panelTop, width, panelHeight);
            var axisSpace = index == panels.Length - 1 ? 24 : 7;
            var data = new Rect(panel.Left, panel.Top + 19, panel.Width, Math.Max(1, panel.Height - 19 - axisSpace));
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

    private void DrawText(DrawingContext context, string text, Point origin, double fontSize, Color color)
    {
        var brush = new SolidColorBrush(color);
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            new Typeface("Inter"),
            fontSize,
            brush);
        context.DrawText(formatted, origin);
    }

    private readonly record struct PlotPanel(Rect Panel, Rect Data);
}