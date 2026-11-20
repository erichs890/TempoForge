using NAudio.Lame;
using NAudio.Wave;
using TempoForge.Core.Decoding;
using TempoForge.Core.Dsp;
using TempoForge.Core.Looping;

namespace TempoForge.Core.Export;

public enum ExportFormat { Wav, Mp3 }

/// <summary>Tudo o que define um arquivo exportado. É uma fotografia: mudar a UI depois não o afeta.</summary>
public sealed record ExportOptions
{
    public required string SourcePath { get; init; }
    public required string OutputPath { get; init; }
    public ExportFormat Format { get; init; } = ExportFormat.Wav;

    /// <summary>Taxa do MP3 em kbps: 128, 192, 256 ou 320.</summary>
    public int Mp3Bitrate { get; init; } = 320;

    public double Tempo { get; init; } = 1.0;
    public double PitchSemitones { get; init; }
    public EffectSettings Effects { get; init; } = new();

    /// <summary>Se definido, exporta só este trecho do arquivo original.</summary>
    public LoopRegion? Range { get; init; }
}

/// <summary>
/// Renderiza o arquivo com os mesmos estágios da reprodução (velocidade/tom → efeitos),
/// fora do tempo real, e grava em WAV ou MP3.
/// </summary>
public static class AudioExporter
{
    private const int BlockFrames = 2048;
    private const int SettleFrames = 9600; // silêncio inicial para os efeitos chegarem aos alvos

    public static readonly int[] Mp3Bitrates = [128, 192, 256, 320];

    public static void Export(ExportOptions options, IProgress<double>? progress = null,
        CancellationToken cancel = default)
    {
        string temp = options.OutputPath + ".parcial";
        try
        {
            Render(options, temp, progress, cancel);
            cancel.ThrowIfCancellationRequested();
            File.Move(temp, options.OutputPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static void Render(ExportOptions options, string path, IProgress<double>? progress,
        CancellationToken cancel)
    {
        using AudioDecoder decoder = AudioDecoder.Open(options.SourcePath);
        long start = options.Range?.Start ?? 0;
        long end = Math.Min(options.Range?.End ?? decoder.TotalFrames, decoder.TotalFrames);
        if (end <= start) throw new InvalidOperationException("O trecho a exportar está vazio.");
        if (start > 0) decoder.Seek(start);

        using var stretcher = new RubberBandStretcher
        {
            Tempo = options.Tempo,
            PitchScale = Math.Pow(2.0, options.PitchSemitones / 12.0),
        };
        var effects = new EffectChain(options.Effects);

        var block = new float[BlockFrames * AudioFormat.Channels];
        var input = new float[BlockFrames * AudioFormat.Channels];

        // Deixa as rampas internas dos efeitos chegarem aos valores pedidos antes do áudio real.
        for (int settled = 0; settled < SettleFrames; settled += BlockFrames)
        {
            Array.Clear(block);
            effects.Process(block);
        }

        var format = WaveFormat.CreateIeeeFloatWaveFormat(AudioFormat.SampleRate, AudioFormat.Channels);
        using Stream file = File.Create(path);
        using var writer = new OutputWriter(file, format, options);

        int skip = effects.LatencyFrames;   // atraso fixo do limitador, removido do início
        long remaining = end - start;
        int drain = -1;
        bool flushed = false;

        while (true)
        {
            cancel.ThrowIfCancellationRequested();

            int got = stretcher.Read(block, BlockFrames);
            if (got > 0)
            {
                Span<float> rendered = block.AsSpan(0, got * AudioFormat.Channels);
                effects.Process(rendered);
                int drop = Math.Min(skip, got);
                skip -= drop;
                writer.Write(block, drop * AudioFormat.Channels, (got - drop) * AudioFormat.Channels);
                continue;
            }

            if (remaining > 0)
            {
                int wanted = (int)Math.Min(Math.Clamp(stretcher.FramesRequired, 256, BlockFrames), remaining);
                int read = decoder.Read(input, 0, wanted);
                if (read == 0)
                {
                    remaining = 0; // o arquivo terminou antes da duração anunciada
                    continue;
                }
                stretcher.Write(input.AsSpan(0, read * AudioFormat.Channels));
                remaining -= read;
                progress?.Report(1.0 - remaining / (double)(end - start));
                continue;
            }

            // Fim da entrada: esvazia o atraso interno do estágio de stretch.
            if (drain < 0) drain = stretcher.LatencyFrames;
            if (drain > 0)
            {
                int n = Math.Min(drain, BlockFrames);
                Array.Clear(input, 0, n * AudioFormat.Channels);
                stretcher.Write(input.AsSpan(0, n * AudioFormat.Channels));
                drain -= n;
                continue;
            }

            if (!flushed)
            {
                // Empurra silêncio para sair o que ainda está no atraso do limitador.
                int tail = effects.LatencyFrames;
                Array.Clear(block, 0, tail * AudioFormat.Channels);
                effects.Process(block.AsSpan(0, tail * AudioFormat.Channels));
                writer.Write(block, 0, tail * AudioFormat.Channels);
                flushed = true;
            }
            break;
        }
        progress?.Report(1.0);
    }

    /// <summary>Grava float estéreo em WAV de 16 bits ou em MP3.</summary>
    private sealed class OutputWriter : IDisposable
    {
        private readonly WaveFileWriter? _wav;
        private readonly LameMP3FileWriter? _mp3;
        private byte[] _bytes = new byte[BlockFrames * AudioFormat.Channels * sizeof(float)];

        public OutputWriter(Stream file, WaveFormat floatFormat, ExportOptions options)
        {
            if (options.Format == ExportFormat.Mp3)
                _mp3 = new LameMP3FileWriter(file, floatFormat, options.Mp3Bitrate);
            else
                _wav = new WaveFileWriter(file, new WaveFormat(AudioFormat.SampleRate, 16, AudioFormat.Channels));
        }

        public void Write(float[] samples, int offset, int count)
        {
            if (count <= 0) return;
            if (_wav != null)
            {
                _wav.WriteSamples(samples, offset, count); // converte para 16 bits
                return;
            }
            int byteCount = count * sizeof(float);
            if (_bytes.Length < byteCount) _bytes = new byte[byteCount];
            Buffer.BlockCopy(samples, offset * sizeof(float), _bytes, 0, byteCount);
            _mp3!.Write(_bytes, 0, byteCount);
        }

        public void Dispose()
        {
            _wav?.Dispose();
            _mp3?.Dispose();
        }
    }
}
