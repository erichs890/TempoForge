using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TempoForge.Core.Engine;
using TempoForge.Core.Waveform;

namespace TempoForge.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const int PeakBuckets = 4096;

    private readonly AudioEngine _engine = new();
    private CancellationTokenSource? _peakCancel;

    [ObservableProperty] private string _title = "Nenhum arquivo aberto";
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private double _position;
    [ObservableProperty] private double _duration;
    [ObservableProperty] private string _positionText = "0:00";
    [ObservableProperty] private string _durationText = "0:00";
    [ObservableProperty] private PeakData? _peaks;

    public const double MinSpeed = 0.25, MaxSpeed = 4.0, SpeedStep = 0.05;

    /// <summary>Velocidade em múltiplos do original (1,00 = normal).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedText), nameof(SpeedSlider))]
    private double _speed = 1.0;

    public string SpeedText => $"{Speed:0.00}×";

    /// <summary>Posição do controle deslizante: escala logarítmica, 0 = velocidade normal.</summary>
    public double SpeedSlider
    {
        get => Math.Log2(Speed);
        set => Speed = Math.Round(Math.Pow(2, value), 2);
    }

    partial void OnSpeedChanged(double value)
    {
        double clamped = Math.Clamp(value, MinSpeed, MaxSpeed);
        if (clamped != value)
        {
            Speed = clamped;
            return;
        }
        _engine.Tempo = value;
    }

    [RelayCommand]
    private void SpeedUp() => Speed = Math.Round(Speed + SpeedStep, 2);

    [RelayCommand]
    private void SpeedDown() => Speed = Math.Round(Speed - SpeedStep, 2);

    [RelayCommand]
    private void SpeedReset() => Speed = 1.0;

    public const int MaxSemitones = 12, MaxCents = 50;

    /// <summary>Transposição em semitons inteiros (−12 a +12).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PitchText))]
    private int _semitones;

    /// <summary>Afinação fina em cents (−50 a +50).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PitchText))]
    private int _cents;

    public string PitchText
    {
        get
        {
            string st = Semitones > 0 ? $"+{Semitones}" : Semitones.ToString();
            return Cents == 0 ? $"{st} st" : $"{st} st {Cents:+0;−0} c";
        }
    }

    partial void OnSemitonesChanged(int value)
    {
        int clamped = Math.Clamp(value, -MaxSemitones, MaxSemitones);
        if (clamped != value) Semitones = clamped;
        else ApplyPitch();
    }

    partial void OnCentsChanged(int value)
    {
        int clamped = Math.Clamp(value, -MaxCents, MaxCents);
        if (clamped != value) Cents = clamped;
        else ApplyPitch();
    }

    private void ApplyPitch() => _engine.PitchSemitones = Semitones + Cents / 100.0;

    [RelayCommand]
    private void PitchUp() => Semitones++;

    [RelayCommand]
    private void PitchDown() => Semitones--;

    [RelayCommand]
    private void PitchReset()
    {
        Semitones = 0;
        Cents = 0;
    }

    /// <summary>Abre um arquivo. Lança exceção com mensagem legível se o formato não puder ser lido.</summary>
    public void OpenFile(string path)
    {
        _peakCancel?.Cancel();
        _engine.Open(path);

        Title = Path.GetFileNameWithoutExtension(path);
        Duration = _engine.DurationSeconds;
        DurationText = FormatTime(Duration);
        Position = 0;
        IsLoaded = true;

        _peakCancel = new CancellationTokenSource();
        Peaks = PeakData.Build(path, PeakBuckets, _peakCancel.Token, out _);

        _engine.Play();
        IsPlaying = true;
    }

    [RelayCommand]
    private void PlayPause()
    {
        if (!_engine.IsLoaded) return;
        if (_engine.IsPlaying) _engine.Pause();
        else _engine.Play();
        IsPlaying = _engine.IsPlaying;
    }

    public void Seek(double seconds)
    {
        if (!_engine.IsLoaded) return;
        _engine.Seek(seconds);
        Position = _engine.PositionSeconds;
        PositionText = FormatTime(Position);
    }

    public void SeekRelative(double deltaSeconds) => Seek(_engine.PositionSeconds + deltaSeconds);

    /// <summary>Chamado ~30 vezes por segundo pela janela para refletir o estado do motor.</summary>
    public void Tick()
    {
        if (!_engine.IsLoaded) return;
        Position = _engine.PositionSeconds;
        PositionText = FormatTime(Position);
        IsPlaying = _engine.IsPlaying;
    }

    public static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        var t = TimeSpan.FromSeconds(seconds);
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes}:{t.Seconds:00}";
    }

    public void Dispose()
    {
        _peakCancel?.Cancel();
        _engine.Dispose();
    }
}
