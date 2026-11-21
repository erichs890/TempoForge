using System.Runtime;
using System.Windows;
using System.Windows.Threading;
using TempoForge.App.Localization;
using TempoForge.App.Themes;
using TempoForge.App.Views;
using TempoForge.Core.Persistence;

namespace TempoForge.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Coletas de lixo curtas: a thread de áudio não aloca, mas a UI sim.
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);

        // Tema e idioma valem desde a primeira janela (inclusive a de atualização).
        AppSettings settings = new DataStore().LoadSettings();
        ThemeManager.Apply(settings.Theme);
        Loc.Apply(settings.Language ?? Loc.DefaultLanguage);

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
