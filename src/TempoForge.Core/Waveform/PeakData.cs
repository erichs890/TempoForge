using TempoForge.Core.Decoding;

namespace TempoForge.Core.Waveform;

/// <summary>
/// Resumo do arquivo para desenhar a forma de onda: mínimo e máximo por faixa de tempo.
/// É preenchido aos poucos por uma thread de fundo; <see cref="Filled"/> diz até onde.
/// </summary>
public sealed class PeakData
{
    private int _filled;

    public PeakData(int buckets)
    {
        Min = new float[buckets];
        Max = new float[buckets];
    }

    public float[] Min { get; }
    public float[] Max { get; }
    public int Buckets => Min.Length;
    public int Filled => Volatile.Read(ref _filled);
    public bool IsComplete => Filled >= Buckets;

    private const int FileMagic = 0x314B5054; // "TPK1"

    /// <summary>Grava o resumo completo em disco para reabrir o arquivo sem varrer de novo.</summary>
    public void Save(string path)
    {
        if (!IsComplete) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            using (var writer = new BinaryWriter(File.Create(temp)))
            {
                writer.Write(FileMagic);
                writer.Write(Buckets);
                for (int i = 0; i < Buckets; i++)
                {
                    writer.Write(Min[i]);
                    writer.Write(Max[i]);
                }
            }
            File.Move(temp, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // O cache é só uma otimização.
        }
    }

    /// <summary>Lê um resumo salvo; nulo se não existir, estiver corrompido ou tiver outro tamanho.</summary>
    public static PeakData? TryLoad(string path, int buckets)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var reader = new BinaryReader(File.OpenRead(path));
            if (reader.ReadInt32() != FileMagic || reader.ReadInt32() != buckets) return null;
            var data = new PeakData(buckets);
            for (int i = 0; i < buckets; i++)
            {
                data.Min[i] = reader.ReadSingle();
                data.Max[i] = reader.ReadSingle();
            }
            data._filled = buckets;
            return data;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Varre o arquivo inteiro com um decodificador próprio, sem tocar na reprodução.</summary>
    public static PeakData Build(string path, int buckets, CancellationToken cancel, out Task completion)
    {
        var data = new PeakData(buckets);
        completion = Task.Factory.StartNew(() => data.Scan(path, cancel), cancel,
            TaskCreationOptions.LongRunning, TaskScheduler.Default);
        return data;
    }

    private void Scan(string path, CancellationToken cancel)
    {
        Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
        using AudioDecoder decoder = AudioDecoder.Open(path);
        long total = Math.Max(1, decoder.TotalFrames);
        double framesPerBucket = total / (double)Buckets;

        var block = new float[8192 * AudioFormat.Channels];
        long frame = 0;
        int bucket = 0;
        float min = 0, max = 0;

        while (!cancel.IsCancellationRequested)
        {
            int got = decoder.Read(block, 0, 8192);
            if (got == 0) break;

            for (int i = 0; i < got; i++)
            {
                float v = (block[i * 2] + block[i * 2 + 1]) * 0.5f;
                if (v < min) min = v;
                if (v > max) max = v;

                frame++;
                if (frame >= (long)((bucket + 1) * framesPerBucket))
                {
                    if (bucket < Buckets)
                    {
                        Min[bucket] = min;
                        Max[bucket] = max;
                        bucket++;
                        Volatile.Write(ref _filled, bucket);
                    }
                    min = 0;
                    max = 0;
                }
            }
        }

        // A duração informada pode ser maior que o conteúdo real; o resto fica em silêncio.
        if (!cancel.IsCancellationRequested) Volatile.Write(ref _filled, Buckets);
    }
}
