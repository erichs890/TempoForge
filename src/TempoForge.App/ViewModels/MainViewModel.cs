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
