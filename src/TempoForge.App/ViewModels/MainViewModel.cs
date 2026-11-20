using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TempoForge.Core.Engine;
using TempoForge.Core.Looping;
using TempoForge.Core.Waveform;

namespace TempoForge.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private const int PeakBuckets = 4096;

    private readonly AudioEngine _engine = new();
    private CancellationTokenSource? _peakCancel;

    public MainViewModel()
    {
        Equalizer = new EqualizerViewModel(_engine.Effects);
    }

    public EqualizerViewModel Equalizer { get; }

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

    // ---- Loop A-B ----

    /// <summary>Ponto A em segundos, ou NaN se não definido.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoopText))]
    private double _loopA = double.NaN;

    /// <summary>Ponto B em segundos, ou NaN se não definido.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoopText))]
    private double _loopB = double.NaN;

    /// <summary>Se o trecho A-B está sendo repetido agora.</summary>
    [ObservableProperty] private bool _isLooping;

    public string LoopText
    {
        get
        {
            if (double.IsNaN(LoopA)) return "Sem loop";
            if (double.IsNaN(LoopB)) return $"A {FormatTimePrecise(LoopA)}  —  B não definido";
            return $"A {FormatTimePrecise(LoopA)}  —  B {FormatTimePrecise(LoopB)}  ({LoopB - LoopA:0.00} s)";
        }
    }

    [RelayCommand]
    private void SetLoopA()
    {
        if (!_engine.IsLoaded) return;
        LoopA = _engine.PositionSeconds;
        if (!double.IsNaN(LoopB) && LoopB <= LoopA) LoopB = double.NaN;
        ApplyLoop(enable: !double.IsNaN(LoopB));
    }

    [RelayCommand]
    private void SetLoopB()
    {
        if (!_engine.IsLoaded) return;
        double position = _engine.PositionSeconds;
        if (double.IsNaN(LoopA)) LoopA = 0;
        if (position <= LoopA) return;
        LoopB = position;
        ApplyLoop(enable: true);
    }

    [RelayCommand]
    private void ToggleLoop()
    {
        if (double.IsNaN(LoopA) || double.IsNaN(LoopB)) return;
        ApplyLoop(enable: !IsLooping);
    }

    [RelayCommand]
    private void ClearLoop()
    {
        LoopA = double.NaN;
        LoopB = double.NaN;
        ApplyLoop(enable: false);
    }

    /// <summary>Move um dos pontos do loop (arraste na forma de onda ou ajuste fino).</summary>
    public void MoveLoopPoint(bool isA, double seconds)
    {
        if (!_engine.IsLoaded) return;
        seconds = Math.Clamp(seconds, 0, Duration);
        if (isA)
        {
            if (!double.IsNaN(LoopB)) seconds = Math.Min(seconds, LoopB - 0.05);
            LoopA = Math.Max(0, seconds);
        }
        else
        {
            if (double.IsNaN(LoopA)) return;
            LoopB = Math.Max(seconds, LoopA + 0.05);
        }
        ApplyLoop(enable: IsLooping);
    }

    private void ApplyLoop(bool enable)
    {
        if (enable && !double.IsNaN(LoopA) && !double.IsNaN(LoopB) && _engine.SetLoop(LoopA, LoopB))
        {
            IsLooping = true;
        }
        else
        {
            _engine.ClearLoop();
            IsLooping = false;
        }
    }

    // ---- Marcadores ----

    public ObservableCollection<Marker> Markers { get; } = [];

    [RelayCommand]
    private void AddMarker()
    {
        if (!_engine.IsLoaded) return;
        double position = _engine.PositionSeconds;
        if (Markers.Any(m => Math.Abs(m.Seconds - position) < 0.05)) return;

        int index = 0;
        while (index < Markers.Count && Markers[index].Seconds < position) index++;
        Markers.Insert(index, new Marker(position, FormatTime(position)));
    }

    [RelayCommand]
    private void RemoveMarker(Marker? marker)
    {
        if (marker != null) Markers.Remove(marker);
    }

    [RelayCommand]
    private void JumpToMarker(Marker? marker)
    {
        if (marker != null) Seek(marker.Seconds);
    }

    [RelayCommand]
    private void NextMarker()
    {
        double position = _engine.PositionSeconds;
        Marker? next = Markers.FirstOrDefault(m => m.Seconds > position + 0.05);
        if (next != null) Seek(next.Seconds);
    }

    [RelayCommand]
    private void PreviousMarker()
    {
        // Margem de 1 s: apertar duas vezes seguidas volta ao marcador anterior, não ao mesmo.
        double position = _engine.PositionSeconds;
        Marker? previous = Markers.LastOrDefault(m => m.Seconds < position - 1.0);
        Seek(previous?.Seconds ?? 0);
    }

    public static string FormatTimePrecise(double seconds)
    {
        if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
        int minutes = (int)(seconds / 60);
        return $"{minutes}:{seconds - minutes * 60:00.00}";
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
        LoopA = double.NaN;
        LoopB = double.NaN;
        IsLooping = false;
        Markers.Clear();

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
