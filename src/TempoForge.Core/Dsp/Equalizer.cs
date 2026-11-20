namespace TempoForge.Core.Dsp;

/// <summary>
/// Equalizador de 8 bandas com pré-amplificador. A banda mais grave e a mais aguda são
/// prateleiras (shelf); as do meio são sinos (peaking). Ganhos mudam em rampa, sem cliques.
/// </summary>
public sealed class Equalizer
{
    private const int ChunkFrames = 64;
    private const float MaxDbPerChunk = 1.0f;   // ±15 dB em ~40 ms
    private const double MidBandQ = 1.1;

    private readonly Biquad[] _filters = new Biquad[EffectSettings.EqBandCount];
    private readonly float[] _gainsDb = new float[EffectSettings.EqBandCount];
    private float _preampDb;

    public Equalizer()
    {
        for (int i = 0; i < _filters.Length; i++) _filters[i].SetIdentity();
    }

    public void Reset()
    {
        for (int i = 0; i < _filters.Length; i++) _filters[i].Reset();
    }

    public void Process(Span<float> interleaved, EffectSettings settings)
    {
        bool enabled = settings.EqEnabled;
        float preampTarget = enabled ? settings.PreampDb : 0f;

        for (int offset = 0; offset < interleaved.Length; offset += ChunkFrames * 2)
        {
            Span<float> chunk = interleaved.Slice(offset, Math.Min(ChunkFrames * 2, interleaved.Length - offset));

            float preampBefore = _preampDb;
            _preampDb = Approach(_preampDb, preampTarget);
            ApplyPreamp(chunk, preampBefore, _preampDb);

            for (int band = 0; band < _filters.Length; band++)
            {
                float target = enabled ? settings.GetEqGainDb(band) : 0f;
                float current = _gainsDb[band];
                if (current != target)
                {
                    current = Approach(current, target);
                    _gainsDb[band] = current;
                    UpdateBand(band, current);
                }
                // Banda em 0 dB é neutra; pulá-la economiza CPU sem alterar o som.
                if (current != 0f) _filters[band].Process(chunk);
                else _filters[band].Reset();
            }
        }
    }

    private static float Approach(float current, float target)
    {
        float diff = target - current;
        if (Math.Abs(diff) <= MaxDbPerChunk) return target;
        return current + Math.Sign(diff) * MaxDbPerChunk;
    }

    private void UpdateBand(int band, float gainDb)
    {
        float frequency = EffectSettings.EqFrequencies[band];
        if (band == 0) _filters[band].SetLowShelf(frequency, gainDb);
        else if (band == _filters.Length - 1) _filters[band].SetHighShelf(frequency, gainDb);
        else _filters[band].SetPeaking(frequency, MidBandQ, gainDb);
    }

    private static void ApplyPreamp(Span<float> chunk, float fromDb, float toDb)
    {
        if (fromDb == 0f && toDb == 0f) return;
        float from = MathF.Pow(10, fromDb / 20), to = MathF.Pow(10, toDb / 20);
        int frames = chunk.Length / 2;
        float step = (to - from) / Math.Max(1, frames);
        float gain = from;
        for (int i = 0; i < frames; i++)
        {
            gain += step;
            chunk[i * 2] *= gain;
            chunk[i * 2 + 1] *= gain;
        }
    }
}
