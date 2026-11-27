using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TempoForge.Core.Dsp;
using TempoForge.Core.Looping;

namespace TempoForge.Core.Persistence;

/// <summary>Preferências globais do aplicativo.</summary>
public sealed class AppSettings
{
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double WindowWidth { get; set; } = 1320;
    public double WindowHeight { get; set; } = 780;
    public bool WindowMaximized { get; set; }

    /// <summary>"dark" ou "light".</summary>
    public string Theme { get; set; } = "dark";

    /// <summary>"pt" ou "en"; nulo = seguir o idioma do Windows.</summary>
    public string? Language { get; set; }

    public string? LastFile { get; set; }
    public List<string> Recents { get; set; } = [];
    public List<string> LibraryFolders { get; set; } = [];

    /// <summary>Volume geral do app, de 0 a 100.</summary>
    public double Volume { get; set; } = 100;

    public bool EqEnabled { get; set; } = true;
    public double PreampDb { get; set; }
    public double[] EqGainsDb { get; set; } = new double[EffectSettings.EqBandCount];

    public bool VocalReducerEnabled { get; set; }
    public double VocalReducerAmount { get; set; } = 100;
    public double VocalReducerLowCutHz { get; set; } = 120;
    public double VocalReducerHighCutHz { get; set; } = 8000;

    public const int MaxRecents = 30;

    /// <summary>Põe o arquivo no topo dos recentes, sem repetir.</summary>
    public void AddRecent(string path)
    {
        Recents.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Recents.Insert(0, path);
        if (Recents.Count > MaxRecents) Recents.RemoveRange(MaxRecents, Recents.Count - MaxRecents);
    }
}

/// <summary>Ajustes guardados para cada arquivo de áudio.</summary>
public sealed class TrackState
{
    public double Speed { get; set; } = 1.0;
    public int Semitones { get; set; }
    public int Cents { get; set; }
    public double? LoopA { get; set; }
    public double? LoopB { get; set; }
    public bool IsLooping { get; set; }
    public List<Marker> Markers { get; set; } = [];
}

/// <summary>
/// Leitura e gravação dos dados do usuário em uma pasta (por padrão %AppData%\TempoForge).
/// A gravação é atômica: escreve num arquivo temporário e só então substitui o definitivo.
/// </summary>
public sealed class DataStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public DataStore(string? root = null)
    {
        // TEMPOFORGE_DATA permite usar outra pasta (modo portátil ou testes).
        string? overridden = Environment.GetEnvironmentVariable("TEMPOFORGE_DATA");
        Root = root
               ?? (string.IsNullOrWhiteSpace(overridden) ? null : overridden)
               ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TempoForge");
    }

    public string Root { get; }

    private string SettingsPath => Path.Combine(Root, "settings.json");
    private string TrackPath(string key) => Path.Combine(Root, "tracks", key + ".json");
    public string PeaksPath(string key) => Path.Combine(Root, "cache", "peaks", key + ".pk");

    public AppSettings LoadSettings() => Load<AppSettings>(SettingsPath) ?? new AppSettings();

    public void SaveSettings(AppSettings settings) => Save(SettingsPath, settings);

    public TrackState LoadTrack(string audioPath)
    {
        string? key = TrackKey(audioPath);
        return (key == null ? null : Load<TrackState>(TrackPath(key))) ?? new TrackState();
    }

    public void SaveTrack(string audioPath, TrackState state)
    {
        string? key = TrackKey(audioPath);
        if (key != null) Save(TrackPath(key), state);
    }

    /// <summary>
    /// Identifica um arquivo por caminho, tamanho e data de modificação: se o arquivo for
    /// substituído por outro com o mesmo nome, os ajustes antigos não são reaproveitados.
    /// </summary>
    public static string? TrackKey(string audioPath)
    {
        try
        {
            var info = new FileInfo(audioPath);
            if (!info.Exists) return null;
            string identity = $"{info.FullName.ToUpperInvariant()}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
            return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static T? Load<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return null; // arquivo corrompido ou ilegível: começa do padrão em vez de impedir o uso
        }
    }

    private static void Save<T>(string path, T value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Json));
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Falha ao salvar preferências não deve derrubar a reprodução.
        }
    }
}
