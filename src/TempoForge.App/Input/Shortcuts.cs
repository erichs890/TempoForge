using System.Windows.Input;
using TempoForge.App.ViewModels;

namespace TempoForge.App.Input;

/// <summary>
/// Mapa de atalhos de teclado. Teclas de caractere ([ ] \ + − , . M R) são tratadas pelo
/// texto digitado, não pela posição física, para funcionar em qualquer layout (ABNT2, US…).
/// </summary>
public static class Shortcuts
{
    public const double SeekSeconds = 5, FineSeekSeconds = 1;
    public const int CentsStep = 5;

    /// <summary>Lista exibida na ajuda (F1).</summary>
    public static readonly (string Keys, string Action)[] Help =
    [
        ("Espaço", "Tocar / pausar"),
        ("←  /  →", "Voltar / avançar 5 s"),
        ("Shift + ←  /  →", "Voltar / avançar 1 s"),
        ("Home", "Ir para o início"),
        ("[", "Marcar ponto A do loop"),
        ("]", "Marcar ponto B do loop"),
        ("\\", "Ligar / desligar o loop"),
        ("+  /  −", "Velocidade ±0,05×"),
        ("↑  /  ↓", "Tom ±1 semitom"),
        ("Ctrl + ↑  /  ↓", "Afinação ±5 cents"),
        ("R", "Voltar velocidade e tom ao normal"),
        ("M", "Adicionar marcador"),
        (",  /  .", "Marcador anterior / próximo"),
        ("Ctrl + O", "Abrir arquivo"),
        ("Ctrl + E", "Exportar"),
        ("F1", "Mostrar esta lista"),
    ];

    /// <summary>Teclas que não geram texto. Devolve verdadeiro se o atalho foi atendido.</summary>
    public static bool HandleKey(MainViewModel vm, Key key, ModifierKeys modifiers)
    {
        bool ctrl = modifiers == ModifierKeys.Control;
        bool shift = modifiers == ModifierKeys.Shift;
        bool none = modifiers == ModifierKeys.None;

        switch (key)
        {
            case Key.Space when none:
                vm.PlayPauseCommand.Execute(null);
                return true;
            case Key.Left when none || shift:
                vm.SeekRelative(-(shift ? FineSeekSeconds : SeekSeconds));
                return true;
            case Key.Right when none || shift:
                vm.SeekRelative(shift ? FineSeekSeconds : SeekSeconds);
                return true;
            case Key.Home when none:
                vm.Seek(0);
                return true;
            case Key.Up when none:
                vm.PitchUpCommand.Execute(null);
                return true;
            case Key.Down when none:
                vm.PitchDownCommand.Execute(null);
                return true;
            case Key.Up when ctrl:
                vm.Cents += CentsStep;
                return true;
            case Key.Down when ctrl:
                vm.Cents -= CentsStep;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Teclas de caractere. Devolve verdadeiro se o atalho foi atendido.</summary>
    public static bool HandleText(MainViewModel vm, string text)
    {
        switch (text.ToLowerInvariant())
        {
            case "[": vm.SetLoopACommand.Execute(null); return true;
            case "]": vm.SetLoopBCommand.Execute(null); return true;
            case "\\": vm.ToggleLoopCommand.Execute(null); return true;
            case "+" or "=": vm.SpeedUpCommand.Execute(null); return true;
            case "-" or "_": vm.SpeedDownCommand.Execute(null); return true;
            case "m": vm.AddMarkerCommand.Execute(null); return true;
            case ",": vm.PreviousMarkerCommand.Execute(null); return true;
            case ".": vm.NextMarkerCommand.Execute(null); return true;
            case "r":
                vm.SpeedResetCommand.Execute(null);
                vm.PitchResetCommand.Execute(null);
                return true;
            default: return false;
        }
    }
}
