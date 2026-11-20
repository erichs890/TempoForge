namespace TempoForge.Core.Dsp;

/// <summary>
/// Filtro de segunda ordem estéreo (forma direta II transposta). Os coeficientes seguem as
/// fórmulas clássicas de equalização paramétrica (peaking, low-shelf, high-shelf).
/// </summary>
public struct Biquad
{
    private float _b0, _b1, _b2, _a1, _a2;
    private float _z1L, _z2L, _z1R, _z2R;

    public void Reset() => _z1L = _z2L = _z1R = _z2R = 0;

    public void SetIdentity()
    {
        _b0 = 1;
        _b1 = _b2 = _a1 = _a2 = 0;
    }

    public void SetPeaking(double frequency, double q, double gainDb)
    {
        double a = Math.Pow(10, gainDb / 40);
        double w = 2 * Math.PI * frequency / AudioFormat.SampleRate;
        double alpha = Math.Sin(w) / (2 * q);
        double cos = Math.Cos(w);
        Normalize(1 + alpha * a, -2 * cos, 1 - alpha * a, 1 + alpha / a, -2 * cos, 1 - alpha / a);
    }

    public void SetLowShelf(double frequency, double gainDb)
    {
        double a = Math.Pow(10, gainDb / 40);
        double w = 2 * Math.PI * frequency / AudioFormat.SampleRate;
        double cos = Math.Cos(w);
        double beta = Math.Sin(w) * Math.Sqrt(2 * a); // inclinação S = 1
        Normalize(
            a * ((a + 1) - (a - 1) * cos + beta),
            2 * a * ((a - 1) - (a + 1) * cos),
            a * ((a + 1) - (a - 1) * cos - beta),
            (a + 1) + (a - 1) * cos + beta,
            -2 * ((a - 1) + (a + 1) * cos),
            (a + 1) + (a - 1) * cos - beta);
    }

    public void SetHighShelf(double frequency, double gainDb)
    {
        double a = Math.Pow(10, gainDb / 40);
        double w = 2 * Math.PI * frequency / AudioFormat.SampleRate;
        double cos = Math.Cos(w);
        double beta = Math.Sin(w) * Math.Sqrt(2 * a);
        Normalize(
            a * ((a + 1) + (a - 1) * cos + beta),
            -2 * a * ((a - 1) + (a + 1) * cos),
            a * ((a + 1) + (a - 1) * cos - beta),
            (a + 1) - (a - 1) * cos + beta,
            2 * ((a - 1) - (a + 1) * cos),
            (a + 1) - (a - 1) * cos - beta);
    }

    public void SetLowPass(double frequency, double q = 0.7071)
    {
        double w = 2 * Math.PI * frequency / AudioFormat.SampleRate;
        double alpha = Math.Sin(w) / (2 * q);
        double cos = Math.Cos(w);
        Normalize((1 - cos) / 2, 1 - cos, (1 - cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
    }

    public void SetHighPass(double frequency, double q = 0.7071)
    {
        double w = 2 * Math.PI * frequency / AudioFormat.SampleRate;
        double alpha = Math.Sin(w) / (2 * q);
        double cos = Math.Cos(w);
        Normalize((1 + cos) / 2, -(1 + cos), (1 + cos) / 2, 1 + alpha, -2 * cos, 1 - alpha);
    }

    private void Normalize(double b0, double b1, double b2, double a0, double a1, double a2)
    {
        _b0 = (float)(b0 / a0);
        _b1 = (float)(b1 / a0);
        _b2 = (float)(b2 / a0);
        _a1 = (float)(a1 / a0);
        _a2 = (float)(a2 / a0);
    }

    /// <summary>Filtra um trecho estéreo intercalado, no próprio buffer.</summary>
    public void Process(Span<float> interleaved)
    {
        float b0 = _b0, b1 = _b1, b2 = _b2, a1 = _a1, a2 = _a2;
        float z1L = _z1L, z2L = _z2L, z1R = _z1R, z2R = _z2R;
        for (int i = 0; i + 1 < interleaved.Length; i += 2)
        {
            float x = interleaved[i];
            float y = b0 * x + z1L;
            z1L = b1 * x - a1 * y + z2L;
            z2L = b2 * x - a2 * y;
            interleaved[i] = y;

            x = interleaved[i + 1];
            y = b0 * x + z1R;
            z1R = b1 * x - a1 * y + z2R;
            z2R = b2 * x - a2 * y;
            interleaved[i + 1] = y;
        }
        // Evita números desnormalizados, que deixam a CPU lenta em silêncio.
        _z1L = Math.Abs(z1L) < 1e-20f ? 0 : z1L;
        _z2L = Math.Abs(z2L) < 1e-20f ? 0 : z2L;
        _z1R = Math.Abs(z1R) < 1e-20f ? 0 : z1R;
        _z2R = Math.Abs(z2R) < 1e-20f ? 0 : z2R;
    }

    /// <summary>Filtra um único canal (usado onde esquerda e direita têm filtros diferentes).</summary>
    public float ProcessLeft(float x)
    {
        float y = _b0 * x + _z1L;
        _z1L = _b1 * x - _a1 * y + _z2L;
        _z2L = _b2 * x - _a2 * y;
        return y;
    }
}
