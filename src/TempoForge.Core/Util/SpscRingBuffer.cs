namespace TempoForge.Core.Util;

/// <summary>
/// Fila circular de floats para exatamente um produtor e um consumidor, sem locks.
/// O produtor só avança o índice de escrita; o consumidor só avança o de leitura.
/// </summary>
public sealed class SpscRingBuffer
{
    private readonly float[] _buffer;
    private readonly int _mask;
    private long _write;
    private long _read;

    public SpscRingBuffer(int capacity)
    {
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
            throw new ArgumentException("A capacidade precisa ser potência de 2.", nameof(capacity));
        _buffer = new float[capacity];
        _mask = capacity - 1;
    }

    public int Capacity => _buffer.Length;

    public int AvailableToRead => (int)(Volatile.Read(ref _write) - Volatile.Read(ref _read));

    public int AvailableToWrite => _buffer.Length - AvailableToRead;

    /// <summary>Chamado só pelo produtor. Devolve quantos valores couberam.</summary>
    public int Write(ReadOnlySpan<float> source)
    {
        long write = _write;
        int free = _buffer.Length - (int)(write - Volatile.Read(ref _read));
        int count = Math.Min(free, source.Length);
        if (count <= 0) return 0;

        int start = (int)(write & _mask);
        int first = Math.Min(count, _buffer.Length - start);
        source[..first].CopyTo(_buffer.AsSpan(start, first));
        if (count > first) source.Slice(first, count - first).CopyTo(_buffer);

        Volatile.Write(ref _write, write + count);
        return count;
    }

    /// <summary>Chamado só pelo consumidor. Devolve quantos valores foram lidos.</summary>
    public int Read(Span<float> destination)
    {
        long read = _read;
        int available = (int)(Volatile.Read(ref _write) - read);
        int count = Math.Min(available, destination.Length);
        if (count <= 0) return 0;

        int start = (int)(read & _mask);
        int first = Math.Min(count, _buffer.Length - start);
        _buffer.AsSpan(start, first).CopyTo(destination);
        if (count > first) _buffer.AsSpan(0, count - first).CopyTo(destination[first..]);

        Volatile.Write(ref _read, read + count);
        return count;
    }

    /// <summary>Descarta tudo o que está na fila. Chamado só pelo consumidor.</summary>
    public void DiscardAll() => Volatile.Write(ref _read, Volatile.Read(ref _write));
}
