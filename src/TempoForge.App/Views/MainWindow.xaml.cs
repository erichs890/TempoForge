using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using TempoForge.App.Input;
using TempoForge.App.Localization;
using TempoForge.App.Themes;
using TempoForge.App.ViewModels;
using TempoForge.Core.Decoding;
using TempoForge.Core.Library;
using TempoForge.Core.Persistence;

namespace TempoForge.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly DispatcherTimer _timer;

    public MainWindow()
    {
        InitializeComponent();
        WindowTheme.SyncTitleBar(this);
        DataContext = _vm;
        RestoreWindowPlacement();

        Waveform.SeekRequested += _vm.Seek;
        Waveform.LoopPointMoved += _vm.MoveLoopPoint;
        Waveform.ViewChanged += () => ZoomLabel.Text = Waveform.Zoom > 1.0 ? $"{Waveform.Zoom:0.#}×" : "";
        _vm.PropertyChanged += OnViewModelChanged;

        foreach (ComboBoxItem item in LanguageBox.Items)
            if ((string)item.Tag == Loc.Language) LanguageBox.SelectedItem = item;
        UpdateThemeButton();
        Loc.Changed += UpdateThemeButton;

        _timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) =>
        {
            _vm.Tick();
            Waveform.RefreshIfGrowing();
        };
        _timer.Start();

        Loaded += (_, _) => OpenInitialFile();
    }

    /// <summary>Abre o arquivo passado na linha de comando ou, na falta dele, o último usado (pausado).</summary>
    private void OpenInitialFile()
    {
        string[] args = Environment.GetCommandLineArgs();
        if (args.Length > 1)
        {
            TryOpen(args[1]);
            return;
        }

        string? last = _vm.Settings.LastFile;
        if (last != null && File.Exists(last)) TryOpen(last, autoPlay: false, quiet: true);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsPlaying))
            PlayButton.Content = _vm.IsPlaying ? "" : ""; // ícones de pausar / tocar
        else if (e.PropertyName == nameof(MainViewModel.IsLooping))
        {
            // Referências dinâmicas: as cores acompanham a troca de tema.
            LoopToggle.SetResourceReference(BackgroundProperty, _vm.IsLooping ? "LoopBrush" : "SurfaceHighBrush");
            LoopToggle.SetResourceReference(ForegroundProperty, _vm.IsLooping ? "OnLoopBrush" : "TextBrush");
        }
        else if (e.PropertyName == nameof(MainViewModel.IsLoaded))
            EmptyState.Visibility = _vm.IsLoaded ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---- Abrir arquivos ----

    private void OnOpenClick(object sender, RoutedEventArgs e) => ShowOpenDialog();

    private void ShowOpenDialog()
    {
        string patterns = string.Join(";", AudioDecoder.SupportedExtensions.Select(x => "*" + x));
        var dialog = new OpenFileDialog
        {
            Title = Loc.T("OpenTitle"),
            Filter = $"{Loc.T("FilterAudio")}|{patterns}|{Loc.T("FilterAll")}|*.*",
        };
        if (dialog.ShowDialog(this) == true) TryOpen(dialog.FileName);
    }

    private void TryOpen(string path, bool autoPlay = true, bool quiet = false)
    {
        if (!File.Exists(path))
        {
            _vm.ForgetRecent(path);
            if (!quiet)
                MessageBox.Show(this, Loc.F("FileGone", path), "TempoForge",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _vm.OpenFile(path, autoPlay);
        }
        catch (Exception ex)
        {
            if (!quiet)
                MessageBox.Show(this, Loc.F("OpenFailed", ex.Message), "TempoForge",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnFileListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListBox { SelectedItem: LibraryItem item }) TryOpen(item.Path);
    }

    private void OnFileListKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && sender is ListBox { SelectedItem: LibraryItem item })
        {
            TryOpen(item.Path);
            e.Handled = true;
        }
    }

    private void OnAddFolderClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = Loc.T("PickFolder") };
        if (dialog.ShowDialog(this) == true) _vm.AddLibraryFolder(dialog.FolderName);
    }

    // ---- Arrastar e soltar ----

    private static string[] DroppedPaths(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) as string[] ?? [];

    private void OnDragEnter(object sender, DragEventArgs e)
    {
        bool usable = DroppedPaths(e).Any(p => Directory.Exists(p) || AudioDecoder.IsSupported(p));
        e.Effects = usable ? DragDropEffects.Copy : DragDropEffects.None;
        DropHint.Visibility = usable ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e) => DropHint.Visibility = Visibility.Collapsed;

    private void OnDrop(object sender, DragEventArgs e)
    {
        DropHint.Visibility = Visibility.Collapsed;
        string[] paths = DroppedPaths(e);

        // Pastas soltas entram na biblioteca; o primeiro arquivo de áudio solto é aberto.
        foreach (string folder in paths.Where(Directory.Exists)) _vm.AddLibraryFolder(folder);
        string? file = paths.FirstOrDefault(p => File.Exists(p) && AudioDecoder.IsSupported(p));
        if (file != null) TryOpen(file);
        else if (paths.Any(Directory.Exists)) LibraryTab.IsChecked = true;
    }

    // ---- Exportar ----

    private void OnExportClick(object sender, RoutedEventArgs e) => ShowExportDialog();

    private void ShowExportDialog()
    {
        var options = _vm.CreateExportOptions();
        if (options == null) return;
        new ExportDialog(options, _vm.SuggestExportName(), _vm.DescribeAdjustments()) { Owner = this }.ShowDialog();
    }

    private void OnBackClick(object sender, RoutedEventArgs e) => _vm.SeekRelative(-5);

    private void OnForwardClick(object sender, RoutedEventArgs e) => _vm.SeekRelative(5);

    // ---- Tema e idioma ----

    /// <summary>O botão mostra o tema para o qual ele troca.</summary>
    private void UpdateThemeButton() =>
        ThemeButton.Content = Loc.T(ThemeManager.IsDark ? "ThemeLight" : "ThemeDark");

    private void OnThemeClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.Apply(ThemeManager.IsDark ? ThemeManager.Light : ThemeManager.Dark);
        _vm.Settings.Theme = ThemeManager.Current;
        _vm.SaveNow();
        UpdateThemeButton();
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageBox.SelectedItem is not ComboBoxItem { Tag: string language } || language == Loc.Language) return;
        Loc.Apply(language);
        _vm.Settings.Language = language;
        _vm.SaveNow();
    }

    // ---- Zoom da faixa ----

    private void OnZoomInClick(object sender, RoutedEventArgs e) => Waveform.ZoomIn();

    private void OnZoomOutClick(object sender, RoutedEventArgs e) => Waveform.ZoomOut();

    private void OnZoomResetClick(object sender, RoutedEventArgs e) => Waveform.ResetZoom();

    private void OnZoomLoopClick(object sender, RoutedEventArgs e) => Waveform.ZoomToRange(_vm.LoopA, _vm.LoopB);

    // ---- Equalizador ----

    /// <summary>Duplo clique em um controle do equalizador devolve-o a 0 dB.</summary>
    private void OnEqSliderDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Slider slider) return;
        slider.Value = 0;
        e.Handled = true;
    }

    // ---- Atalhos de teclado ----

    /// <summary>Enquanto se digita no filtro ou se navega numa lista, as teclas pertencem a esse controle.</summary>
    private static bool FocusIsTyping => Keyboard.FocusedElement is TextBox;
    private static bool FocusIsList => Keyboard.FocusedElement is ListBoxItem or ListBox;

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        ModifierKeys modifiers = Keyboard.Modifiers;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (modifiers == ModifierKeys.Control && key == Key.O) { ShowOpenDialog(); e.Handled = true; return; }
        if (modifiers == ModifierKeys.Control && key == Key.E) { ShowExportDialog(); e.Handled = true; return; }
        if (key == Key.F1) { ShowShortcutHelp(); e.Handled = true; return; }
        if (key == Key.Escape) { Keyboard.ClearFocus(); FocusManager.SetFocusedElement(this, this); return; }

        if (FocusIsTyping) return;
        if (FocusIsList && key is Key.Up or Key.Down or Key.Home or Key.End or Key.Enter) return;

        if (Shortcuts.HandleKey(_vm, key, modifiers)) e.Handled = true;
    }

    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        base.OnPreviewTextInput(e);
        if (FocusIsTyping || string.IsNullOrEmpty(e.Text)) return;
        if (Shortcuts.HandleText(_vm, e.Text)) e.Handled = true;
    }

    private void OnHelpClick(object sender, RoutedEventArgs e) => ShowShortcutHelp();

    private void ShowShortcutHelp()
    {
        new ShortcutsDialog { Owner = this }.ShowDialog();
    }

    // ---- Janela ----

    private void RestoreWindowPlacement()
    {
        AppSettings s = _vm.Settings;
        Width = Math.Max(MinWidth, s.WindowWidth);
        Height = Math.Max(MinHeight, s.WindowHeight);

        if (s.WindowLeft is double left && s.WindowTop is double top
            && left >= SystemParameters.VirtualScreenLeft - 50
            && top >= SystemParameters.VirtualScreenTop - 50
            && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 100
            && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 100)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }
        if (s.WindowMaximized) WindowState = WindowState.Maximized;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        AppSettings s = _vm.Settings;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        Rect bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (!bounds.IsEmpty)
        {
            s.WindowLeft = bounds.Left;
            s.WindowTop = bounds.Top;
            s.WindowWidth = bounds.Width;
            s.WindowHeight = bounds.Height;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        _vm.Dispose(); // grava os ajustes
        base.OnClosed(e);
    }
}
