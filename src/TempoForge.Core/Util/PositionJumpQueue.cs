namespace TempoForge.Core.Util;

/// <summary>
/// Avisos da thread de decodificação para o callback: "a partir do quadro N do fluxo, o áudio
/// vem da posição P do arquivo". É assim que o callback sabe onde está a emenda de um loop.
/// Um produtor, um consumidor, sem locks.
/// </summary>
public sealed class PositionJumpQueue
{
    private const int Capacity = 256; // potência de 2
    private readonly long[] _streamFrame = new long[Capacity];
    private readonly long[] _filePosition = new long[Capacity];
    private int _write;
    private int _read;

    public bool IsEmpty => Volatile.Read(ref _write) == Volatile.Read(ref _read);

    /// <summary>Lado do produtor. Falso se a fila estiver cheia.</summary>
    public bool TryPush(long streamFrame, long filePosition)
    {
        int write = _write;
        if (write - Volatile.Read(ref _read) >= Capacity) return false;
        _streamFrame[write & (Capacity - 1)] = streamFrame;
        _filePosition[write & (Capacity - 1)] = filePosition;
        Volatile.Write(ref _write, write + 1);
        return true;
    }

    /// <summary>Lado do consumidor: retira o próximo aviso se ele já foi alcançado.</summary>
    public bool TryPopReached(long playedStreamFrame, out long streamFrame, out long filePosition)
    {
        int read = _read;
        if (read != Volatile.Read(ref _write) && _streamFrame[read & (Capacity - 1)] <= playedStreamFrame)
        {
            streamFrame = _streamFrame[read & (Capacity - 1)];
            filePosition = _filePosition[read & (Capacity - 1)];
            Volatile.Write(ref _read, read + 1);
            return true;
        }
        streamFrame = filePosition = 0;
        return false;
    }

    /// <summary>Lado do consumidor: descarta tudo.</summary>
    public void DiscardAll() => Volatile.Write(ref _read, Volatile.Read(ref _write));
}
