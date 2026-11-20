using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        CaptureMouse();
        RequestSeek(e.GetPosition(this).X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (IsMouseCaptured) RequestSeek(e.GetPosition(this).X);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) => ReleaseMouseCapture();

    private void RequestSeek(double x)
    {
        if (Duration <= 0 || ActualWidth <= 0) return;
        SeekRequested?.Invoke(Math.Clamp(x / ActualWidth, 0, 1) * Duration);
    }
}
