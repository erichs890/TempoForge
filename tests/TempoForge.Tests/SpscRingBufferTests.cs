using TempoForge.Core.Util;

namespace TempoForge.Tests;

public class SpscRingBufferTests
{
    [Fact]
    public void WritesAndReadsAcrossTheWrapPoint()
    {
        var ring = new SpscRingBuffer(8);
        Assert.Equal(6, ring.Write(new float[] { 1, 2, 3, 4, 5, 6 }));

        var first = new float[4];
        Assert.Equal(4, ring.Read(first));
        Assert.Equal(new float[] { 1, 2, 3, 4 }, first);

        Assert.Equal(5, ring.Write(new float[] { 7, 8, 9, 10, 11 }));
        var rest = new float[7];
        Assert.Equal(7, ring.Read(rest));
        Assert.Equal(new float[] { 5, 6, 7, 8, 9, 10, 11 }, rest);
    }

    [Fact]
    public void RefusesToOverwriteUnreadData()
    {
        var ring = new SpscRingBuffer(4);
        Assert.Equal(4, ring.Write(new float[] { 1, 2, 3, 4, 5, 6 }));
        Assert.Equal(0, ring.Write(new float[] { 9 }));
        Assert.Equal(0, ring.AvailableToWrite);
    }

    [Fact]
    public async Task KeepsOrderWithOneProducerAndOneConsumer()
    {
        const int total = 2_000_000;
        var ring = new SpscRingBuffer(1024);

        var producer = Task.Run(() =>
        {
            var chunk = new float[37];
            int next = 0;
            while (next < total)
            {
                int n = Math.Min(chunk.Length, total - next);
                for (int i = 0; i < n; i++) chunk[i] = next + i;
                int written = ring.Write(chunk.AsSpan(0, n));
                next += written;
                if (written == 0) Thread.Yield();
            }
        });

        var chunkOut = new float[53];
        int expected = 0;
        while (expected < total)
        {
            int got = ring.Read(chunkOut);
            for (int i = 0; i < got; i++) Assert.Equal(expected + i, (int)chunkOut[i]);
            expected += got;
            if (got == 0) Thread.Yield();
        }
        await producer;
    }
}
