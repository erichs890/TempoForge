using System.Windows;
using TempoForge.App.Input;

namespace TempoForge.App.Views;

public partial class ShortcutsDialog : Window
{
    public ShortcutsDialog()
    {
        InitializeComponent();
        List.ItemsSource = Shortcuts.Help.Select(h => new { h.Keys, h.Action }).ToList();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
