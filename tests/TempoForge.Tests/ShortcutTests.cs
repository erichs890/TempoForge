using System.Windows.Input;
using TempoForge.App.Input;
using TempoForge.App.ViewModels;

namespace TempoForge.Tests;

/// <summary>
/// Exercita o mapa de atalhos contra o ViewModel real (com o motor de áudio tocando um
/// arquivo quase mudo), sem depender de uma janela.
/// </summary>
[Trait("Category", "Device")]
public sealed class ShortcutTests : IDisposable
{
    private readonly string _data = Path.Combine(Path.GetTempPath(), $"tempoforge-vm-{Guid.NewGuid():N}");
    private readonly TempWave _wave = TempWave.Sine(30.0, 440, amplitude: 0.001f);
    private readonly MainViewModel _vm;

    public ShortcutTests()
    {
        Environment.SetEnvironmentVariable("TEMPOFORGE_DATA", _data);
        _vm = new MainViewModel();
        _vm.OpenFile(_wave.Path, autoPlay: false);
    }

    public void Dispose()
    {
        _vm.Dispose();
        _wave.Dispose();
        Environment.SetEnvironmentVariable("TEMPOFORGE_DATA", null);
        try { Directory.Delete(_data, recursive: true); } catch (IOException) { }
    }

    private void Key(Key key, ModifierKeys modifiers = ModifierKeys.None) =>
        Assert.True(Shortcuts.HandleKey(_vm, key, modifiers));

    private void Text(string text) => Assert.True(Shortcuts.HandleText(_vm, text));

    /// <summary>Espera o motor concluir um seek e publicar a posição.</summary>
    private double SettledPosition()
    {
        Thread.Sleep(250);
        _vm.Tick();
        return _vm.Position;
    }

    [Fact]
    public void SpaceTogglesPlayback()
    {
        Assert.False(_vm.IsPlaying);
        Key(System.Windows.Input.Key.Space);
        Assert.True(_vm.IsPlaying);
        Key(System.Windows.Input.Key.Space);
        Assert.False(_vm.IsPlaying);
    }

    [Fact]
    public void ArrowsSeekAndHomeRewinds()
    {
        Key(System.Windows.Input.Key.Right);
        Key(System.Windows.Input.Key.Right);
        Assert.Equal(10.0, SettledPosition(), 1);

        Key(System.Windows.Input.Key.Left, ModifierKeys.Shift);
        Assert.Equal(9.0, SettledPosition(), 1);

        Key(System.Windows.Input.Key.Left);
        Assert.Equal(4.0, SettledPosition(), 1);

        Key(System.Windows.Input.Key.Home);
        Assert.Equal(0.0, SettledPosition(), 1);
    }

    [Fact]
    public void PlusAndMinusChangeSpeedWithinLimits()
    {
        Text("+");
        Text("=");
        Assert.Equal(1.10, _vm.Speed, 2);
        Text("-");
        Assert.Equal(1.05, _vm.Speed, 2);

        for (int i = 0; i < 100; i++) Text("-");
        Assert.Equal(0.25, _vm.Speed, 2);
        for (int i = 0; i < 200; i++) Text("+");
        Assert.Equal(4.00, _vm.Speed, 2);
    }

    [Fact]
    public void ArrowsChangePitchAndCents()
    {
        Key(System.Windows.Input.Key.Up);
        Key(System.Windows.Input.Key.Up);
        Key(System.Windows.Input.Key.Down, ModifierKeys.Control);
        Assert.Equal(2, _vm.Semitones);
        Assert.Equal(-5, _vm.Cents);

        for (int i = 0; i < 30; i++) Key(System.Windows.Input.Key.Down);
        Assert.Equal(-12, _vm.Semitones);

        Text("r");
        Assert.Equal(0, _vm.Semitones);
        Assert.Equal(0, _vm.Cents);
        Assert.Equal(1.0, _vm.Speed);
    }

    [Fact]
    public void BracketsSetTheLoopAndBackslashTogglesIt()
    {
        _vm.Seek(5);
        SettledPosition();
        Text("[");
        Assert.Equal(5.0, _vm.LoopA, 1);
        Assert.False(_vm.IsLooping); // só A: ainda não há trecho

        _vm.Seek(8);
        SettledPosition();
        Text("]");
        Assert.Equal(8.0, _vm.LoopB, 1);
        Assert.True(_vm.IsLooping);

        Text("\\");
        Assert.False(_vm.IsLooping);
        Assert.Equal(8.0, _vm.LoopB, 1); // os pontos continuam guardados
        Text("\\");
        Assert.True(_vm.IsLooping);
    }

    [Fact]
    public void MarkersCanBeAddedAndNavigated()
    {
        _vm.Seek(6);
        SettledPosition();
        Text("m");
        _vm.Seek(15);
        SettledPosition();
        Text("m");
        Text("m"); // mesma posição: não duplica
        Assert.Equal(2, _vm.Markers.Count);

        Key(System.Windows.Input.Key.Home);
        SettledPosition();
        Text(".");
        Assert.Equal(6.0, SettledPosition(), 1);
        Text(".");
        Assert.Equal(15.0, SettledPosition(), 1);
        Text(",");
        Assert.Equal(6.0, SettledPosition(), 1);
    }

    [Fact]
    public void UnmappedKeysAreIgnored()
    {
        Assert.False(Shortcuts.HandleKey(_vm, System.Windows.Input.Key.F5, ModifierKeys.None));
        Assert.False(Shortcuts.HandleKey(_vm, System.Windows.Input.Key.Left, ModifierKeys.Alt));
        Assert.False(Shortcuts.HandleText(_vm, "q"));
    }

    [Fact]
    public void TrackAdjustmentsComeBackWhenTheFileIsReopened()
    {
        Text("-");
        Key(System.Windows.Input.Key.Up);
        _vm.Seek(5);
        SettledPosition();
        Text("[");
        _vm.Seek(9);
        SettledPosition();
        Text("]");
        Text("m");
        _vm.Equalizer.Bands[1].GainDb = 4.5;
        _vm.SaveNow();

        using var reopened = new MainViewModel();
        reopened.OpenFile(_wave.Path, autoPlay: false);
        Assert.Equal(0.95, reopened.Speed, 2);
        Assert.Equal(1, reopened.Semitones);
        Assert.Equal(5.0, reopened.LoopA, 1);
        Assert.Equal(9.0, reopened.LoopB, 1);
        Assert.True(reopened.IsLooping);
        Assert.Single(reopened.Markers);
        Assert.Equal(4.5, reopened.Equalizer.Bands[1].GainDb);
        Assert.Single(reopened.Recents);
    }
}
