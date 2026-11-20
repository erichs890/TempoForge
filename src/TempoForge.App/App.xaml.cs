using System.Runtime;
using System.Windows;
using System.Windows.Threading;

namespace TempoForge.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Coletas de lixo curtas: a thread de áudio não aloca, mas a UI sim.
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "TempoForge", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
