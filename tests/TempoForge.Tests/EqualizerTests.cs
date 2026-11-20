using TempoForge.Core.Dsp;

namespace TempoForge.Tests;

public class EqualizerTests
{
    /// <summary>Ganho em dB que o equalizador aplica a uma senoide, medido depois de estabilizar.</summary>
    private static double MeasureGainDb(EffectSettings settings, double frequency)
    {
        var eq = new Equalizer();
        float[] signal = StretchHarness.Sine(2.0, frequency, 0.1f);
        double inputRms = Rms(signal, 48000);
        eq.Process(signal, settings);
        return 20 * Math.Log10(Rms(signal, 48000) / inputRms);
    }

    private static double Rms(float[] interleaved, int fromFrame)
    {
        double sum = 0;
        int count = 0;
        for (int i = fromFrame * 2; i < interleaved.Length; i += 2, count++) sum += interleaved[i] * interleaved[i];
        return Math.Sqrt(sum / count);
    }

    [Fact]
    public void FlatSettingsLeaveTheSignalUntouched()
    {
        var eq = new Equalizer();
        float[] signal = StretchHarness.Sine(0.5, 1000);
        float[] original = (float[])signal.Clone();
        eq.Process(signal, new EffectSettings());
        Assert.Equal(original, signal);
    }

    [Theory]
    [InlineData(0, 30.0)]
    [InlineData(3, 1000.0)]
    [InlineData(5, 4000.0)]
    [InlineData(7, 20000.0)]
    public void EachBandBoostsItsOwnFrequency(int band, double frequency)
    {
        var settings = new EffectSettings();
        settings.SetEqGainDb(band, 12);
        Assert.InRange(MeasureGainDb(settings, frequency), 10.5, 12.5);

        settings.SetEqGainDb(band, -12);
        Assert.InRange(MeasureGainDb(settings, frequency), -12.5, -10.5);
    }

    [Fact]
    public void ABandBarelyAffectsDistantFrequencies()
    {
        var settings = new EffectSettings();
        settings.SetEqGainDb(3, 12); // 1 kHz
        Assert.InRange(MeasureGainDb(settings, 60), -0.5, 0.5);
        Assert.InRange(MeasureGainDb(settings, 12000), -0.5, 0.5);
    }

    [Fact]
    public void PreampAndBypassBehave()
    {
        var settings = new EffectSettings { PreampDb = -6 };
        Assert.InRange(MeasureGainDb(settings, 1000), -6.1, -5.9);

        settings.SetEqGainDb(3, 12);
        settings.EqEnabled = false;
        Assert.InRange(MeasureGainDb(settings, 1000), -0.05, 0.05);
    }

    [Fact]
    public void MovingABandWhilePlayingProducesNoClicks()
    {
        var settings = new EffectSettings();
        var eq = new Equalizer();
        float[] signal = StretchHarness.Sine(3.0, 440);
        for (int block = 0; block < signal.Length / 960; block++)
        {
            settings.SetEqGainDb(2, block % 20 < 10 ? 15 : -15); // saltos bruscos a cada 100 ms
            settings.SetEqGainDb(1, (float)(15 * Math.Sin(block * 0.3)));
            eq.Process(signal.AsSpan(block * 960, 960), settings);
        }

        // Com +15 dB a senoide chega a ~2,8 de pico: variação máxima legítima ≈ 0,16 por amostra.
        float worst = 0;
        for (int i = 0; i < signal.Length / 2 - 1; i++)
            worst = Math.Max(worst, Math.Abs(signal[(i + 1) * 2] - signal[i * 2]));
        Assert.InRange(worst, 0.02f, 0.25f);
    }

    [Fact]
    public void LimiterKeepsBoostedAudioBelowFullScale()
    {
        var settings = new EffectSettings { PreampDb = 15 };
        var chain = new EffectChain(settings);
        float[] signal = StretchHarness.Sine(1.0, 220, 0.9f);
        chain.Process(signal);

        float peak = signal.Max(Math.Abs);
        Assert.InRange(peak, 0.8f, 1.0f);

        // Abaixo do teto o limitador não mexe em nada além do atraso fixo.
        var quiet = new EffectChain(new EffectSettings());
        float[] soft = StretchHarness.Sine(0.5, 220, 0.5f);
        float[] expected = (float[])soft.Clone();
        quiet.Process(soft);
        int delay = quiet.LatencyFrames;
        for (int i = 4800; i < 9600; i++)
            Assert.Equal(expected[(i - delay) * 2], soft[i * 2], 4);
    }
}
