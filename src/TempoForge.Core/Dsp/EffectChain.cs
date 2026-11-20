namespace TempoForge.Core.Dsp;

/// <summary>
/// Efeitos aplicados depois do estágio de velocidade/tom, sempre na mesma ordem.
/// A reprodução e a exportação usam esta mesma classe, então soam iguais.
/// </summary>
public sealed class EffectChain
{
    private readonly EffectSettings _settings;
    private readonly Equalizer _equalizer = new();
    private readonly Limiter _limiter = new();

    public EffectChain(EffectSettings settings) => _settings = settings;

    public int LatencyFrames => _limiter.LatencyFrames;

    public void Reset()
    {
        _equalizer.Reset();
        _limiter.Reset();
    }

    public void Process(Span<float> interleaved)
    {
        _equalizer.Process(interleaved, _settings);
        _limiter.Process(interleaved);
    }
}
