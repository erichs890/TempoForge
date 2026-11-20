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

    public float GetEqGainDb(int band) => Volatile.Read(ref _eqGainsDb[band]);

    public void SetEqGainDb(int band, float gainDb) =>
        Volatile.Write(ref _eqGainsDb[band], Math.Clamp(gainDb, -EqMaxDb, EqMaxDb));
}
