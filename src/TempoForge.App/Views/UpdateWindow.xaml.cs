using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using TempoForge.App.Themes;
using TempoForge.Core.Updates;

namespace TempoForge.App.Views;

/// <summary>
/// Janela mostrada antes da principal: procura uma versão mais nova, baixa o instalador e o
/// executa. Se algo falhar ou o usuário pular, o app abre normalmente.
/// </summary>
public partial class UpdateWindow : Window
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(4);
    private readonly CancellationTokenSource _cancel = new();

    public UpdateWindow()
    {
        InitializeComponent();
        WindowTheme.UseDarkTitleBar(this);
    }

    /// <summary>Verdadeiro se o instalador novo foi iniciado e o app deve encerrar.</summary>
    public bool InstallerStarted { get; private set; }

    public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    /// <summary>
    /// Só a cópia instalada se atualiza sozinha: uma compilação de desenvolvimento não tem
    /// desinstalador ao lado e não deve ser substituída pelo instalador.
    /// </summary>
    public static bool ShouldCheck =>
        Environment.GetEnvironmentVariable("TEMPOFORGE_NO_UPDATE") == null
        && File.Exists(Path.Combine(AppContext.BaseDirectory, "unins000.exe"));

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        try
        {
            await RunAsync();
        }
        catch (Exception)
        {
            // Rede, disco ou download corrompido: segue com a versão instalada.
        }
        Close();
    }

    private async Task RunAsync()
    {
        var checker = new UpdateChecker();

        UpdateInfo? update;
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token))
        {
            timeout.CancelAfter(CheckTimeout);
            update = await checker.CheckAsync(CurrentVersion, timeout.Token);
        }
        if (update == null || _cancel.IsCancellationRequested) return;

        Status.Text = $"Baixando a versão {update.Version.ToString(3)}…";
        Progress.IsIndeterminate = false;
        var progress = new Progress<double>(value => Progress.Value = value);
        string folder = Path.Combine(Path.GetTempPath(), "TempoForge-update");
        string installer = await checker.DownloadAsync(update, folder, progress, _cancel.Token);

        Status.Text = "Instalando a atualização…";
        SkipButton.IsEnabled = false;
        Process.Start(new ProcessStartInfo(installer)
        {
            // O instalador fecha esta cópia, atualiza na mesma pasta e reabre o app.
            Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RELAUNCH=1",
            UseShellExecute = true,
        });
        InstallerStarted = true;
    }

    private void OnSkipClick(object sender, RoutedEventArgs e) => _cancel.Cancel();

    protected override void OnClosed(EventArgs e)
    {
        _cancel.Cancel();
        base.OnClosed(e);
    }
}
