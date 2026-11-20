using System.Runtime;
using System.Windows;
using System.Windows.Threading;
using TempoForge.App.Views;

namespace TempoForge.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Coletas de lixo curtas: a thread de áudio não aloca, mas a UI sim.
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);

        // Antes de abrir: se houver versão nova publicada, baixa e roda o instalador.
        if (UpdateWindow.ShouldCheck)
        {
            var update = new UpdateWindow();
            update.ShowDialog();
            if (update.InstallerStarted)
            {
                Shutdown();
                return;
            }
        }

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow = new MainWindow();
        MainWindow.Show();
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "TempoForge", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
