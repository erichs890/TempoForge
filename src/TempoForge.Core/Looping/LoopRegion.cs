namespace TempoForge.Core.Looping;

/// <summary>Trecho A-B a repetir, em quadros do arquivo. Imutável: trocar o loop é trocar a referência.</summary>
public sealed record LoopRegion(long Start, long End)
{
    /// <summary>Menor loop aceito: 50 ms.</summary>
    public const long MinFrames = AudioFormat.SampleRate / 20;

    public double StartSeconds => AudioFormat.FramesToSeconds(Start);
    public double EndSeconds => AudioFormat.FramesToSeconds(End);
}

/// <summary>Ponto nomeado do arquivo para onde o usuário pode saltar.</summary>
public sealed record Marker(double Seconds, string Name);
