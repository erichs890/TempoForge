using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TempoForge.App.Themes;

/// <summary>Pede ao Windows a barra de título escura, para combinar com o resto da janela.</summary>
public static class WindowTheme
{
    private const int DwmUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);

    public static void UseDarkTitleBar(Window window)
    {
        void Apply()
        {
            nint handle = new WindowInteropHelper(window).Handle;
            if (handle == 0) return;
            int enabled = 1;
            // Em versões do Windows sem suporte a chamada apenas falha, sem efeito colateral.
            _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
        }

        if (new WindowInteropHelper(window).Handle != 0) Apply();
        else window.SourceInitialized += (_, _) => Apply();
    }
}
