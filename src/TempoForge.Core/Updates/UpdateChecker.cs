using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TempoForge.Core.Updates;

/// <summary>Uma versão publicada mais nova que a instalada.</summary>
public sealed record UpdateInfo(Version Version, string InstallerUrl, string FileName, long Size, string? Sha256);

/// <summary>
/// Consulta a última versão publicada no GitHub Releases e baixa o instalador.
/// Qualquer falha de rede é tratada como "sem atualização": o app nunca deixa de abrir por isso.
/// </summary>
public sealed class UpdateChecker
{
    public const string Repository = "erichs890/TempoForge";

    private readonly HttpClient _http;
    private readonly string _latestUrl;
    private readonly string _downloadPrefix;

    public UpdateChecker(HttpMessageHandler? handler = null, string repository = Repository)
    {
        _http = handler == null ? new HttpClient() : new HttpClient(handler);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TempoForge-Updater");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _latestUrl = $"https://api.github.com/repos/{repository}/releases/latest";
        _downloadPrefix = $"https://github.com/{repository}/releases/download/";
    }

    /// <summary>Devolve a atualização disponível, ou nulo se não houver (ou se a consulta falhar).</summary>
    public async Task<UpdateInfo?> CheckAsync(Version current, CancellationToken cancel = default)
    {
        try
        {
            using HttpResponseMessage response = await _http.GetAsync(_latestUrl, cancel).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            string json = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
            return Parse(json, current, _downloadPrefix);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException)
        {
            return null;
        }
    }

    /// <summary>Interpreta a resposta da API; público para os testes.</summary>
    public static UpdateInfo? Parse(string json, Version current, string downloadPrefix)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        if (root.TryGetProperty("draft", out JsonElement draft) && draft.ValueKind == JsonValueKind.True) return null;
        if (root.TryGetProperty("prerelease", out JsonElement pre) && pre.ValueKind == JsonValueKind.True) return null;
        // A versão vem do nome do instalador ("TempoForge-1.2.3-setup.exe"); a tag só vale
        // como reserva, porque uma release pode ter uma tag qualquer.
        Version? tagVersion = null;
        if (root.TryGetProperty("tag_name", out JsonElement tag)) TryParseVersion(tag.GetString(), out tagVersion);
        if (!root.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array) return null;

        foreach (JsonElement asset in assets.EnumerateArray())
        {
            string? name = asset.TryGetProperty("name", out JsonElement n) ? n.GetString() : null;
            string? url = asset.TryGetProperty("browser_download_url", out JsonElement u) ? u.GetString() : null;
            if (name == null || url == null) continue;
            if (!name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase)) continue;
            // Só aceita o instalador hospedado no próprio repositório, por HTTPS.
            if (!url.StartsWith(downloadPrefix, StringComparison.Ordinal)) continue;

            Match match = Regex.Match(name, @"-(\d+(?:\.\d+){1,3})-setup\.exe$", RegexOptions.IgnoreCase);
            Version? version = match.Success && TryParseVersion(match.Groups[1].Value, out Version? named)
                ? named
                : tagVersion;
            if (version == null || version <= Normalize(current)) return null;

            long size = asset.TryGetProperty("size", out JsonElement s) && s.TryGetInt64(out long bytes) ? bytes : 0;
            string? digest = asset.TryGetProperty("digest", out JsonElement d) ? d.GetString() : null;
            string? sha256 = digest != null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                ? digest["sha256:".Length..]
                : null;
            return new UpdateInfo(version, url, Path.GetFileName(name), size, sha256);
        }
        return null;
    }

    /// <summary>Aceita "v1.2.3", "1.2" e ignora sufixos como "-beta" ou "+abc".</summary>
    public static bool TryParseVersion(string? text, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim().TrimStart('v', 'V');
        int cut = text.IndexOfAny(['-', '+']);
        if (cut >= 0) text = text[..cut];
        if (!Version.TryParse(text.Contains('.') ? text : text + ".0", out Version? parsed)) return false;
        version = Normalize(parsed);
        return true;
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));

    /// <summary>
    /// Baixa o instalador para <paramref name="folder"/> e confere tamanho e SHA-256 quando o
    /// GitHub os informa. Devolve o caminho do arquivo; lança se o download não conferir.
    /// </summary>
    public async Task<string> DownloadAsync(UpdateInfo update, string folder, IProgress<double>? progress = null,
        CancellationToken cancel = default)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, update.FileName);
        string partial = path + ".parcial";
        try
        {
            using (HttpResponseMessage response = await _http
                       .GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                long total = response.Content.Headers.ContentLength ?? update.Size;
                await using Stream source = await response.Content.ReadAsStreamAsync(cancel).ConfigureAwait(false);
                await using var target = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = new byte[81920];
                long done = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancel).ConfigureAwait(false)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), cancel).ConfigureAwait(false);
                    hash.AppendData(buffer, 0, read);
                    done += read;
                    if (total > 0) progress?.Report(Math.Min(1.0, done / (double)total));
                }

                if (update.Size > 0 && done != update.Size)
                    throw new InvalidDataException("O instalador baixado veio com tamanho diferente do esperado.");
                if (update.Sha256 != null
                    && !Convert.ToHexString(hash.GetHashAndReset()).Equals(update.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("O instalador baixado não confere com o publicado.");
            }

            File.Move(partial, path, overwrite: true);
            return path;
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
        }
    }
}
