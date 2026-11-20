using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using TempoForge.Core.Decoding;
using TempoForge.Core.Dsp;
using TempoForge.Core.Looping;
using TempoForge.Core.Util;

namespace TempoForge.Core.Engine;

/// <summary>
/// Motor de reprodução. Uma thread decodifica para uma fila sem locks; o callback do
/// dispositivo lê dessa fila, passa pelo grafo de DSP e entrega ao WASAPI.
/// O callback nunca bloqueia, aloca ou faz E/S.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    private const int RingCapacity = 1 << 18;      // 131072 quadros estéreo ≈ 2,7 s
    private const int DecodeBlockFrames = 4096;
    private const int FeedBlockFrames = 2048;
    private const int PrefillFrames = 4096;         // ~85 ms
    private const int SeamFadeFrames = 240;         // 5 ms de cada lado da emenda do loop
    private const float FadeStep = 1f / (0.010f * AudioFormat.SampleRate); // fade de 10 ms

    private readonly SpscRingBuffer _ring = new(RingCapacity);
    private readonly RenderSource _source;
    private readonly AutoResetEvent _decodeWake = new(false);
    private readonly PositionJumpQueue _jumps = new();

    private AudioDecoder? _decoder;
    private Thread? _decodeThread;
    private WasapiOut? _output;
    private volatile bool _decodeRun;

    // UI → decodificação
    private long _seekRequest = -1;
    private LoopRegion? _loopRequest;

    // decodificação → callback (protocolo de descarte após seek)
    private int _flushGeneration;
    private int _flushAcknowledged;
    private long _flushBaseFrame;
    private volatile bool _endOfFile;

    // UI → callback
    private volatile bool _playing;
    private double _tempoTarget = 1.0;
    private double _pitchTarget = 1.0;

    // callback → UI
    private long _positionFrames;
    private volatile bool _ended;
    private int _underruns;

    public AudioEngine()
    {
        _source = new RenderSource(this);
    }

    /// <summary>Alvos do equalizador e dos demais efeitos; podem ser alterados a qualquer momento.</summary>
    public EffectSettings Effects { get; } = new();

    public bool IsLoaded => _decoder != null;
    public bool IsPlaying => _playing;
    public string? FilePath => _decoder?.Path;
    public long TotalFrames => _decoder?.TotalFrames ?? 0;
    public double DurationSeconds => AudioFormat.FramesToSeconds(TotalFrames);

    /// <summary>Posição audível, em segundos do arquivo original.</summary>
    public double PositionSeconds => AudioFormat.FramesToSeconds(Volatile.Read(ref _positionFrames));

    /// <summary>Verdadeiro quando a reprodução chegou ao fim do arquivo.</summary>
    public bool HasEnded => _ended;

    public int UnderrunCount => Volatile.Read(ref _underruns);

    /// <summary>Velocidade de reprodução (1 = original), sem alterar o tom.</summary>
    public double Tempo
    {
        get => Volatile.Read(ref _tempoTarget);
        set => Volatile.Write(ref _tempoTarget,
            Math.Clamp(value, RubberBandStretcher.MinTempo, RubberBandStretcher.MaxTempo));
    }

    /// <summary>Altura em semitons (frações valem como cents), sem alterar a velocidade.</summary>
    public double PitchSemitones
    {
        get => 12.0 * Math.Log2(Volatile.Read(ref _pitchTarget));
        set => Volatile.Write(ref _pitchTarget, Math.Clamp(Math.Pow(2.0, value / 12.0),
            RubberBandStretcher.MinPitchScale, RubberBandStretcher.MaxPitchScale));
    }

    /// <summary>Loop A-B em vigor, ou nulo.</summary>
    public LoopRegion? Loop => Volatile.Read(ref _loopRequest);

    /// <summary>Define o trecho a repetir. Devolve falso se o trecho for inválido ou curto demais.</summary>
    public bool SetLoop(double startSeconds, double endSeconds)
    {
        if (!IsLoaded) return false;
        long start = Math.Clamp(AudioFormat.SecondsToFrames(startSeconds), 0, TotalFrames);
        long end = Math.Clamp(AudioFormat.SecondsToFrames(endSeconds), 0, TotalFrames);
        if (end - start < LoopRegion.MinFrames) return false;
        Volatile.Write(ref _loopRequest, new LoopRegion(start, end));
        _decodeWake.Set();
        return true;
    }

    public void ClearLoop()
    {
        Volatile.Write(ref _loopRequest, null);
        _decodeWake.Set();
    }

    public void Open(string path)
    {
        Close();

        AudioDecoder decoder = AudioDecoder.Open(path);
        _decoder = decoder;
        Volatile.Write(ref _loopRequest, null);
        _endOfFile = false;
        _ended = false;
        _playing = false;
        Volatile.Write(ref _positionFrames, 0);
        Interlocked.Exchange(ref _seekRequest, 0); // primeira ação da thread: posicionar e limpar

        _decodeRun = true;
        _decodeThread = new Thread(DecodeLoop)
        {
            IsBackground = true,
            Name = "TempoForge.Decode",
            Priority = ThreadPriority.AboveNormal,
        };
        _decodeThread.Start(decoder);

        var output = new WasapiOut(AudioClientShareMode.Shared, true, 40);
        output.Init(new SampleToWaveProvider(_source));
        output.Play(); // o dispositivo roda sempre; pausa = silêncio com fade
        _output = output;
    }

    public void Close()
    {
        _playing = false;

        WasapiOut? output = _output;
        _output = null;
        if (output != null)
        {
            output.Stop();
            output.Dispose();
        }

        _decodeRun = false;
        _decodeWake.Set();
        _decodeThread?.Join();
        _decodeThread = null;

        _decoder?.Dispose();
        _decoder = null;

        // Sem produtor nem consumidor ativos, é seguro limpar tudo daqui.
        _ring.DiscardAll();
        _jumps.DiscardAll();
        _source.ResetState();
        Volatile.Write(ref _positionFrames, 0);
        _ended = false;
    }

    public void Play()
    {
        if (!IsLoaded) return;
        if (_ended)
        {
            _ended = false;
            Seek(0);
        }
        _playing = true;
    }

    public void Pause() => _playing = false;

    public void Seek(double seconds)
    {
        if (!IsLoaded) return;
        long frame = Math.Clamp(AudioFormat.SecondsToFrames(seconds), 0, TotalFrames);
        _ended = false;
        Volatile.Write(ref _positionFrames, frame); // resposta imediata na UI
        Interlocked.Exchange(ref _seekRequest, frame);
        _decodeWake.Set();
    }

    private void DecodeLoop(object? state)
    {
        var decoder = (AudioDecoder)state!;
        var block = new float[DecodeBlockFrames * AudioFormat.Channels];
        LoopRegion? loop = null;
        long written = 0;      // quadros escritos na fila desde o último descarte
        int fadeInLeft = 0;

        void Reposition(long frame)
        {
            decoder.Seek(frame);
            _endOfFile = false;
            fadeInLeft = 0;
            Volatile.Write(ref _flushBaseFrame, frame);
            int generation = Interlocked.Increment(ref _flushGeneration);
            // Espera o callback descartar o que era da posição antiga antes de escrever de novo.
            while (_decodeRun && Volatile.Read(ref _flushAcknowledged) != generation)
                Thread.Sleep(1);
            written = 0;
        }

        while (_decodeRun)
        {
            long seek = Interlocked.Exchange(ref _seekRequest, -1);
            if (seek >= 0)
            {
                loop = Volatile.Read(ref _loopRequest);
                Reposition(seek);
                continue;
            }

            LoopRegion? requested = Volatile.Read(ref _loopRequest);
            if (!ReferenceEquals(requested, loop))
            {
                loop = requested;
                long audible = Volatile.Read(ref _positionFrames);
                if (loop != null && (audible < loop.Start || audible >= loop.End))
                {
                    Reposition(loop.Start); // fora do trecho: começa pelo ponto A
                }
                else if (_endOfFile || !_jumps.IsEmpty || (loop != null && decoder.Position > loop.End))
                {
                    // O que já está na fila foi decodificado com a regra antiga: refaz a partir daqui.
                    Reposition(audible);
                }
                continue;
            }

            if (_endOfFile || _ring.AvailableToWrite < block.Length)
            {
                _decodeWake.WaitOne(5);
                continue;
            }

            int want = DecodeBlockFrames;
            bool inLoop = loop != null && decoder.Position < loop.End;
            bool lastBeforeSeam = false;
            if (inLoop)
            {
                long toEnd = loop!.End - decoder.Position;
                if (toEnd > SeamFadeFrames) want = (int)Math.Min(want, toEnd - SeamFadeFrames);
                else { want = (int)toEnd; lastBeforeSeam = true; }
            }

            int frames = decoder.Read(block, 0, want);
            if (frames == 0 && !(inLoop && decoder.Position > loop!.Start))
            {
                _endOfFile = true;
                continue;
            }

            if (fadeInLeft > 0)
            {
                int n = Math.Min(fadeInLeft, frames);
                for (int i = 0; i < n; i++)
                {
                    float gain = 1f - (fadeInLeft - i) / (float)SeamFadeFrames;
                    block[i * 2] *= gain;
                    block[i * 2 + 1] *= gain;
                }
                fadeInLeft -= n;
            }
            if (lastBeforeSeam)
            {
                for (int i = 0; i < frames; i++)
                {
                    float gain = 1f - (i + 1) / (float)frames;
                    block[i * 2] *= gain;
                    block[i * 2 + 1] *= gain;
                }
            }

            if (frames > 0)
            {
                _ring.Write(block.AsSpan(0, frames * AudioFormat.Channels));
                written += frames;
            }

            // Chegou ao ponto B (ou o arquivo acabou antes dele): volta ao ponto A sem buraco.
            if (inLoop && (decoder.Position >= loop!.End || frames == 0))
            {
                decoder.Seek(loop.Start);
                fadeInLeft = SeamFadeFrames;
                while (_decodeRun && !_jumps.TryPush(written, loop.Start)) Thread.Sleep(1);
            }
        }
    }

    public void Dispose()
    {
        Close();
        _source.Dispose();
        _decodeWake.Dispose();
    }

    /// <summary>Lado do callback de áudio. Tudo aqui roda na thread do dispositivo.</summary>
    private sealed class RenderSource : ISampleProvider, IDisposable
    {
        private readonly AudioEngine _engine;
        private readonly ITimeStretcher _stretcher = new RubberBandStretcher();
        private readonly float[] _feed = new float[FeedBlockFrames * AudioFormat.Channels];

        private long _baseFrame;
        private long _consumedFrames;
        private int _drainFramesLeft;
        private float _gain;
        private bool _prefilling;

        private readonly EffectChain _effects;

        public RenderSource(AudioEngine engine)
        {
            _engine = engine;
            _effects = new EffectChain(engine.Effects);
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(AudioFormat.SampleRate, AudioFormat.Channels);
        }

        public WaveFormat WaveFormat { get; }

        public void ResetState()
        {
            _stretcher.Reset();
            _baseFrame = 0;
            _consumedFrames = 0;
            _gain = 0;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            AudioEngine e = _engine;
            Span<float> output = buffer.AsSpan(offset, count);
            int frames = count / AudioFormat.Channels;

            int generation = Volatile.Read(ref e._flushGeneration);
            if (generation != Volatile.Read(ref e._flushAcknowledged))
            {
                e._ring.DiscardAll();
                e._jumps.DiscardAll();
                _stretcher.Reset();
                _effects.Reset();
                _baseFrame = Volatile.Read(ref e._flushBaseFrame);
                _consumedFrames = 0;
                _drainFramesLeft = -1;
                _gain = 0;
                _prefilling = true;
                Volatile.Write(ref e._flushAcknowledged, generation);
            }

            // Depois de um seek, espera a fila ter um mínimo de áudio antes de começar a consumir.
            if (_prefilling && (e._endOfFile || e._ring.AvailableToRead >= PrefillFrames * AudioFormat.Channels))
                _prefilling = false;

            _stretcher.Tempo = Glide(_stretcher.Tempo, Volatile.Read(ref e._tempoTarget));
            _stretcher.PitchScale = Glide(_stretcher.PitchScale, Volatile.Read(ref e._pitchTarget));

            bool playing = e._playing;
            if (_prefilling || (!playing && _gain <= 0f))
            {
                output.Clear();
                return count;
            }

            int produced = Render(output, frames);
            if (produced < frames) output[(produced * AudioFormat.Channels)..].Clear();

            _effects.Process(output);
            ApplyFade(output, frames, playing ? 1f : 0f);

            long played = Math.Max(0, _consumedFrames - _stretcher.LatencyFrames);
            while (e._jumps.TryPopReached(played, out long streamFrame, out long filePosition))
                _baseFrame = filePosition - streamFrame; // passou por uma emenda de loop
            Volatile.Write(ref e._positionFrames, Math.Clamp(_baseFrame + played, 0, e.TotalFrames));
            return count;
        }

        /// <summary>
        /// Aproxima o valor atual do alvo em passos por bloco (~30 ms no total), para que
        /// arrastar um controle nunca cause salto audível.
        /// </summary>
        private static double Glide(double current, double target)
        {
            double next = current + (target - current) * 0.4;
            return Math.Abs(target - next) < 1e-4 ? target : next;
        }

        private int Render(Span<float> output, int frames)
        {
            AudioEngine e = _engine;
            int done = 0;
            while (done < frames)
            {
                done += _stretcher.Read(output[(done * AudioFormat.Channels)..], frames - done);
                if (done >= frames) break;

                int wanted = Math.Clamp(_stretcher.FramesRequired, 256, FeedBlockFrames);
                int available = e._ring.AvailableToRead / AudioFormat.Channels;
                int take = Math.Min(wanted, available);

                if (take > 0)
                {
                    Span<float> feed = _feed.AsSpan(0, take * AudioFormat.Channels);
                    e._ring.Read(feed);
                    _stretcher.Write(feed);
                    _consumedFrames += take;
                    continue;
                }

                if (!e._endOfFile)
                {
                    // Disco mais lento que a reprodução: entrega silêncio, nunca espera.
                    if (e._playing) Interlocked.Increment(ref e._underruns);
                    break;
                }

                // Fim do arquivo: empurra silêncio para esvaziar o atraso interno do estágio.
                if (_drainFramesLeft < 0) _drainFramesLeft = _stretcher.LatencyFrames;
                if (_drainFramesLeft == 0)
                {
                    e._ended = true;
                    e._playing = false;
                    break;
                }
                int drain = Math.Min(_drainFramesLeft, FeedBlockFrames);
                Span<float> silence = _feed.AsSpan(0, drain * AudioFormat.Channels);
                silence.Clear();
                _stretcher.Write(silence);
                _drainFramesLeft -= drain;
            }
            return done;
        }

        private void ApplyFade(Span<float> output, int frames, float target)
        {
            if (_gain == target && target == 1f) return;
            float gain = _gain;
            for (int i = 0; i < frames; i++)
            {
                if (gain < target) gain = Math.Min(target, gain + FadeStep);
                else if (gain > target) gain = Math.Max(target, gain - FadeStep);
                output[i * 2] *= gain;
                output[i * 2 + 1] *= gain;
            }
            _gain = gain;
        }

        public void Dispose() => _stretcher.Dispose();
    }
}
