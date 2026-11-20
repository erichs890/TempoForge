namespace TempoForge.Core.Dsp;

/// <summary>Estágio neutro: entrega o áudio como entrou. Usado em testes e como reserva.</summary>
public sealed class PassthroughStretcher : ITimeStretcher
{
    private const int CapacityFrames = 16384;
    private readonly float[] _fifo = new float[CapacityFrames * AudioFormat.Channels];
    private int _count; // em valores (não quadros)

    public double Tempo { get; set; } = 1.0;
    public double PitchScale { get; set; } = 1.0;
    public int FramesRequired => _count == 0 ? 1024 : 0;
    public int LatencyFrames => 0;

    public void Write(ReadOnlySpan<float> interleaved)
    {
        int n = Math.Min(interleaved.Length, _fifo.Length - _count);
        interleaved[..n].CopyTo(_fifo.AsSpan(_count));
        _count += n;
    }

    public int Read(Span<float> interleaved, int maxFrames)
    {
        int n = Math.Min(_count, Math.Min(maxFrames * AudioFormat.Channels, interleaved.Length));
        if (n <= 0) return 0;
        _fifo.AsSpan(0, n).CopyTo(interleaved);
        _count -= n;
        if (_count > 0) Array.Copy(_fifo, n, _fifo, 0, _count);
        return n / AudioFormat.Channels;
    }

    public void Reset() => _count = 0;

    public void Dispose() { }
}
