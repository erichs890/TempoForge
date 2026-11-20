using System.ComponentModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using TempoForge.Core.Export;

namespace TempoForge.App.Views;

/// <summary>Escolhe formato e destino e acompanha a exportação, que roda fora da thread da interface.</summary>
public partial class ExportDialog : Window
{
    private readonly ExportOptions _template;
    private readonly string _suggestedName;
    private CancellationTokenSource? _cancel;

    /// <param name="template">Ajustes atuais; formato, destino e trecho são decididos aqui.</param>
    /// <param name="suggestedName">Nome de arquivo sugerido, sem extensão.</param>
    /// <param name="summary">Descrição legível dos ajustes que serão aplicados.</param>
    public ExportDialog(ExportOptions template, string suggestedName, string summary)
    {
        InitializeComponent();
        _template = template;
        _suggestedName = suggestedName;
        Summary.Text = summary;

        foreach (int bitrate in AudioExporter.Mp3Bitrates) BitrateBox.Items.Add($"{bitrate} kbps");
        BitrateBox.SelectedIndex = AudioExporter.Mp3Bitrates.Length - 1;

        RangeOnly.IsEnabled = template.Range != null;
        RangeOnly.IsChecked = false;
    }

    private void OnFormatChanged(object sender, RoutedEventArgs e)
    {
        if (BitratePanel != null) BitratePanel.IsEnabled = Mp3Option.IsChecked == true;
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        bool mp3 = Mp3Option.IsChecked == true;
        string extension = mp3 ? ".mp3" : ".wav";
        var dialog = new SaveFileDialog
        {
            Title = "Salvar áudio exportado",
            FileName = _suggestedName + extension,
            DefaultExt = extension,
            Filter = mp3 ? "MP3|*.mp3" : "WAV|*.wav",
            InitialDirectory = Path.GetDirectoryName(_template.SourcePath),
        };
        if (dialog.ShowDialog(this) != true) return;

        if (string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(_template.SourcePath),
                StringComparison.OrdinalIgnoreCase))
        {
            Status.Text = "Escolha outro nome: não é possível gravar por cima do arquivo que está aberto.";
            return;
        }

        ExportOptions options = _template with
        {
            OutputPath = dialog.FileName,
            Format = mp3 ? ExportFormat.Mp3 : ExportFormat.Wav,
            Mp3Bitrate = AudioExporter.Mp3Bitrates[BitrateBox.SelectedIndex],
            Range = RangeOnly.IsChecked == true ? _template.Range : null,
        };

        _cancel = new CancellationTokenSource();
        OptionsPanel.IsEnabled = false;
        ExportButton.IsEnabled = false;
        CancelButton.Content = "Cancelar";
        Progress.Visibility = Visibility.Visible;
        Progress.Value = 0;
        Status.Text = "Exportando…";

        var progress = new Progress<double>(value => Progress.Value = value);
        try
        {
            CancellationToken token = _cancel.Token;
            await Task.Run(() => AudioExporter.Export(options, progress, token), token);
            Progress.Value = 1;
            Status.Text = $"Pronto: {Path.GetFileName(options.OutputPath)}";
        }
        catch (OperationCanceledException)
        {
            Status.Text = "Exportação cancelada.";
            Progress.Visibility = Visibility.Hidden;
        }
        catch (Exception ex)
        {
            Status.Text = $"Não foi possível exportar. {ex.Message}";
            Progress.Visibility = Visibility.Hidden;
        }
        finally
        {
            _cancel.Dispose();
            _cancel = null;
            OptionsPanel.IsEnabled = true;
            ExportButton.IsEnabled = true;
            CancelButton.Content = "Fechar";
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        if (_cancel != null) _cancel.Cancel();
        else Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Fechar durante a exportação equivale a cancelar; a janela fecha quando ela parar.
        if (_cancel != null)
        {
            _cancel.Cancel();
            e.Cancel = true;
        }
        base.OnClosing(e);
    }
}
