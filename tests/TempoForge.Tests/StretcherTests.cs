using TempoForge.Core;
using TempoForge.Core.Dsp;

namespace TempoForge.Tests;

public class StretcherTests
{
    [Theory]
    [InlineData(0.25)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(4.0)]
    public void TempoChangesDurationAndKeepsPitch(double tempo)
    {
        float[] input = StretchHarness.Sine(4.0, 440);
        using var stretcher = new RubberBandStretcher { Tempo = tempo };

        float[] output = StretchHarness.Run(stretcher, input);

        double expectedFrames = 4.0 * AudioFormat.SampleRate / tempo;
        double actualFrames = output.Length / 2.0;
        Assert.InRange(actualFrames, expectedFrames * 0.98, expectedFrames * 1.02);

        // O tom não pode mudar: 440 Hz continua 440 Hz (tolerância de 5 cents).
        int middle = (int)(actualFrames / 2) - 16384;
        double frequency = StretchHarness.DominantFrequency(output, Math.Max(0, middle));
        Assert.InRange(frequency, 440 * Math.Pow(2, -5 / 1200.0), 440 * Math.Pow(2, 5 / 1200.0));
    }

    [Fact]
    public void SweepingTempoWhilePlayingProducesNoClicks()
    {
        float[] input = StretchHarness.Sine(6.0, 440);
        using var stretcher = new RubberBandStretcher();
        var chunk = new float[480 * 2];
        var output = new List<float>();
        int position = 0, totalFrames = input.Length / 2, step = 0;

        while (position < totalFrames)
        {
            // Simula o usuário arrastando o controle: um novo valor a cada bloco de 10 ms.
            stretcher.Tempo = 1.0 + 0.9 * Math.Sin(step++ * 0.05);
            int got = stretcher.Read(chunk, 480);
            if (got > 0)
            {
                output.AddRange(chunk.AsSpan(0, got * 2).ToArray());
                continue;
            }
            int take = Math.Min(Math.Clamp(stretcher.FramesRequired, 256, 1024), totalFrames - position);
            stretcher.Write(input.AsSpan(position * 2, take * 2));
            position += take;
        }

        // Uma senoide de 440 Hz a 0,5 de amplitude varia no máximo ~0,029 por amostra.
        float worst = 0;
        for (int i = 4800; i < output.Count / 2 - 1; i++)
            worst = Math.Max(worst, Math.Abs(output[(i + 1) * 2] - output[i * 2]));
        Assert.InRange(worst, 0.01f, 0.06f);
    }

    [Fact]
    public void ResetStartsCleanAfterASeek()
    {
        using var stretcher = new RubberBandStretcher { Tempo = 0.5 };
        StretchHarness.Run(stretcher, StretchHarness.Sine(1.0, 440));

        stretcher.Reset();
        Assert.Equal(0, stretcher.LatencyFrames);

        float[] output = StretchHarness.Run(stretcher, StretchHarness.Sine(1.0, 440));
        Assert.InRange(output.Length / 2.0, 96_000 * 0.97, 96_000 * 1.03);
    }
}
