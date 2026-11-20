namespace TempoForge.Core.Dsp;

/// <summary>
/// Atenua o que está no centro do estéreo (normalmente a voz principal). O sinal é dividido em
/// três faixas por crossovers Linkwitz-Riley de 4ª ordem, que somam de volta sem alterar o
/// volume; só na faixa do meio um canal é subtraído do outro. Graves e agudos ficam intactos,
/// o que preserva baixo, bumbo e o brilho dos pratos.
/// </summary>
public sealed class VocalReducer
{
    private const int ChunkFrames = 64;
    private const float MixStepPerChunk = 0.05f;         // liga/desliga em ~27 ms
    private const float AmountStepPerChunk = 0.04f;
    private const float MaxFrequencyRatioPerChunk = 1.06f;

    private Crossover _upper;          // separa (graves+médios) de agudos
    private Crossover _lower;          // separa graves de médios
    private Crossover _align;          // alinha a fase dos agudos com as outras duas faixas

    private float _mix;                // 0 = sinal original, 1 = processado
    private float _amount;
    private float _lowCut = 120f, _highCut = 8000f;

    public VocalReducer() => UpdateFilters();

    public void Reset()
    {
        _upper.Reset();
        _lower.Reset();
        _align.Reset();
    }

    public void Process(Span<float> interleaved, EffectSettings settings)
    {
        float mixTarget = settings.VocalReducerEnabled ? 1f : 0f;
        float amountTarget = settings.VocalReducerAmount;
        float lowTarget = settings.VocalReducerLowCutHz;
        float highTarget = Math.Max(settings.VocalReducerHighCutHz, lowTarget * 2);

        Span<float> lowMid = stackalloc float[ChunkFrames * 2];
        Span<float> high = stackalloc float[ChunkFrames * 2];
        Span<float> low = stackalloc float[ChunkFrames * 2];
        Span<float> mid = stackalloc float[ChunkFrames * 2];
        Span<float> highA = stackalloc float[ChunkFrames * 2];
        Span<float> highB = stackalloc float[ChunkFrames * 2];

        for (int offset = 0; offset < interleaved.Length; offset += ChunkFrames * 2)
        {
            Span<float> chunk = interleaved.Slice(offset, Math.Min(ChunkFrames * 2, interleaved.Length - offset));
            int n = chunk.Length;

            float mixBefore = _mix;
            _mix = Approach(_mix, mixTarget, MixStepPerChunk);
            float amountBefore = _amount;
            _amount = Approach(_amount, amountTarget, AmountStepPerChunk);

            if (_lowCut != lowTarget || _highCut != highTarget)
            {
                _lowCut = Glide(_lowCut, lowTarget);
                _highCut = Glide(_highCut, highTarget);
                UpdateFilters();
            }

            if (mixBefore == 0f && _mix == 0f)
            {
                Reset(); // desligado: custo zero e sinal intocado
                continue;
            }

            _upper.Split(chunk, lowMid[..n], high[..n]);
            _lower.Split(lowMid[..n], low[..n], mid[..n]);
            _align.Split(high[..n], highA[..n], highB[..n]);

            int frames = n / 2;
            float mixStep = (_mix - mixBefore) / frames, amountStep = (_amount - amountBefore) / frames;
            float mix = mixBefore, amount = amountBefore;
            for (int i = 0; i < frames; i++)
            {
                mix += mixStep;
                amount += amountStep;
                int l = i * 2, r = l + 1;
                float rest = low[l] + highA[l] + highB[l];
                float wetL = rest + mid[l] - amount * mid[r];
                rest = low[r] + highA[r] + highB[r];
                float wetR = rest + mid[r] - amount * mid[l];
                chunk[l] += (wetL - chunk[l]) * mix;
                chunk[r] += (wetR - chunk[r]) * mix;
            }
        }
    }

    private void UpdateFilters()
    {
        _upper.SetFrequency(_highCut);
        _lower.SetFrequency(_lowCut);
        _align.SetFrequency(_lowCut);
    }

    private static float Approach(float current, float target, float step)
    {
        float diff = target - current;
        return Math.Abs(diff) <= step ? target : current + Math.Sign(diff) * step;
    }

    private static float Glide(float current, float target)
    {
        if (current < target) return Math.Min(target, current * MaxFrequencyRatioPerChunk);
        return Math.Max(target, current / MaxFrequencyRatioPerChunk);
    }

    /// <summary>Crossover Linkwitz-Riley de 4ª ordem: dois passa-baixas e dois passa-altas em série.</summary>
    private struct Crossover
    {
        private Biquad _low1, _low2, _high1, _high2;

        public void SetFrequency(double frequency)
        {
            _low1.SetLowPass(frequency);
            _low2.SetLowPass(frequency);
            _high1.SetHighPass(frequency);
            _high2.SetHighPass(frequency);
        }

        public void Reset()
        {
            _low1.Reset();
            _low2.Reset();
            _high1.Reset();
            _high2.Reset();
        }

        public void Split(ReadOnlySpan<float> input, Span<float> low, Span<float> high)
        {
            input.CopyTo(low);
            input.CopyTo(high);
            _low1.Process(low);
            _low2.Process(low);
            _high1.Process(high);
            _high2.Process(high);
        }
    }
}
