using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, e) => ((WaveformView)d).FollowPosition((double)e.OldValue, (double)e.NewValue)));

    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(
        nameof(Duration), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((WaveformView)d).ResetZoom()));

    public static readonly DependencyProperty WaveBrushProperty = DependencyProperty.Register(
        nameof(WaveBrush), typeof(Brush), typeof(WaveformView),
        new FrameworkPropertyMetadata(Brushes.Gray, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PlayedBrushProperty = DependencyProperty.Register(
        nameof(PlayedBrush), typeof(Brush), typeof(WaveformView),
        new FrameworkPropertyMetadata(Brushes.Orange, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
        nameof(Background), typeof(Brush), typeof(WaveformView),
        new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PlayheadBrushProperty = DependencyProperty.Register(
        nameof(PlayheadBrush), typeof(Brush), typeof(WaveformView),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush PlayheadBrush { get => (Brush)GetValue(PlayheadBrushProperty); set => SetValue(PlayheadBrushProperty, value); }

    public static readonly DependencyProperty HandleTextBrushProperty = DependencyProperty.Register(
        nameof(HandleTextBrush), typeof(Brush), typeof(WaveformView),
        new FrameworkPropertyMetadata(Brushes.Black, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush HandleTextBrush { get => (Brush)GetValue(HandleTextBrushProperty); set => SetValue(HandleTextBrushProperty, value); }

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

    // ---- Zoom ----
    // A janela visível vai de _viewStart a _viewStart + VisibleSpan, em segundos do arquivo.
    private const double MinVisibleSeconds = 1.0;
    private const double ZoomStep = 1.3;
    private double _zoom = 1.0;
    private double _viewStart;

    /// <summary>Fator de ampliação atual (1 = a música inteira).</summary>
    public double Zoom => _zoom;

    /// <summary>O zoom ou o trecho visível mudou.</summary>
    public event Action? ViewChanged;

    private double MaxZoom => Duration > MinVisibleSeconds ? Duration / MinVisibleSeconds : 1.0;
    private double VisibleSpan => Duration / _zoom;

    private double TimeToX(double seconds) =>
        Duration > 0 ? (seconds - _viewStart) / VisibleSpan * ActualWidth : 0;

    private double XToTime(double x) =>
        Duration > 0 && ActualWidth > 0
            ? Math.Clamp(_viewStart + Math.Clamp(x / ActualWidth, 0, 1) * VisibleSpan, 0, Duration)
            : 0;

    private void SetView(double zoom, double viewStart)
    {
        zoom = Math.Clamp(zoom, 1.0, MaxZoom);
        double span = Duration / zoom;
        viewStart = Math.Clamp(viewStart, 0, Math.Max(0, Duration - span));
        if (zoom == _zoom && viewStart == _viewStart) return;
        _zoom = zoom;
        _viewStart = viewStart;
        InvalidateWave();
        ViewChanged?.Invoke();
    }

    /// <summary>Amplia ou reduz mantendo fixo na tela o instante <paramref name="anchorSeconds"/>.</summary>
    private void ZoomAround(double factor, double anchorSeconds)
    {
        if (Duration <= 0) return;
        double fraction = (anchorSeconds - _viewStart) / VisibleSpan;
        double zoom = Math.Clamp(_zoom * factor, 1.0, MaxZoom);
        SetView(zoom, anchorSeconds - fraction * (Duration / zoom));
    }

    /// <summary>O ponto fixo dos botões de zoom é o cursor de reprodução, se visível; senão o centro.</summary>
    private double ButtonAnchor =>
        Position >= _viewStart && Position <= _viewStart + VisibleSpan ? Position : _viewStart + VisibleSpan / 2;

    public void ZoomIn() => ZoomAround(ZoomStep * ZoomStep, ButtonAnchor);
    public void ZoomOut() => ZoomAround(1 / (ZoomStep * ZoomStep), ButtonAnchor);
    public void ResetZoom() => SetView(1.0, 0);

    /// <summary>Amplia para que o trecho dado ocupe a tela, com uma margem.</summary>
    public void ZoomToRange(double startSeconds, double endSeconds)
    {
        if (Duration <= 0 || !(endSeconds > startSeconds)) return;
        double margin = (endSeconds - startSeconds) * 0.15;
        double span = Math.Max(MinVisibleSeconds, endSeconds - startSeconds + 2 * margin);
        SetView(Duration / span, startSeconds - margin);
    }

    /// <summary>
    /// Com zoom, a janela acompanha a reprodução por páginas: quando o cursor sai pela borda
    /// (ou salta por causa de uma busca), a janela pula para deixá-lo perto do início.
    /// Se o usuário deslocou a janela para longe do cursor, ela fica onde está.
    /// </summary>
    private void FollowPosition(double previous, double current)
    {
        if (_zoom <= 1.0 || _drag != DragMode.None) return;
        double end = _viewStart + VisibleSpan;
        bool wasVisible = previous >= _viewStart && previous <= end;
        bool isVisible = current >= _viewStart && current <= end;
        bool jumped = Math.Abs(current - previous) > 1.0;
        if (!isVisible && (wasVisible || jumped)) SetView(_zoom, current - VisibleSpan * 0.1);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        if (Duration <= 0) return;
        double notches = e.Delta / 120.0;
        if (Keyboard.Modifiers == ModifierKeys.Shift)
            SetView(_zoom, _viewStart - notches * VisibleSpan * 0.15); // desloca
        else
            ZoomAround(Math.Pow(ZoomStep, notches), XToTime(e.GetPosition(this).X));
        e.Handled = true;
    }

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
                double left = Math.Max(0, ax), right = Math.Min(w, bx);
                if (right > left) dc.DrawRectangle(fill, null, new Rect(left, 0, right - left, h));
                DrawHandle(dc, line, bx, h, "B");
            }
            DrawHandle(dc, line, ax, h, "A");
        }

        if (Markers != null)
        {
            foreach (Marker marker in Markers)
            {
                double x = Math.Round(TimeToX(marker.Seconds));
                if (x < -6 || x > w + 6) continue;
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
        if (x < -9 || x > ActualWidth + 9) return;
        dc.DrawRectangle(brush, null, new Rect(x - 1, 0, 2, h));
        dc.DrawRoundedRectangle(brush, null, new Rect(x - 8, h - 16, 16, 16), 3, 3);
        var text = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
            10, HandleTextBrush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(text, new Point(x - text.Width / 2, h - 15));
    }

    // A onda é rasterizada uma única vez em dois bitmaps (não tocado / tocado); cada quadro
    // só desenha as duas imagens recortadas, sem refazer a geometria de milhares de colunas.
    private ImageSource? _waveImage, _playedImage;
    private Brush? _waveImageBrush, _playedImageBrush;
    private int _waveFilled = -1;
    private double _waveWidth = -1, _waveHeight = -1, _waveDpi = -1;
    private long _lastGrowRefresh;

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
        _waveImage = _playedImage = null;
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
            double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            if (_waveImage == null || filled != _waveFilled || w != _waveWidth || h != _waveHeight
                || dpi != _waveDpi || !ReferenceEquals(WaveBrush, _waveImageBrush)
                || !ReferenceEquals(PlayedBrush, _playedImageBrush))
            {
                Geometry wave = BuildGeometry(peaks, filled, w, h, _zoom, Duration > 0 ? _viewStart / Duration : 0);
                _waveImage = Rasterize(wave, WaveBrush, w, h, dpi);
                _playedImage = Rasterize(wave, PlayedBrush, w, h, dpi);
                _waveImageBrush = WaveBrush;
                _playedImageBrush = PlayedBrush;
                _waveFilled = filled;
                _waveWidth = w;
                _waveHeight = h;
                _waveDpi = dpi;
            }

            double playedX = Duration > 0 ? Math.Clamp(TimeToX(Position), 0, w) : 0;
            var bounds = new Rect(0, 0, w, h);

            dc.PushClip(new RectangleGeometry(new Rect(playedX, 0, Math.Max(0, w - playedX), h)));
            dc.DrawImage(_waveImage, bounds);
            dc.Pop();

            dc.PushClip(new RectangleGeometry(new Rect(0, 0, playedX, h)));
            dc.DrawImage(_playedImage, bounds);
            dc.Pop();

            DrawOverlays(dc, w, h);

            if (Duration > 0 && Position >= _viewStart && Position <= _viewStart + VisibleSpan)
                dc.DrawRectangle(PlayheadBrush, null, new Rect(Math.Round(playedX) - 0.5, 0, 1.5, h));

            if (Duration > 0 && _zoom > 1.0)
            {
                // Faixa-guia: mostra que parte da música está na tela e onde está o cursor.
                dc.DrawRectangle(WaveBrush, null, new Rect(0, h - 3, w, 3));
                dc.DrawRectangle(PlayedBrush, null,
                    new Rect(_viewStart / Duration * w, h - 3, Math.Max(3, w / _zoom), 3));
                dc.DrawRectangle(PlayheadBrush, null,
                    new Rect(Math.Clamp(Position / Duration, 0, 1) * (w - 1.5), h - 4, 1.5, 4));
            }
        }
    }

    private static ImageSource Rasterize(Geometry wave, Brush brush, double w, double h, double dpi)
    {
        var visual = new DrawingVisual();
        using (DrawingContext dc = visual.RenderOpen()) dc.DrawGeometry(brush, null, wave);
        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(w * dpi)), Math.Max(1, (int)Math.Ceiling(h * dpi)),
            96 * dpi, 96 * dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static Geometry BuildGeometry(PeakData peaks, int filled, double w, double h, double zoom, double startFraction)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            double mid = h / 2, scale = h / 2 * 0.95;
            int columns = (int)w;
            double bucketsPerColumn = peaks.Buckets / zoom / columns;
            double firstBucket = startFraction * peaks.Buckets;

            for (int x = 0; x < columns; x++)
            {
                int from = (int)(firstBucket + x * bucketsPerColumn);
                int to = Math.Max(from + 1, (int)(firstBucket + (x + 1) * bucketsPerColumn));
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
                // Com muito zoom um mesmo trecho ocupa várias colunas: fecha as frestas entre elas.
                double right = x + (bucketsPerColumn < 1 ? 1.0 : 0.8);
                ctx.LineTo(new Point(right, top), false, false);
                ctx.LineTo(new Point(right, bottom), false, false);
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
        if (peaks == null || peaks.Filled == _waveFilled) return;
        // Durante a varredura basta atualizar algumas vezes por segundo (e uma última vez ao terminar).
        long now = Environment.TickCount64;
        if (peaks.Filled < peaks.Buckets && now - _lastGrowRefresh < 200) return;
        _lastGrowRefresh = now;
        InvalidateVisual();
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
            LoopPointMoved?.Invoke(_drag == DragMode.LoopA, XToTime(x));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        _drag = DragMode.None;
        ReleaseMouseCapture();
    }

    private void RequestSeek(double x)
    {
        if (Duration <= 0 || ActualWidth <= 0) return;
        SeekRequested?.Invoke(XToTime(x));
    }
}
