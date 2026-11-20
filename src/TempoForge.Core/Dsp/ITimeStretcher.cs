namespace TempoForge.Core.Dsp;

/// <summary>
/// Estágio que muda duração e/ou altura do áudio. Todas as chamadas vêm de uma única
/// thread (a de áudio, ou a de exportação) e não podem alocar nem bloquear.
/// Os buffers são estéreo intercalados.
/// </summary>
public interface ITimeStretcher : IDisposable
{
    /// <summary>Velocidade de reprodução: 1 = original, 2 = dobro da velocidade.</summary>
    double Tempo { get; set; }

    /// <summary>Fator de altura: 1 = original, 2 = uma oitava acima.</summary>
    double PitchScale { get; set; }

    /// <summary>Quantos quadros de entrada o estágio quer receber antes de produzir saída.</summary>
    int FramesRequired { get; }

    /// <summary>Atraso interno, em quadros de entrada, entre o que entrou e o que já saiu.</summary>
    int LatencyFrames { get; }

    void Write(ReadOnlySpan<float> interleaved);

    /// <summary>Copia até <paramref name="maxFrames"/> quadros prontos. Devolve quantos copiou.</summary>
    int Read(Span<float> interleaved, int maxFrames);

    /// <summary>Descarta todo o estado interno (após um seek).</summary>
    void Reset();
}
