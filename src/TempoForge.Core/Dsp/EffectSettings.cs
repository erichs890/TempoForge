namespace TempoForge.Core.Dsp;

/// <summary>
/// Valores-alvo dos efeitos. A UI escreve aqui a qualquer momento; a thread de áudio lê a cada
/// bloco e caminha suavemente até o alvo. Só há tipos simples, lidos e escritos de forma atômica.
/// </summary>
public sealed class EffectSettings
{
    public const int EqBandCount = 8;
    public const float EqMaxDb = 15f;

    /// <summary>Frequência central de cada banda, em Hz.</summary>
    public static readonly float[] EqFrequencies = [60, 150, 400, 1000, 2000, 4000, 8000, 16000];

    private readonly float[] _eqGainsDb = new float[EqBandCount];
    private volatile bool _eqEnabled = true;
    private float _preampDb;

    public bool EqEnabled { get => _eqEnabled; set => _eqEnabled = value; }

    /// <summary>Ganho geral antes do equalizador, em dB (−15 a +15).</summary>
    public float PreampDb
    {
        get => Volatile.Read(ref _preampDb);
        set => Volatile.Write(ref _preampDb, Math.Clamp(value, -EqMaxDb, EqMaxDb));
    }

    private volatile bool _vocalReducerEnabled;
    private float _vocalReducerAmount = 1f;
    private float _vocalReducerLowCutHz = 120f;
    private float _vocalReducerHighCutHz = 8000f;

    public bool VocalReducerEnabled { get => _vocalReducerEnabled; set => _vocalReducerEnabled = value; }

    /// <summary>Intensidade da redução: 0 = nada, 1 = cancelamento total do centro.</summary>
    public float VocalReducerAmount
    {
        get => Volatile.Read(ref _vocalReducerAmount);
        set => Volatile.Write(ref _vocalReducerAmount, Math.Clamp(value, 0f, 1f));
    }

    /// <summary>Abaixo desta frequência nada é removido (20 a 400 Hz).</summary>
    public float VocalReducerLowCutHz
    {
        get => Volatile.Read(ref _vocalReducerLowCutHz);
        set => Volatile.Write(ref _vocalReducerLowCutHz, Math.Clamp(value, 20f, 400f));
    }

    /// <summary>Acima desta frequência nada é removido (1 a 16 kHz).</summary>
    public float VocalReducerHighCutHz
    {
        get => Volatile.Read(ref _vocalReducerHighCutHz);
        set => Volatile.Write(ref _vocalReducerHighCutHz, Math.Clamp(value, 1000f, 16000f));
    }

    public float GetEqGainDb(int band) => Volatile.Read(ref _eqGainsDb[band]);

    public void SetEqGainDb(int band, float gainDb) =>
        Volatile.Write(ref _eqGainsDb[band], Math.Clamp(gainDb, -EqMaxDb, EqMaxDb));
}
