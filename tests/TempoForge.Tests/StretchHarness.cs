using TempoForge.Core;
using TempoForge.Core.Dsp;

namespace TempoForge.Tests;

/// <summary>Passa um sinal inteiro por um estágio de stretch, do mesmo jeito que o motor faz.</summary>
public static class StretchHarness
{
    public static float[] Sine(double seconds, double frequency, float amplitude = 0.5f)
    {
        int frames = (int)(seconds * AudioFormat.SampleRate);
        var data = new float[frames * 2];
        for (int i = 0; i < frames; i++)
        {
            float v = amplitude * MathF.Sin(2 * MathF.PI * (float)frequency * i / AudioFormat.SampleRate);
            data[i * 2] = v;
            data[i * 2 + 1] = v;
        }
        return data;
    }

    public static float[] Run(ITimeStretcher stretcher, float[] input)
    {
        var output = new List<float>(input.Length * 4);
        var chunk = new float[1024 * 2];
        var silence = new float[1024 * 2];
        int position = 0;
        int totalFrames = input.Length / 2;
        int drain = -1;

        while (true)
        {
            int got = stretcher.Read(chunk, 1024);
            if (got > 0)
            {
                output.AddRange(chunk.AsSpan(0, got * 2).ToArray());
                continue;
            }

            int wanted = Math.Clamp(stretcher.FramesRequired, 256, 1024);
            int take = Math.Min(wanted, totalFrames - position);
            if (take > 0)
            {
                stretcher.Write(input.AsSpan(position * 2, take * 2));
                position += take;
                continue;
            }

            if (drain < 0) drain = stretcher.LatencyFrames;
            if (drain == 0) break;
            int n = Math.Min(drain, 1024);
            stretcher.Write(silence.AsSpan(0, n * 2));
            drain -= n;
        }
        return output.ToArray();
    }

    /// <summary>Frequência dominante do canal esquerdo, por FFT com janela de Hann e interpolação parabólica.</summary>
    public static double DominantFrequency(float[] interleaved, int startFrame, int size = 32768)
    {
        var re = new double[size];
        var im = new double[size];
        for (int i = 0; i < size; i++)
        {
            double window = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / (size - 1));
            re[i] = interleaved[(startFrame + i) * 2] * window;
        }
        Fft(re, im);

        int best = 1;
        double bestMag = 0;
        for (int k = 1; k < size / 2; k++)
        {
            double mag = re[k] * re[k] + im[k] * im[k];
            if (mag > bestMag) { bestMag = mag; best = k; }
        }

        double Mag(int k) => Math.Log(Math.Sqrt(re[k] * re[k] + im[k] * im[k]) + 1e-12);
        double a = Mag(best - 1), b = Mag(best), c = Mag(best + 1);
        double shift = 0.5 * (a - c) / (a - 2 * b + c);
        return (best + shift) * AudioFormat.SampleRate / size;
    }

    private static void Fft(double[] re, double[] im)
    {
        int n = re.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            int bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }
        for (int len = 2; len <= n; len <<= 1)
        {
            double angle = -2 * Math.PI / len;
            double wr = Math.Cos(angle), wi = Math.Sin(angle);
            for (int i = 0; i < n; i += len)
            {
                double cr = 1, ci = 0;
                for (int k = 0; k < len / 2; k++)
                {
                    int u = i + k, v = i + k + len / 2;
                    double tr = re[v] * cr - im[v] * ci;
                    double ti = re[v] * ci + im[v] * cr;
                    re[v] = re[u] - tr;
                    im[v] = im[u] - ti;
                    re[u] += tr;
                    im[u] += ti;
                    double ncr = cr * wr - ci * wi;
                    ci = cr * wi + ci * wr;
                    cr = ncr;
                }
            }
        }
    }
}
