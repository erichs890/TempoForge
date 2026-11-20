namespace TempoForge.Core.Dsp;

/// <summary>
/// Limitador de pico estéreo com antecipação de 2 ms. Fica no fim da cadeia para que reforços
/// do equalizador nunca estourem a saída; abaixo do teto ele não altera o sinal.
/// </summary>
public sealed class Limiter
{
    private const int LookaheadFrames = 96;         // 2 ms
    private const float Ceiling = 0.966f;           // −0,3 dBFS

    private readonly float[] _delay = new float[LookaheadFrames * 2];
    private int _index;
    private float _gain = 1f;
    private readonly float _attack = 1f - MathF.Exp(-1f / (0.0004f * AudioFormat.SampleRate));
    private readonly float _release = 1f - MathF.Exp(-1f / (0.120f * AudioFormat.SampleRate));

    /// <summary>Atraso que o limitador acrescenta, em quadros.</summary>
    public int LatencyFrames => LookaheadFrames;

    public void Reset()
    {
        Array.Clear(_delay);
        _index = 0;
        _gain = 1f;
    }

    public void Process(Span<float> interleaved)
    {
        float gain = _gain;
        int index = _index;
        for (int i = 0; i + 1 < interleaved.Length; i += 2)
        {
            float inL = interleaved[i], inR = interleaved[i + 1];
            float peak = Math.Max(Math.Abs(inL), Math.Abs(inR));
            float wanted = peak > Ceiling ? Ceiling / peak : 1f;

            // Ataque rápido (dentro da janela de antecipação), recuperação lenta.
            gain += (wanted - gain) * (wanted < gain ? _attack : _release);

            float outL = _delay[index] * gain, outR = _delay[index + 1] * gain;
            _delay[index] = inL;
            _delay[index + 1] = inR;
            index += 2;
            if (index >= _delay.Length) index = 0;

            interleaved[i] = Math.Clamp(outL, -1f, 1f);
            interleaved[i + 1] = Math.Clamp(outR, -1f, 1f);
        }
        _gain = gain;
        _index = index;
    }
}
