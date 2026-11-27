using System.Globalization;
using System.Windows;

namespace TempoForge.App.Localization;

/// <summary>
/// Textos da interface em português e inglês. O XAML usa <c>{DynamicResource S.Chave}</c>;
/// o código usa <see cref="T"/> e <see cref="F"/>. Trocar o idioma atualiza tudo na hora.
/// </summary>
public static class Loc
{
    public const string Portuguese = "pt", English = "en";

    private static readonly Dictionary<string, (string Pt, string En)> Strings = new()
    {
        ["Recents"] = ("Recentes", "Recent"),
        ["Library"] = ("Biblioteca", "Library"),
        ["RecentsEmpty"] = ("Os arquivos que você abrir aparecem aqui. Também dá para arrastar um arquivo para a janela.", "Files you open show up here. You can also drag a file onto the window."),
        ["AddFolder"] = ("+ Pasta", "+ Folder"),
        ["AddFolderTip"] = ("Adicionar uma pasta de músicas", "Add a music folder"),
        ["FilterTip"] = ("Filtrar por nome", "Filter by name"),
        ["RemoveFolderTip"] = ("Remover esta pasta da biblioteca (não apaga arquivos)", "Remove this folder from the library (no files are deleted)"),
        ["ThemeDark"] = ("Tema escuro", "Dark theme"),
        ["ThemeLight"] = ("Tema claro", "Light theme"),
        ["ThemeTip"] = ("Alternar entre tema claro e escuro", "Switch between light and dark theme"),
        ["LanguageTip"] = ("Idioma", "Language"),
        ["Shortcuts"] = ("Atalhos", "Shortcuts"),
        ["ShortcutsTip"] = ("Ver os atalhos de teclado  (F1)", "Show keyboard shortcuts  (F1)"),
        ["Export"] = ("Exportar…", "Export…"),
        ["ExportTip"] = ("Salvar o áudio com os ajustes atuais  (Ctrl+E)", "Save the audio with the current settings  (Ctrl+E)"),
        ["Open"] = ("Abrir arquivo…", "Open file…"),
        ["ZoomOutTip"] = ("Reduzir o zoom  (roda do mouse)", "Zoom out  (mouse wheel)"),
        ["ZoomInTip"] = ("Ampliar a faixa  (roda do mouse; Shift + roda desloca)", "Zoom in  (mouse wheel; Shift + wheel scrolls)"),
        ["ZoomLoopTip"] = ("Ampliar o trecho entre A e B", "Zoom to the A–B section"),
        ["ZoomAll"] = ("Tudo", "All"),
        ["ZoomAllTip"] = ("Mostrar a música inteira", "Show the whole song"),
        ["EmptyTitle"] = ("Arraste uma música para cá", "Drag a song here"),
        ["EmptyHint"] = ("ou use “Abrir arquivo…” (Ctrl+O). MP3, M4A, FLAC, WAV e OGG.", "or use “Open file…” (Ctrl+O). MP3, M4A, FLAC, WAV and OGG."),
        ["LoopATip"] = ("Marcar início do loop aqui  ( [ )", "Set loop start here  ( [ )"),
        ["LoopBTip"] = ("Marcar fim do loop aqui  ( ] )", "Set loop end here  ( ] )"),
        ["LoopToggleTip"] = ("Ligar ou desligar a repetição do trecho A-B  ( \\ )", "Turn A-B repeat on or off  ( \\ )"),
        ["Clear"] = ("Limpar", "Clear"),
        ["ClearTip"] = ("Apagar os pontos A e B", "Remove points A and B"),
        ["BackTip"] = ("Voltar 5 segundos  ( ← )", "Back 5 seconds  ( ← )"),
        ["PlayTip"] = ("Tocar ou pausar  (Espaço)", "Play or pause  (Space)"),
        ["ForwardTip"] = ("Avançar 5 segundos  ( → )", "Forward 5 seconds  ( → )"),
        ["NextMarkerTip"] = ("Próximo marcador  ( . )", "Next marker  ( . )"),
        ["AddMarker"] = ("+ Marcador", "+ Marker"),
        ["AddMarkerTip"] = ("Adicionar marcador aqui  ( M )", "Add a marker here  ( M )"),
        ["PrevMarkerTip"] = ("Marcador anterior  ( , )", "Previous marker  ( , )"),
        ["MarkerChipTip"] = ("Clique para ir até aqui; botão direito para remover", "Click to jump here; right-click to remove"),
        ["TypeValueTip"] = ("Clique para digitar um valor (Enter confirma, Esc cancela)", "Click to type a value (Enter confirms, Esc cancels)"),
        ["KeyClickValue"] = ("Clique em um valor", "Click a value"),
        ["HelpTypeValue"] = ("Digitar velocidade ou ganho", "Type a speed or gain"),
        ["VolumeTip"] = ("Volume do app (duplo clique volta a 100%)", "App volume (double-click returns to 100%)"),
        ["Normal"] = ("Normal", "Normal"),
        ["Speed"] = ("Velocidade", "Speed"),
        ["SlowerTip"] = ("Mais devagar  ( − )", "Slower  ( − )"),
        ["FasterTip"] = ("Mais rápido  ( + )", "Faster  ( + )"),
        ["SpeedMin"] = ("0,25×", "0.25×"),
        ["Pitch"] = ("Tom", "Pitch"),
        ["SemitoneDownTip"] = ("Um semitom abaixo  ( ↓ )", "One semitone down  ( ↓ )"),
        ["SemitoneUpTip"] = ("Um semitom acima  ( ↑ )", "One semitone up  ( ↑ )"),
        ["CentsTip"] = ("Afinação fina, em cents  ( Ctrl + ↑ / ↓ )", "Fine tuning, in cents  ( Ctrl + ↑ / ↓ )"),
        ["SemitonesCaption"] = ("semitons (±12)", "semitones (±12)"),
        ["Equalizer"] = ("Equalizador", "Equalizer"),
        ["Reset"] = ("Zerar", "Reset"),
        ["PresetsTip"] = ("Predefinições", "Presets"),
        ["DoubleClickReset"] = ("Duplo clique para zerar", "Double-click to reset"),
        ["Preamp"] = ("Pré-amp", "Preamp"),
        ["PresetFlat"] = ("Plano", "Flat"),
        ["PresetMoreBass"] = ("Mais graves", "More bass"),
        ["PresetLessBass"] = ("Menos graves", "Less bass"),
        ["PresetBright"] = ("Mais brilho", "Brighter"),
        ["PresetVoice"] = ("Voz em destaque", "Vocals forward"),
        ["PresetInstrument"] = ("Instrumento em destaque", "Instrument forward"),
        ["PresetLowVolume"] = ("Volume baixo", "Low volume"),
        ["ReduceVocals"] = ("Reduzir voz", "Reduce vocals"),
        ["ReduceVocalsTip"] = ("Atenua o que está no centro do estéreo. Funciona melhor em gravações estéreo de estúdio.", "Attenuates what sits in the stereo centre. Works best on stereo studio recordings."),
        ["Intensity"] = ("Intensidade", "Amount"),
        ["KeepLows"] = ("Preservar graves abaixo de", "Keep bass below"),
        ["KeepHighs"] = ("Preservar agudos acima de", "Keep treble above"),
        ["DropToOpen"] = ("Solte para abrir", "Drop to open"),
        ["NoFile"] = ("Nenhum arquivo aberto", "No file open"),
        ["NoLoop"] = ("Sem loop", "No loop"),
        ["LoopAAt"] = ("A em {0}", "A at {0}"),
        ["DescSpeed"] = ("velocidade {0}", "speed {0}"),
        ["DescPitch"] = ("tom {0}", "pitch {0}"),
        ["DescEq"] = ("equalizador ligado", "equalizer on"),
        ["DescVocal"] = ("redução de voz ligada", "vocal reduction on"),
        ["DescPrefix"] = ("O arquivo será gravado como você está ouvindo: {0}.", "The file will be saved the way you hear it: {0}."),
        ["LibSearching"] = ("Procurando músicas…", "Looking for songs…"),
        ["LibAddFolder"] = ("Adicione uma pasta para ver suas músicas aqui.", "Add a folder to see your songs here."),
        ["LibNone"] = ("Nenhum arquivo de áudio nessas pastas.", "No audio files in these folders."),
        ["LibCountFiltered"] = ("{0} de {1} músicas", "{0} of {1} songs"),
        ["LibCount"] = ("{0} músicas", "{0} songs"),
        ["OpenTitle"] = ("Abrir áudio", "Open audio"),
        ["FilterAudio"] = ("Arquivos de áudio", "Audio files"),
        ["FilterAll"] = ("Todos os arquivos", "All files"),
        ["FileGone"] = ("Este arquivo não existe mais:\n\n{0}", "This file no longer exists:\n\n{0}"),
        ["OpenFailed"] = ("Não foi possível abrir este arquivo.\n\n{0}", "This file could not be opened.\n\n{0}"),
        ["PickFolder"] = ("Escolha uma pasta de músicas", "Choose a music folder"),
        ["ExportTitle"] = ("Exportar áudio", "Export audio"),
        ["Format"] = ("Formato", "Format"),
        ["WavOption"] = ("WAV (16 bits, sem perdas)", "WAV (16-bit, lossless)"),
        ["Mp3Quality"] = ("Qualidade do MP3", "MP3 quality"),
        ["RangeOnly"] = ("Exportar só o trecho A-B", "Export only the A-B section"),
        ["Close"] = ("Fechar", "Close"),
        ["Cancel"] = ("Cancelar", "Cancel"),
        ["ExportSaveTitle"] = ("Salvar áudio exportado", "Save exported audio"),
        ["ExportSameFile"] = ("Escolha outro nome: não é possível gravar por cima do arquivo que está aberto.", "Choose another name: the open file cannot be overwritten."),
        ["Exporting"] = ("Exportando…", "Exporting…"),
        ["ExportDone"] = ("Pronto: {0}", "Done: {0}"),
        ["ExportCancelled"] = ("Exportação cancelada.", "Export cancelled."),
        ["ExportFailed"] = ("Não foi possível exportar. {0}", "The export failed. {0}"),
        ["ShortcutsTitle"] = ("Atalhos de teclado", "Keyboard shortcuts"),
        ["KeySpace"] = ("Espaço", "Space"),
        ["KeyWheel"] = ("Roda do mouse na faixa", "Mouse wheel on the track"),
        ["KeyShiftWheel"] = ("Shift + roda na faixa", "Shift + wheel on the track"),
        ["KeyEqDouble"] = ("Duplo clique no equalizador", "Double-click on the equalizer"),
        ["HelpPlay"] = ("Tocar / pausar", "Play / pause"),
        ["HelpSeek5"] = ("Voltar / avançar 5 s", "Back / forward 5 s"),
        ["HelpSeek1"] = ("Voltar / avançar 1 s", "Back / forward 1 s"),
        ["HelpHome"] = ("Ir para o início", "Go to the start"),
        ["HelpLoopA"] = ("Marcar ponto A do loop", "Set loop point A"),
        ["HelpLoopB"] = ("Marcar ponto B do loop", "Set loop point B"),
        ["HelpLoopToggle"] = ("Ligar / desligar o loop", "Turn the loop on / off"),
        ["HelpSpeed"] = ("Velocidade ±0,01×", "Speed ±0.01×"),
        ["HelpPitch"] = ("Tom ±1 semitom", "Pitch ±1 semitone"),
        ["HelpCents"] = ("Afinação ±5 cents", "Tuning ±5 cents"),
        ["HelpResetAll"] = ("Voltar velocidade e tom ao normal", "Reset speed and pitch"),
        ["HelpMarker"] = ("Adicionar marcador", "Add a marker"),
        ["HelpMarkerNav"] = ("Marcador anterior / próximo", "Previous / next marker"),
        ["HelpZoom"] = ("Ampliar / reduzir o zoom", "Zoom in / out"),
        ["HelpPan"] = ("Deslocar a faixa ampliada", "Scroll the zoomed track"),
        ["HelpEqReset"] = ("Zerar a banda", "Reset the band"),
        ["HelpOpen"] = ("Abrir arquivo", "Open a file"),
        ["HelpExport"] = ("Exportar", "Export"),
        ["HelpThis"] = ("Mostrar esta lista", "Show this list"),
        ["UpdChecking"] = ("Verificando atualizações…", "Checking for updates…"),
        ["UpdDownloading"] = ("Baixando a versão {0}…", "Downloading version {0}…"),
        ["UpdInstalling"] = ("Instalando a atualização…", "Installing the update…"),
        ["UpdSkip"] = ("Abrir sem atualizar", "Open without updating"),
    };

    private static ResourceDictionary? _dictionary;

    /// <summary>Idioma em uso: "pt" ou "en".</summary>
    public static string Language { get; private set; } = Portuguese;

    /// <summary>O idioma mudou; quem monta textos em código deve refazê-los.</summary>
    public static event Action? Changed;

    public static IEnumerable<string> Keys => Strings.Keys;

    /// <summary>Idioma inicial quando o usuário ainda não escolheu: o do Windows, se for português.</summary>
    public static string DefaultLanguage =>
        CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "pt" ? Portuguese : English;

    public static string T(string key, string? language = null) =>
        Strings.TryGetValue(key, out var text) ? ((language ?? Language) == English ? text.En : text.Pt) : key;

    public static string F(string key, params object?[] args) => string.Format(T(key), args);

    /// <summary>Aplica o idioma: textos, formato de números e os recursos usados pelo XAML.</summary>
    public static void Apply(string? language)
    {
        Language = language == English ? English : Portuguese;

        var culture = CultureInfo.GetCultureInfo(Language == English ? "en-US" : "pt-BR");
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        if (Application.Current is { } app)
        {
            var dictionary = new ResourceDictionary();
            foreach (string key in Strings.Keys) dictionary["S." + key] = T(key);
            if (_dictionary != null) app.Resources.MergedDictionaries.Remove(_dictionary);
            app.Resources.MergedDictionaries.Add(dictionary);
            _dictionary = dictionary;
        }

        Changed?.Invoke();
    }
}
