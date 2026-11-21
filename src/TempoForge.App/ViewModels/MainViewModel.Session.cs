using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TempoForge.App.Localization;
using TempoForge.Core.Library;
using TempoForge.Core.Looping;
using TempoForge.Core.Persistence;
using TempoForge.Core.Waveform;

namespace TempoForge.App.ViewModels;

/// <summary>Parte do ViewModel que cuida do que persiste entre sessões: ajustes, recentes e biblioteca.</summary>
public sealed partial class MainViewModel
{
    private readonly DataStore _store = new();
    private AppSettings _settings = new();
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private string? _currentPath;
    private bool _restoring;
    private CancellationTokenSource? _libraryCancel;
    private List<LibraryItem> _allLibraryItems = [];

    public AppSettings Settings => _settings;

    public ObservableCollection<LibraryItem> Recents { get; } = [];
    public ObservableCollection<LibraryItem> LibraryItems { get; } = [];
    public ObservableCollection<string> LibraryFolders { get; } = [];

    [ObservableProperty] private string _libraryFilter = "";
    [ObservableProperty] private string _libraryStatus = "";

    private void InitializeSession()
    {
        _settings = _store.LoadSettings();

        _restoring = true;
        Equalizer.IsEnabled = _settings.EqEnabled;
        Equalizer.PreampDb = _settings.PreampDb;
        Equalizer.SetGains(_settings.EqGainsDb);
        VocalReducer.IsEnabled = _settings.VocalReducerEnabled;
        VocalReducer.Amount = _settings.VocalReducerAmount;
        VocalReducer.LowCut = _settings.VocalReducerLowCutHz;
        VocalReducer.HighCut = _settings.VocalReducerHighCutHz;
        _restoring = false;

        RefreshRecents();
        foreach (string folder in _settings.LibraryFolders) LibraryFolders.Add(folder);
        RescanLibrary();

        // Qualquer ajuste alterado é gravado meio segundo depois da última mudança.
        _saveTimer.Tick += (_, _) => SaveNow();
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Speed) or nameof(Semitones) or nameof(Cents)
                or nameof(LoopA) or nameof(LoopB) or nameof(IsLooping)) MarkDirty();
        };
        Markers.CollectionChanged += (_, _) => MarkDirty();
        Equalizer.PropertyChanged += (_, _) => MarkDirty();
        foreach (EqBandViewModel band in Equalizer.Bands) band.PropertyChanged += (_, _) => MarkDirty();
        VocalReducer.PropertyChanged += (_, _) => MarkDirty();
    }

    private void MarkDirty()
    {
        if (_restoring) return;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    /// <summary>Grava agora os ajustes globais e os da faixa aberta.</summary>
    public void SaveNow()
    {
        _saveTimer.Stop();

        _settings.EqEnabled = Equalizer.IsEnabled;
        _settings.PreampDb = Equalizer.PreampDb;
        _settings.EqGainsDb = Equalizer.GetGains();
        _settings.VocalReducerEnabled = VocalReducer.IsEnabled;
        _settings.VocalReducerAmount = VocalReducer.Amount;
        _settings.VocalReducerLowCutHz = VocalReducer.LowCut;
        _settings.VocalReducerHighCutHz = VocalReducer.HighCut;
        _settings.LibraryFolders = LibraryFolders.ToList();
        _store.SaveSettings(_settings);

        if (_currentPath != null)
        {
            _store.SaveTrack(_currentPath, new TrackState
            {
                Speed = Speed,
                Semitones = Semitones,
                Cents = Cents,
                LoopA = double.IsNaN(LoopA) ? null : LoopA,
                LoopB = double.IsNaN(LoopB) ? null : LoopB,
                IsLooping = IsLooping,
                Markers = Markers.ToList(),
            });
        }
    }

    private void RestoreTrack(string path)
    {
        TrackState state = _store.LoadTrack(path);
        _restoring = true;
        Speed = state.Speed;
        Semitones = state.Semitones;
        Cents = state.Cents;
        LoopA = state.LoopA ?? double.NaN;
        LoopB = state.LoopB ?? double.NaN;
        ApplyLoop(enable: state.IsLooping);
        Markers.Clear();
        foreach (Marker marker in state.Markers.OrderBy(m => m.Seconds)) Markers.Add(marker);
        _restoring = false;
    }

    private void LoadPeaks(string path)
    {
        _peakCancel = new CancellationTokenSource();
        string? key = DataStore.TrackKey(path);
        string? cachePath = key == null ? null : _store.PeaksPath(key);

        int buckets = PeakBucketsFor(_engine.DurationSeconds);
        PeakData? cached = cachePath == null ? null : PeakData.TryLoad(cachePath, buckets);
        if (cached != null)
        {
            Peaks = cached;
            return;
        }

        PeakData peaks = PeakData.Build(path, buckets, _peakCancel.Token, out Task scan);
        Peaks = peaks;
        if (cachePath != null)
            scan.ContinueWith(_ => peaks.Save(cachePath), TaskContinuationOptions.OnlyOnRanToCompletion);
    }

    private void RememberOpened(string path)
    {
        _settings.AddRecent(path);
        _settings.LastFile = path;
        RefreshRecents();
        _store.SaveSettings(_settings);
    }

    private void RefreshRecents()
    {
        Recents.Clear();
        foreach (string path in _settings.Recents) Recents.Add(new LibraryItem(path));
    }

    /// <summary>Tira dos recentes um arquivo que não existe mais.</summary>
    public void ForgetRecent(string path)
    {
        _settings.Recents.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        if (string.Equals(_settings.LastFile, path, StringComparison.OrdinalIgnoreCase)) _settings.LastFile = null;
        RefreshRecents();
        _store.SaveSettings(_settings);
    }

    // ---- Biblioteca ----

    public void AddLibraryFolder(string folder)
    {
        if (!Directory.Exists(folder)) return;
        if (LibraryFolders.Any(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase))) return;
        LibraryFolders.Add(folder);
        SaveNow();
        RescanLibrary();
    }

    [RelayCommand]
    private void RemoveLibraryFolder(string? folder)
    {
        if (folder == null || !LibraryFolders.Remove(folder)) return;
        SaveNow();
        RescanLibrary();
    }

    [RelayCommand]
    private async Task RescanLibraryAsync()
    {
        _libraryCancel?.Cancel();
        var cancel = new CancellationTokenSource();
        _libraryCancel = cancel;
        string[] folders = LibraryFolders.ToArray();

        if (folders.Length == 0)
        {
            _allLibraryItems = [];
            ApplyLibraryFilter();
            return;
        }

        LibraryStatus = Loc.T("LibSearching");
        try
        {
            List<LibraryItem> items = await Task.Run(() => LibraryScanner.Scan(folders, cancel.Token), cancel.Token);
            if (cancel.IsCancellationRequested) return;
            _allLibraryItems = items;
            ApplyLibraryFilter();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void RescanLibrary() => _ = RescanLibraryAsync();

    partial void OnLibraryFilterChanged(string value) => ApplyLibraryFilter();

    private void ApplyLibraryFilter()
    {
        string filter = LibraryFilter.Trim();
        LibraryItems.Clear();
        foreach (LibraryItem item in _allLibraryItems)
        {
            if (filter.Length == 0
                || item.Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                || item.Folder.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
                LibraryItems.Add(item);
        }

        LibraryStatus = LibraryFolders.Count == 0
            ? Loc.T("LibAddFolder")
            : _allLibraryItems.Count == 0
                ? Loc.T("LibNone")
                : filter.Length > 0
                    ? Loc.F("LibCountFiltered", LibraryItems.Count, _allLibraryItems.Count)
                    : Loc.F("LibCount", _allLibraryItems.Count);
    }
}
