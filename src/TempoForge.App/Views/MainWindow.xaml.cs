using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using TempoForge.App.ViewModels;
using TempoForge.Core.Decoding;

namespace TempoForge.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly DispatcherTimer _timer;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        Waveform.SeekRequested += _vm.Seek;
        Waveform.LoopPointMoved += _vm.MoveLoopPoint;
        _vm.PropertyChanged += OnViewModelChanged;

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) =>
        {
            _vm.Tick();
            Waveform.RefreshIfGrowing();
        };
        _timer.Start();

        string[] args = Environment.GetCommandLineArgs();
        if (args.Length > 1) Loaded += (_, _) => TryOpen(args[1]);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsPlaying))
            PlayButton.Content = _vm.IsPlaying ? "Pausar" : "Tocar";
        else if (e.PropertyName == nameof(MainViewModel.IsLooping))
            LoopToggle.Background = (System.Windows.Media.Brush)FindResource(_vm.IsLooping ? "LoopBrush" : "SurfaceHighBrush");
    }

    private void OnOpenClick(object sender, RoutedEventArgs e)
    {
        string patterns = string.Join(";", AudioDecoder.SupportedExtensions.Select(x => "*" + x));
        var dialog = new OpenFileDialog
        {
            Title = "Abrir áudio",
            Filter = $"Arquivos de áudio|{patterns}|Todos os arquivos|*.*",
        };
        if (dialog.ShowDialog(this) == true) TryOpen(dialog.FileName);
    }

    private void TryOpen(string path)
    {
        try
        {
            _vm.OpenFile(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Não foi possível abrir este arquivo.\n\n{ex.Message}", "TempoForge",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnExportClick(object sender, RoutedEventArgs e) => ShowExportDialog();

    private void ShowExportDialog()
    {
        var options = _vm.CreateExportOptions();
        if (options == null) return;
        new ExportDialog(options, _vm.SuggestExportName(), _vm.DescribeAdjustments()) { Owner = this }.ShowDialog();
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => _vm.SeekRelative(-5);

    private void OnForwardClick(object sender, RoutedEventArgs e) => _vm.SeekRelative(5);

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _vm.Dispose();
        base.OnClosed(e);
    }
}
