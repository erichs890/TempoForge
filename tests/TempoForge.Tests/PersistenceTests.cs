using TempoForge.Core.Library;
using TempoForge.Core.Looping;
using TempoForge.Core.Persistence;
using TempoForge.Core.Waveform;

namespace TempoForge.Tests;

public class PersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"tempoforge-data-{Guid.NewGuid():N}");

    public PersistenceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private string NewAudioFile(string name, int bytes = 100)
    {
        string path = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    [Fact]
    public void SettingsSurviveARestart()
    {
        var store = new DataStore(Path.Combine(_root, "data"));
        var settings = store.LoadSettings();
        Assert.Empty(settings.Recents); // primeira execução: padrões

        settings.PreampDb = -3.5;
        settings.EqGainsDb[2] = 6;
        settings.VocalReducerEnabled = true;
        settings.LibraryFolders.Add(@"C:\Musicas");
        settings.AddRecent(@"C:\a.mp3");
        store.SaveSettings(settings);

        AppSettings loaded = new DataStore(Path.Combine(_root, "data")).LoadSettings();
        Assert.Equal(-3.5, loaded.PreampDb);
        Assert.Equal(6, loaded.EqGainsDb[2]);
        Assert.True(loaded.VocalReducerEnabled);
        Assert.Equal([@"C:\Musicas"], loaded.LibraryFolders);
        Assert.Equal([@"C:\a.mp3"], loaded.Recents);
    }

    [Fact]
    public void RecentsKeepNewestFirstWithoutDuplicatesOrOverflow()
    {
        var settings = new AppSettings();
        for (int i = 0; i < 40; i++) settings.AddRecent($@"C:\m\{i}.mp3");
        settings.AddRecent(@"C:\M\35.MP3"); // mesmo arquivo, outra capitalização

        Assert.Equal(AppSettings.MaxRecents, settings.Recents.Count);
        Assert.Equal(@"C:\M\35.MP3", settings.Recents[0]);
        Assert.Equal(@"C:\m\39.mp3", settings.Recents[1]);
        Assert.Single(settings.Recents, p => p.Equals(@"C:\m\35.mp3", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TrackStateIsStoredPerFile()
    {
        var store = new DataStore(Path.Combine(_root, "data"));
        string first = NewAudioFile("um.mp3"), second = NewAudioFile("dois.mp3");

        store.SaveTrack(first, new TrackState
        {
            Speed = 0.75, Semitones = -2, Cents = 15, LoopA = 12.5, LoopB = 20.25, IsLooping = true,
            Markers = [new Marker(3.5, "Refrão"), new Marker(40, "Solo")],
        });

        TrackState loaded = store.LoadTrack(first);
        Assert.Equal(0.75, loaded.Speed);
        Assert.Equal(-2, loaded.Semitones);
        Assert.Equal(15, loaded.Cents);
        Assert.Equal(12.5, loaded.LoopA);
        Assert.Equal(20.25, loaded.LoopB);
        Assert.True(loaded.IsLooping);
        Assert.Equal([new Marker(3.5, "Refrão"), new Marker(40, "Solo")], loaded.Markers);

        Assert.Equal(1.0, store.LoadTrack(second).Speed); // outro arquivo: padrões
    }

    [Fact]
    public void ReplacingAFileDiscardsItsOldState()
    {
        var store = new DataStore(Path.Combine(_root, "data"));
        string path = NewAudioFile("faixa.mp3", 100);
        store.SaveTrack(path, new TrackState { Speed = 0.5 });

        File.WriteAllBytes(path, new byte[250]); // conteúdo diferente no mesmo caminho
        Assert.Equal(1.0, store.LoadTrack(path).Speed);
    }

    [Fact]
    public void CorruptFilesFallBackToDefaults()
    {
        string data = Path.Combine(_root, "data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "settings.json"), "{ isto não é json");
        Assert.Empty(new DataStore(data).LoadSettings().Recents);
    }

    [Fact]
    public async Task PeakCacheRoundTrips()
    {
        using TempWave wave = TempWave.Sine(1.0, 220);
        PeakData peaks = PeakData.Build(wave.Path, 128, CancellationToken.None, out Task done);
        await done;

        string cache = Path.Combine(_root, "cache", "x.pk");
        peaks.Save(cache);

        PeakData? loaded = PeakData.TryLoad(cache, 128);
        Assert.NotNull(loaded);
        Assert.True(loaded.IsComplete);
        Assert.Equal(peaks.Max, loaded.Max);
        Assert.Equal(peaks.Min, loaded.Min);
        Assert.Null(PeakData.TryLoad(cache, 256)); // resolução diferente: refaz
    }

    [Fact]
    public void LibraryScanFindsAudioInSubfoldersSorted()
    {
        NewAudioFile(@"lib\Zebra.mp3");
        NewAudioFile(@"lib\sub\alfa.flac");
        NewAudioFile(@"lib\sub\capa.jpg");
        NewAudioFile(@"lib\sub\notas.txt");
        string lib = Path.Combine(_root, "lib");

        List<LibraryItem> items = LibraryScanner.Scan([lib, lib, Path.Combine(_root, "nao-existe")]);

        Assert.Equal(["alfa", "Zebra"], items.Select(i => i.Name));
        Assert.Equal("sub", items[0].Folder);
    }
}
