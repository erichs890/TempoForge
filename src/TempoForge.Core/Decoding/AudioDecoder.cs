using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace TempoForge.Core.Decoding;

/// <summary>
/// Abre um arquivo de áudio e o entrega no formato do motor (float estéreo 48 kHz),
/// com seek por quadro. Uma instância pertence a uma única thread de leitura.
/// </summary>
public sealed class AudioDecoder : IDisposable
{
    private readonly WaveStream _reader;
    private ISampleProvider _chain;
    private long _frame;

    private AudioDecoder(string path, WaveStream reader)
    {
        Path = path;
        _reader = reader;
        _chain = BuildChain();
        TotalFrames = AudioFormat.SecondsToFrames(reader.TotalTime.TotalSeconds);
    }

    public string Path { get; }

    /// <summary>Duração em quadros de 48 kHz (estimativa em alguns formatos comprimidos).</summary>
    public long TotalFrames { get; }

    /// <summary>Posição do próximo quadro a ser lido.</summary>
    public long Position => _frame;

    public static readonly string[] SupportedExtensions =
        [".mp3", ".m4a", ".aac", ".mp4", ".wav", ".flac", ".ogg", ".oga", ".wma", ".aiff", ".aif"];

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant());

    public static AudioDecoder Open(string path)
    {
        string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        WaveStream reader = ext is ".ogg" or ".oga"
            ? new VorbisWaveReader(path)
            : new MediaFoundationReader(path,
                new MediaFoundationReader.MediaFoundationReaderSettings { RequestFloatOutput = true });
        try
        {
            return new AudioDecoder(path, reader);
        }
        catch
        {
            reader.Dispose();
            throw;
        }
    }

    private ISampleProvider BuildChain()
    {
        ISampleProvider chain = _reader.ToSampleProvider();
        int channels = chain.WaveFormat.Channels;
        if (channels == 1) chain = new MonoToStereoSampleProvider(chain);
        else if (channels > 2) chain = new FirstTwoChannelsProvider(chain);

        if (chain.WaveFormat.SampleRate != AudioFormat.SampleRate)
            chain = new WdlResamplingSampleProvider(chain, AudioFormat.SampleRate);
        return chain;
    }

    /// <summary>Lê até <paramref name="frames"/> quadros. Zero indica fim do arquivo.</summary>
    public int Read(float[] buffer, int offset, int frames)
    {
        int wanted = frames * AudioFormat.Channels;
        int total = 0;
        while (total < wanted)
        {
            int got = _chain.Read(buffer, offset + total, wanted - total);
            if (got <= 0) break;
            total += got;
        }
        int read = total / AudioFormat.Channels;
        _frame += read;
        return read;
    }

    public void Seek(long frame)
    {
        frame = Math.Clamp(frame, 0, TotalFrames);
        WaveFormat format = _reader.WaveFormat;
        long sourceFrame = (long)(frame * (double)format.SampleRate / AudioFormat.SampleRate);
        long bytes = sourceFrame * format.BlockAlign;
        _reader.Position = Math.Clamp(bytes, 0, _reader.Length);
        _chain = BuildChain(); // zera o estado do reamostrador
        _frame = frame;
    }

    public void Dispose() => _reader.Dispose();

    /// <summary>Reduz arquivos com mais de 2 canais para os dois primeiros (frente esquerda/direita).</summary>
    private sealed class FirstTwoChannelsProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _channels;
        private float[] _scratch = [];

        public FirstTwoChannelsProvider(ISampleProvider source)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 2);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int frames = count / 2;
            int needed = frames * _channels;
            if (_scratch.Length < needed) _scratch = new float[needed];
            int got = _source.Read(_scratch, 0, needed) / _channels;
            for (int i = 0; i < got; i++)
            {
                buffer[offset + i * 2] = _scratch[i * _channels];
                buffer[offset + i * 2 + 1] = _scratch[i * _channels + 1];
            }
            return got * 2;
        }
    }
}
