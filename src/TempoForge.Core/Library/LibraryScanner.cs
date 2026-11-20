using TempoForge.Core.Decoding;

namespace TempoForge.Core.Library;

/// <summary>Um arquivo de áudio encontrado numa pasta da biblioteca.</summary>
public sealed record LibraryItem(string Path)
{
    public string Name { get; } = System.IO.Path.GetFileNameWithoutExtension(Path);
    public string Folder { get; } = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(Path) ?? "") ?? "";
}

public static class LibraryScanner
{
    /// <summary>Lista, em ordem alfabética, os arquivos de áudio das pastas e subpastas dadas.</summary>
    public static List<LibraryItem> Scan(IEnumerable<string> folders, CancellationToken cancel = default)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<LibraryItem>();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        };

        foreach (string folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (string file in Directory.EnumerateFiles(folder, "*", options))
            {
                cancel.ThrowIfCancellationRequested();
                if (AudioDecoder.IsSupported(file) && seen.Add(file)) items.Add(new LibraryItem(file));
            }
        }

        items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        return items;
    }
}
