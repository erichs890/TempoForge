using System.Windows;

namespace TempoForge.App.Themes;

/// <summary>Troca a paleta (escura ou clara) com o app aberto.</summary>
public static class ThemeManager
{
    public const string Dark = "dark", Light = "light";

    public static string Current { get; private set; } = Dark;
    public static bool IsDark => Current == Dark;

    public static void Apply(string? theme)
    {
        Current = theme == Light ? Light : Dark;
        if (Application.Current is not { } app) return;

        var palette = new ResourceDictionary
        {
            Source = new Uri($"Themes/Palette.{(IsDark ? "Dark" : "Light")}.xaml", UriKind.Relative),
        };
        // A paleta é sempre a primeira entrada (veja App.xaml); os estilos a referenciam
        // com DynamicResource e se atualizam sozinhos.
        app.Resources.MergedDictionaries[0] = palette;

        foreach (Window window in app.Windows) WindowTheme.SyncTitleBar(window);
    }
}
