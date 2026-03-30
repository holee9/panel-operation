using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FpdSimViewer.ViewModels;

namespace FpdSimViewer.Views.Drawing;

public sealed class ScopeRenderer : Canvas
{
    public static readonly double[] TimeScalesUs = [1.0, 2.0, 5.0, 10.0, 20.0, 50.0, 100.0, 200.0, 500.0, 1000.0, 2000.0, 5000.0, 10000.0];

    public static readonly DependencyProperty ChannelsProperty =
        DependencyProperty.Register(
            nameof(Channels),
            typeof(IEnumerable<ScopeChannelViewModel>),
            typeof(ScopeRenderer),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnChannelsChanged));

    public static readonly DependencyProperty TimeScaleMicrosecondsPerDivisionProperty =
        DependencyProperty.Register(
            nameof(TimeScaleMicrosecondsPerDivision),
            typeof(double),
            typeof(ScopeRenderer),
            new FrameworkPropertyMetadata(
                50.0,
                FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTimeScaleChanged));

    public static readonly DependencyProperty CursorATimeProperty =
        DependencyProperty.Register(
            nameof(CursorATime),
            typeof(double?),
            typeof(ScopeRenderer),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CursorBTimeProperty =
        DependencyProperty.Register(
            nameof(CursorBTime),
            typeof(double?),
            typeof(ScopeRenderer),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public ScopeRenderer()
    {
        Background = new SolidColorBrush(Color.FromRgb(17, 24, 39));
        ClipToBounds = true;
    }

    public IEnumerable<ScopeChannelViewModel>? Channels
    {
        get => (IEnumerable<ScopeChannelViewModel>?)GetValue(ChannelsProperty);
        set => SetValue(ChannelsProperty, value);
    }

    public double TimeScaleMicrosecondsPerDivision
    {
        get => (double)GetValue(TimeScaleMicrosecondsPerDivisionProperty);
        set => SetValue(TimeScaleMicrosecondsPerDivisionProperty, value);
    }

    public double? CursorATime
    {
        get => (double?)GetValue(CursorATimeProperty);
        set => SetValue(CursorATimeProperty, value);
    }

    public double? CursorBTime
    {
        get => (double?)GetValue(CursorBTimeProperty);
        set => SetValue(CursorBTimeProperty, value);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        var currentIndex = Array.FindIndex(TimeScalesUs, scale => Math.Abs(scale - TimeScaleMicrosecondsPerDivision) < 0.0001);
        if (currentIndex < 0)
        {
            currentIndex = Array.FindIndex(TimeScalesUs, scale => scale > TimeScaleMicrosecondsPerDivision);
            currentIndex = currentIndex < 0 ? TimeScalesUs.Length - 1 : currentIndex;
        }

        var nextIndex = e.Delta > 0
            ? Math.Max(0, currentIndex - 1)
            : Math.Min(TimeScalesUs.Length - 1, currentIndex + 1);
        TimeScaleMicrosecondsPerDivision = TimeScalesUs[nextIndex];
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        var channels = Channels?.Where(channel => channel.IsVisible).ToList() ?? [];
        if (channels.Count == 0)
        {
            return;
        }

        var plotRect = GetPlotRect();
        var position = e.GetPosition(this);
        if (!plotRect.Contains(position))
        {
            return;
        }

        var endTime = channels.Max(channel => channel.GetLatestTime());
        var visibleWindowUs = TimeScaleMicrosecondsPerDivision * 10.0;
        var startTime = Math.Max(0.0, endTime - visibleWindowUs);
        var cursorTime = startTime + ((position.X - plotRect.Left) / plotRect.Width) * visibleWindowUs;

        if (!CursorATime.HasValue || CursorBTime.HasValue)
        {
            CursorATime = cursorTime;
            CursorBTime = null;
        }
        else
        {
            CursorBTime = cursorTime;
        }

        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseRightButtonDown(e);
        CursorATime = null;
        CursorBTime = null;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        drawingContext.DrawRectangle(Background, null, bounds);

        var channels = Channels?.Where(channel => channel.IsVisible).ToList() ?? [];
        if (bounds.Width < 320 || bounds.Height < 160)
        {
            return;
        }

        var plotRect = GetPlotRect();

        DrawGrid(drawingContext, plotRect);
        DrawChannelLegend(drawingContext, channels, bounds);

        if (channels.Count == 0)
        {
            DrawText(drawingContext, "Enable at least one channel", Brushes.WhiteSmoke, new Point(plotRect.Left + 16, plotRect.Top + 16), 14, FontWeights.SemiBold);
            return;
        }

        var endTime = channels.Max(channel => channel.GetLatestTime());
        var visibleWindowUs = TimeScaleMicrosecondsPerDivision * 10.0;
        var startTime = Math.Max(0.0, endTime - visibleWindowUs);

        DrawXAxis(drawingContext, plotRect, startTime, visibleWindowUs);
        DrawTraces(drawingContext, channels, plotRect, startTime, endTime);
        DrawCursors(drawingContext, plotRect, startTime, visibleWindowUs);
    }

    private static void OnChannelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var renderer = (ScopeRenderer)d;
        renderer.DetachHandlers(e.OldValue as IEnumerable<ScopeChannelViewModel>);
        renderer.AttachHandlers(e.NewValue as IEnumerable<ScopeChannelViewModel>);
        renderer.InvalidateVisual();
    }

    private static void OnTimeScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ScopeRenderer)d).InvalidateVisual();
    }

    private void AttachHandlers(IEnumerable<ScopeChannelViewModel>? channels)
    {
        if (channels is INotifyCollectionChanged notifyCollectionChanged)
        {
            notifyCollectionChanged.CollectionChanged += OnChannelCollectionChanged;
        }

        if (channels is null)
        {
            return;
        }

        foreach (var channel in channels)
        {
            channel.PropertyChanged += OnChannelPropertyChanged;
        }
    }

    private void DetachHandlers(IEnumerable<ScopeChannelViewModel>? channels)
    {
        if (channels is INotifyCollectionChanged notifyCollectionChanged)
        {
            notifyCollectionChanged.CollectionChanged -= OnChannelCollectionChanged;
        }

        if (channels is null)
        {
            return;
        }

        foreach (var channel in channels)
        {
            channel.PropertyChanged -= OnChannelPropertyChanged;
        }
    }

    private void OnChannelCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ScopeChannelViewModel channel in e.OldItems)
            {
                channel.PropertyChanged -= OnChannelPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ScopeChannelViewModel channel in e.NewItems)
            {
                channel.PropertyChanged += OnChannelPropertyChanged;
            }
        }

        InvalidateVisual();
    }

    private void OnChannelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ScopeChannelViewModel.SampleRevision) or nameof(ScopeChannelViewModel.IsVisible))
        {
            InvalidateVisual();
        }
    }

    private Rect GetPlotRect()
    {
        var sidebarWidth = 210.0;
        return new Rect(sidebarWidth, 18, Math.Max(40.0, ActualWidth - sidebarWidth - 12), Math.Max(60.0, ActualHeight - 42));
    }

    private void DrawGrid(DrawingContext drawingContext, Rect plotRect)
    {
        var borderPen = new Pen(new SolidColorBrush(Color.FromRgb(88, 104, 124)), 1.0);
        drawingContext.DrawRectangle(null, borderPen, plotRect);

        var gridPen = new Pen(new SolidColorBrush(Color.FromArgb(90, 228, 232, 240)), 1.0)
        {
            DashStyle = new DashStyle([2.0, 4.0], 0.0),
        };

        for (var division = 1; division < 10; division++)
        {
            var x = plotRect.Left + (plotRect.Width * division / 10.0);
            drawingContext.DrawLine(gridPen, new Point(x, plotRect.Top), new Point(x, plotRect.Bottom));
        }

        for (var division = 1; division < 8; division++)
        {
            var y = plotRect.Top + (plotRect.Height * division / 8.0);
            drawingContext.DrawLine(gridPen, new Point(plotRect.Left, y), new Point(plotRect.Right, y));
        }
    }

    private void DrawChannelLegend(DrawingContext drawingContext, IReadOnlyList<ScopeChannelViewModel> channels, Rect bounds)
    {
        DrawText(drawingContext, "Signal Scope", Brushes.WhiteSmoke, new Point(14, 14), 16, FontWeights.SemiBold);
        DrawText(drawingContext, $"Scale {FormatTimeScale(TimeScaleMicrosecondsPerDivision)}", Brushes.Gainsboro, new Point(14, 36), 12, FontWeights.Medium);

        var y = 62.0;
        foreach (var channel in channels)
        {
            drawingContext.DrawRectangle(channel.Stroke, null, new Rect(14, y + 4, 10, 10));
            DrawText(drawingContext, channel.Name, Brushes.WhiteSmoke, new Point(30, y), 12, FontWeights.SemiBold);
            DrawText(drawingContext, $"{channel.MinVoltage:F1}..{channel.MaxVoltage:F1} V", Brushes.Gainsboro, new Point(30, y + 15), 11, FontWeights.Normal);
            DrawText(drawingContext, $"Now {channel.CurrentValueText}", Brushes.Gainsboro, new Point(30, y + 30), 11, FontWeights.Normal);
            DrawText(drawingContext, $"Freq {channel.FrequencyText}", Brushes.Gainsboro, new Point(30, y + 45), 11, FontWeights.Normal);
            DrawText(drawingContext, $"Pulse {channel.PulseWidthText}", Brushes.Gainsboro, new Point(30, y + 60), 11, FontWeights.Normal);
            var specBrush = channel.SpecResult switch
            {
                "PASS" => Brushes.LightGreen,
                "FAIL" => Brushes.OrangeRed,
                _ => Brushes.Gainsboro,
            };
            DrawText(drawingContext, $"Spec {channel.SpecResult}", specBrush, new Point(30, y + 75), 11, FontWeights.SemiBold);
            y += 100.0;
            if (y > bounds.Bottom - 70)
            {
                break;
            }
        }
    }

    private void DrawXAxis(DrawingContext drawingContext, Rect plotRect, double startTime, double visibleWindowUs)
    {
        for (var division = 0; division <= 10; division++)
        {
            var x = plotRect.Left + (plotRect.Width * division / 10.0);
            var labelTime = startTime + (visibleWindowUs * division / 10.0);
            DrawText(drawingContext, FormatTimeLabel(labelTime), Brushes.Gainsboro, new Point(x - 12, plotRect.Bottom + 4), 10, FontWeights.Normal);
        }

        DrawText(drawingContext, "Time", Brushes.Gainsboro, new Point(plotRect.Right - 32, plotRect.Top - 18), 11, FontWeights.Normal);
    }

    private void DrawTraces(DrawingContext drawingContext, IReadOnlyList<ScopeChannelViewModel> channels, Rect plotRect, double startTime, double endTime)
    {
        foreach (var channel in channels)
        {
            var samples = channel.GetSamples(startTime, endTime);
            if (samples.Count == 0)
            {
                continue;
            }

            var geometry = new StreamGeometry();
            using var context = geometry.Open();
            var first = true;
            foreach (var sample in samples)
            {
                var point = new Point(
                    plotRect.Left + ((sample.TimeUs - startTime) / Math.Max(0.001, endTime - startTime)) * plotRect.Width,
                    plotRect.Bottom - ((sample.Value - channel.MinVoltage) / Math.Max(0.001, channel.MaxVoltage - channel.MinVoltage)) * plotRect.Height);
                point.Y = Math.Clamp(point.Y, plotRect.Top, plotRect.Bottom);

                if (first)
                {
                    context.BeginFigure(point, false, false);
                    first = false;
                }
                else
                {
                    context.LineTo(point, true, false);
                }
            }

            geometry.Freeze();
            drawingContext.DrawGeometry(null, new Pen(channel.Stroke, 1.8), geometry);
        }
    }

    private void DrawCursors(DrawingContext drawingContext, Rect plotRect, double startTime, double visibleWindowUs)
    {
        DrawCursor(drawingContext, plotRect, startTime, visibleWindowUs, CursorATime, Brushes.Yellow, "A");
        DrawCursor(drawingContext, plotRect, startTime, visibleWindowUs, CursorBTime, Brushes.Cyan, "B");

        if (!CursorATime.HasValue || !CursorBTime.HasValue)
        {
            return;
        }

        var min = Math.Min(CursorATime.Value, CursorBTime.Value);
        var max = Math.Max(CursorATime.Value, CursorBTime.Value);
        var x = plotRect.Left + (((min + max) / 2.0 - startTime) / visibleWindowUs) * plotRect.Width;
        DrawText(
            drawingContext,
            $"ΔT = {FormatTimeSpan(max - min)}",
            Brushes.WhiteSmoke,
            new Point(Math.Clamp(x - 34.0, plotRect.Left + 4.0, plotRect.Right - 90.0), plotRect.Top + 4.0),
            11,
            FontWeights.SemiBold);
    }

    private void DrawCursor(DrawingContext drawingContext, Rect plotRect, double startTime, double visibleWindowUs, double? cursorTime, Brush brush, string label)
    {
        if (!cursorTime.HasValue)
        {
            return;
        }

        if (cursorTime.Value < startTime || cursorTime.Value > startTime + visibleWindowUs)
        {
            return;
        }

        var x = plotRect.Left + ((cursorTime.Value - startTime) / visibleWindowUs) * plotRect.Width;
        drawingContext.DrawLine(new Pen(brush, 1.3), new Point(x, plotRect.Top), new Point(x, plotRect.Bottom));
        DrawText(drawingContext, $"{label} {FormatTimeLabel(cursorTime.Value)}", brush, new Point(Math.Clamp(x - 14.0, plotRect.Left + 4.0, plotRect.Right - 60.0), plotRect.Top + 18.0), 10, FontWeights.SemiBold);
    }

    private void DrawText(DrawingContext drawingContext, string text, Brush brush, Point origin, double fontSize, FontWeight fontWeight)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, fontWeight, FontStretches.Normal),
            fontSize,
            brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        drawingContext.DrawText(formatted, origin);
    }

    private static string FormatTimeScale(double timeScaleUs)
    {
        return timeScaleUs >= 1000.0
            ? $"{timeScaleUs / 1000.0:F1} ms/div"
            : $"{timeScaleUs:F0} us/div";
    }

    private static string FormatTimeLabel(double timeUs)
    {
        return timeUs >= 1000.0
            ? $"{timeUs / 1000.0:F2} ms"
            : $"{timeUs:F0} us";
    }

    private static string FormatTimeSpan(double timeUs)
    {
        return timeUs >= 1000.0
            ? $"{timeUs / 1000.0:F2} ms"
            : $"{timeUs:F2} us";
    }
}
