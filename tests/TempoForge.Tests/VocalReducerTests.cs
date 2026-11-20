using TempoForge.Core;
using TempoForge.Core.Dsp;

namespace TempoForge.Tests;

public class VocalReducerTests
{
    private static float[] Tone(double frequency, float left, float right, double seconds = 1.0)
    {
        int frames = (int)(seconds * AudioFormat.SampleRate);
        var data = new float[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            float v = MathF.Sin(2 * MathF.PI * (float)frequency * i / AudioFormat.SampleRate);
            data[i * 2] = v * left;
            data[i * 2 + 1] = v * right;
        }
        return data;
    }

    private static double RmsDb(float[] interleaved, int channel)
    {
        double sum = 0;
        int count = 0;
        for (int i = 24000; i < interleaved.Length / 2; i++, count++)
        {
            float v = interleaved[i * 2 + channel];
            sum += v * v;
        }
        return 10 * Math.Log10(sum / count + 1e-20);
    }

    private static double ChangeDb(EffectSettings settings, double frequency, float left, float right, int channel = 0)
    {
        float[] signal = Tone(frequency, left, right);
        double before = RmsDb(signal, channel);
        new VocalReducer().Process(signal, settings);
        return RmsDb(signal, channel) - before;
    }

    [Fact]
    public void RemovesCenteredSoundInTheVoiceRange()
    {
        var settings = new EffectSettings { VocalReducerEnabled = true };
        Assert.True(ChangeDb(settings, 1000, 0.5f, 0.5f) < -30);
        Assert.True(ChangeDb(settings, 300, 0.5f, 0.5f) < -12);
        Assert.True(ChangeDb(settings, 3000, 0.5f, 0.5f) < -15);
    }

    [Fact]
    public void KeepsBassAndTopEnd()
    {
        var settings = new EffectSettings { VocalReducerEnabled = true };
        Assert.InRange(ChangeDb(settings, 50, 0.5f, 0.5f), -2.0, 0.5);
        Assert.InRange(ChangeDb(settings, 15000, 0.5f, 0.5f), -3.5, 0.5);
    }

    [Fact]
    public void KeepsSoundPannedToOneSide()
    {
        var settings = new EffectSettings { VocalReducerEnabled = true };
        Assert.InRange(ChangeDb(settings, 1000, 0.5f, 0f, channel: 0), -0.2, 0.2);
    }

    [Fact]
    public void AmountControlsHowMuchIsRemoved()
    {
        var settings = new EffectSettings { VocalReducerEnabled = true, VocalReducerAmount = 0.5f };
        Assert.InRange(ChangeDb(settings, 1000, 0.5f, 0.5f), -7.5, -4.5);
    }

    [Fact]
    public void DisabledLeavesTheSignalUntouched()
    {
        float[] signal = Tone(1000, 0.5f, 0.5f);
        float[] original = (float[])signal.Clone();
        new VocalReducer().Process(signal, new EffectSettings());
        Assert.Equal(original, signal);
    }

    [Fact]
    public void TogglingWhilePlayingProducesNoClicks()
    {
        var settings = new EffectSettings();
        var reducer = new VocalReducer();
        float[] signal = Tone(440, 0.5f, 0.5f, 2.0);
        for (int block = 0; block < signal.Length / 960; block++)
        {
            settings.VocalReducerEnabled = block % 20 < 10;
            reducer.Process(signal.AsSpan(block * 960, 960), settings);
        }

        float worst = 0;
        for (int i = 0; i < signal.Length / 2 - 1; i++)
            worst = Math.Max(worst, Math.Abs(signal[(i + 1) * 2] - signal[i * 2]));
        Assert.InRange(worst, 0.01f, 0.04f); // senoide intacta varia até ~0,029 por amostra
    }
}
