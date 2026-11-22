using TempoForge.App.Input;
using TempoForge.App.ViewModels;
using TempoForge.Core.Dsp;

namespace TempoForge.Tests;

public sealed class ValueInputTests
{
    [Theory]
    [InlineData("0,75", 0.75)]
    [InlineData("1.25x", 1.25)]
    [InlineData(" 1,333× ", 1.33)]
    [InlineData("75%", 0.75)]
    [InlineData("150 %", 1.5)]
    [InlineData("9", 4.0)]      // acima do máximo: fica no limite
    [InlineData("0.1", 0.25)]   // abaixo do mínimo
    public void ParsesSpeed(string text, double expected)
    {
        Assert.True(ValueInput.TryParseSpeed(text, 0.25, 4.0, out double speed));
        Assert.Equal(expected, speed, 6);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1,2,3")]
    public void RejectsInvalidSpeed(string text) =>
        Assert.False(ValueInput.TryParseSpeed(text, 0.25, 4.0, out _));

    [Theory]
    [InlineData("2,37", 2.37)]
    [InlineData("+2.375 dB", 2.38)]
    [InlineData("−4,5", -4.5)]
    [InlineData("-4.5db", -4.5)]
    [InlineData("0", 0)]
    [InlineData("40", 15)]
    [InlineData("-40", -15)]
    public void ParsesDecibels(string text, double expected)
    {
        Assert.True(ValueInput.TryParseDecibels(text, -15, 15, out double db));
        Assert.Equal(expected, db, 6);
    }

    [Fact]
    public void EqualizerBandAcceptsTypedGainAndIgnoresGarbage()
    {
        var settings = new EffectSettings();
        var eq = new EqualizerViewModel(settings);
        EqBandViewModel band = eq.Bands[3];

        band.GainInput = "2,37";
        Assert.Equal(2.37, band.GainDb, 6);
        Assert.Null(eq.SelectedPreset); // curva digitada não é nenhuma predefinição

        band.GainInput = "banana";
        Assert.Equal(2.37, band.GainDb, 6);

        band.GainInput = "0";
        Assert.Equal(eq.Presets[0], eq.SelectedPreset); // tudo zerado volta a "Plano"

        eq.PreampInput = "-3.25 dB";
        Assert.Equal(-3.25, eq.PreampDb, 6);
    }
}
