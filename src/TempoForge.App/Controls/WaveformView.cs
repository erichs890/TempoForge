using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using TempoForge.Core.Looping;
using TempoForge.Core.Waveform;

namespace TempoForge.App.Controls;

/// <summary>Desenha a forma de onda com a parte já tocada destacada; clique ou arraste para buscar.</summary>
public sealed class WaveformView : FrameworkElement
{
    public static readonly DependencyProperty PeaksProperty = DependencyProperty.Register(
        nameof(Peaks), typeof(PeakData), typeof(WaveformView),
        new FrameworkPropertyMetadata(null, (d, _) => ((WaveformView)d).InvalidateWave()));

    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
        nameof(Position), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
        nameof(Duration), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty WaveBrushProperty = DependencyProperty.Register(
        nameof(WaveBrush), typeof(Brush), typeof(WaveformView),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PlayedBrushProperty = DependencyProperty.Register(
        nameof(PlayedBrush), typeof(Brush), typeof(WaveformView),
        new FrameworkPropertyMetadata(Brushes.Orange, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background), typeof(Brush), typeof(WaveformView),
        new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LoopAProperty = DependencyProperty.Register(
        nameof(LoopA), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LoopBProperty = DependencyProperty.Register(
        nameof(LoopB), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsLoopingProperty = DependencyProperty.Register(
        nameof(IsLooping), typeof(bool), typeof(WaveformView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LoopBrushProperty = DependencyProperty.Register(
        nameof(LoopBrush), typeof(Brush), typeof(WaveformView),
        new FrameworkPropertyMetadata(Brushes.DeepSkyBlue, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MarkerBrushProperty = DependencyProperty.Register(
        nameof(MarkerBrush), typeof(Brush), typeof(WaveformView),
        new FrameworkPropertyMetadata(Brushes.Gold, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MarkersProperty = DependencyProperty.Register(
        nameof(Markers), typeof(IEnumerable<Marker>), typeof(WaveformView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnMarkersChanged));

    private const double HandleGrabPixels = 7;
    private enum DragMode { None, Seek, LoopA, LoopB }
    private DragMode _drag;

    public double LoopA { get => (double)GetValue(LoopAProperty); set => SetValue(LoopAProperty, value); }
    public double LoopB { get => (double)GetValue(LoopBProperty); set => SetValue(LoopBProperty, value); }
    public bool IsLooping { get => (bool)GetValue(IsLoopingProperty); set => SetValue(IsLoopingProperty, value); }
    public Brush LoopBrush { get => (Brush)GetValue(LoopBrushProperty); set => SetValue(LoopBrushProperty, value); }
    public Brush MarkerBrush { get => (Brush)GetValue(MarkerBrushProperty); set => SetValue(MarkerBrushProperty, value); }
    public IEnumerable<Marker>? Markers { get => (IEnumerable<Marker>?)GetValue(MarkersProperty); set => SetValue(MarkersProperty, value); }

    /// <summary>O usuário arrastou o ponto A (true) ou B (false) para um novo tempo, em segundos.</summary>
    public event Action<bool, double>? LoopPointMoved;

    private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (WaveformView)d;
        if (e.OldValue is INotifyCollectionChanged oldList) oldList.CollectionChanged -= view.OnMarkerListChanged;
        if (e.NewValue is INotifyCollectionChanged newList) newList.CollectionChanged += view.OnMarkerListChanged;
    }

    private void OnMarkerListChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    private double TimeToX(double seconds) => Duration > 0 ? seconds / Duration * ActualWidth : 0;

    private void DrawOverlays(DrawingContext dc, double w, double h)
    {
        if (Duration <= 0) return;

        if (!double.IsNaN(LoopA))
        {
            double ax = TimeToX(LoopA);
            Brush line = LoopBrush;
            if (!double.IsNaN(LoopB))
            {
                double bx = TimeToX(LoopB);
                var fill = LoopBrush.Clone();
                fill.Opacity = IsLooping ? 0.22 : 0.08;
                fill.Freeze();
                dc.DrawRectangle(fill, null, new Rect(ax, 0, Math.Max(0, bx - ax), h));
                DrawHandle(dc, line, bx, h, "B");
            }
            DrawHandle(dc, line, ax, h, "A");
        }

        if (Markers != null)
        {
            foreach (Marker marker in Markers)
            {
                double x = Math.Round(TimeToX(marker.Seconds));
                dc.DrawRectangle(MarkerBrush, null, new Rect(x - 0.5, 0, 1, h));
                var flag = new StreamGeometry();
                using (StreamGeometryContext ctx = flag.Open())
                {
                    ctx.BeginFigure(new Point(x - 5, 0), true, true);
                    ctx.LineTo(new Point(x + 5, 0), false, false);
                    ctx.LineTo(new Point(x, 7), false, false);
                }
                flag.Freeze();
                dc.DrawGeometry(MarkerBrush, null, flag);
            }
        }
    }

    private void DrawHandle(DrawingContext dc, Brush brush, double x, double h, string label)
    {
        x = Math.Round(x);
        dc.DrawRectangle(brush, null, new Rect(x - 1, 0, 2, h));
        dc.DrawRoundedRectangle(brush, null, new Rect(x - 8, h - 16, 16, 16), 3, 3);
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            10, Brushes.Black, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(text, new Point(x - text.Width / 2, h - 15));
    }

    private Geometry? _wave;
    private int _waveFilled = -1;
    private double _waveWidth = -1, _waveHeight = -1;

    public PeakData? Peaks { get => (PeakData?)GetValue(PeaksProperty); set => SetValue(PeaksProperty, value); }
    public double Position { get => (double)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public double Duration { get => (double)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public Brush WaveBrush { get => (Brush)GetValue(WaveBrushProperty); set => SetValue(WaveBrushProperty, value); }
    public Brush PlayedBrush { get => (Brush)GetValue(PlayedBrushProperty); set => SetValue(PlayedBrushProperty, value); }
    public Brush Background { get => (Brush)GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>Pedido de busca, em segundos.</summary>
    public event Action<double>? SeekRequested;

    private void InvalidateWave()
    {
        _wave = null;
        _waveFilled = -1;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 2 || h < 2) return;

        dc.DrawRectangle(Background, null, new Rect(0, 0, w, h));

        PeakData? peaks = Peaks;
        if (peaks != null)
        {
            int filled = peaks.Filled;
            if (_wave == null || filled != _waveFilled || w != _waveWidth || h != _waveHeight)
            {
                _wave = BuildGeometry(peaks, filled, w, h);
                _waveFilled = filled;
                _waveWidth = w;
                _waveHeight = h;
            }

            double playedX = Duration > 0 ? Math.Clamp(Position / Duration, 0, 1) * w : 0;

            dc.PushClip(new RectangleGeometry(new Rect(playedX, 0, Math.Max(0, w - playedX), h)));
            dc.DrawGeometry(WaveBrush, null, _wave);
            dc.Pop();

            dc.PushClip(new RectangleGeometry(new Rect(0, 0, playedX, h)));
            dc.DrawGeometry(PlayedBrush, null, _wave);
            dc.Pop();

            DrawOverlays(dc, w, h);

            if (Duration > 0)
                dc.DrawRectangle(Brushes.White, null, new Rect(Math.Round(playedX) - 0.5, 0, 1.5, h));
        }
    }

    private static Geometry BuildGeometry(PeakData peaks, int filled, double w, double h)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            double mid = h / 2, scale = h / 2 * 0.95;
            int columns = (int)w;
            double bucketsPerColumn = peaks.Buckets / (double)columns;

            for (int x = 0; x < columns; x++)
            {
                int from = (int)(x * bucketsPerColumn);
                int to = Math.Max(from + 1, (int)((x + 1) * bucketsPerColumn));
                if (from >= filled) break;
                to = Math.Min(to, filled);

                float min = 0, max = 0;
                for (int b = from; b < to; b++)
                {
                    if (peaks.Min[b] < min) min = peaks.Min[b];
                    if (peaks.Max[b] > max) max = peaks.Max[b];
                }

                double top = mid - Math.Min(1f, max) * scale;
                double bottom = mid - Math.Max(-1f, min) * scale;
                if (bottom - top < 1) { top = mid - 0.5; bottom = mid + 0.5; }

                ctx.BeginFigure(new Point(x, top), true, true);
                ctx.LineTo(new Point(x + 0.8, top), false, false);
                ctx.LineTo(new Point(x + 0.8, bottom), false, false);
                ctx.LineTo(new Point(x, bottom), false, false);
            }
        }
        geometry.Freeze();
        return geometry;
    }

    /// <summary>Redesenha se a varredura de fundo avançou desde o último quadro.</summary>
    public void RefreshIfGrowing()
    {
        PeakData? peaks = Peaks;
        if (peaks != null && peaks.Filled != _waveFilled) InvalidateVisual();
    }

    private DragMode HitTest(double x)
    {
        if (Duration <= 0) return DragMode.None;
        // B tem prioridade quando os dois estão muito próximos, para poder afastá-lo.
        if (!double.IsNaN(LoopB) && Math.Abs(x - TimeToX(LoopB)) <= HandleGrabPixels) return DragMode.LoopB;
        if (!double.IsNaN(LoopA) && Math.Abs(x - TimeToX(LoopA)) <= HandleGrabPixels) return DragMode.LoopA;
        return DragMode.Seek;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        double x = e.GetPosition(this).X;
        _drag = HitTest(x);
        if (_drag == DragMode.None) return;
        CaptureMouse();
        if (_drag == DragMode.Seek) RequestSeek(x);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        double x = e.GetPosition(this).X;
        if (!IsMouseCaptured)
        {
            Cursor = HitTest(x) is DragMode.LoopA or DragMode.LoopB ? Cursors.SizeWE : Cursors.IBeam;
            return;
        }

        if (_drag == DragMode.Seek) RequestSeek(x);
        else if (_drag is DragMode.LoopA or DragMode.LoopB && ActualWidth > 0)
            LoopPointMoved?.Invoke(_drag == DragMode.LoopA, Math.Clamp(x / ActualWidth, 0, 1) * Duration);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _drag = DragMode.None;
        ReleaseMouseCapture();
    }

    private void RequestSeek(double x)
    {
        if (Duration <= 0 || ActualWidth <= 0) return;
        SeekRequested?.Invoke(Math.Clamp(x / ActualWidth, 0, 1) * Duration);
    }
}
