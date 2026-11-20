using System.Runtime.InteropServices;
using TempoForge.Native;

namespace TempoForge.Core.Dsp;

/// <summary>
/// Mudança de velocidade e de altura pelo Rubber Band (motor R3, modo tempo real).
/// Toda a memória é reservada na construção; nenhuma chamada posterior aloca do lado gerenciado.
/// </summary>
public sealed unsafe class RubberBandStretcher : ITimeStretcher
{
    public const double MinTempo = 0.25, MaxTempo = 4.0;
    public const double MinPitchScale = 0.25, MaxPitchScale = 4.0;

    private const int BlockFrames = 4096;

    private nint _state;
    private readonly float* _inLeft, _inRight, _outLeft, _outRight;
    private readonly float** _inPlanes, _outPlanes;

    private double _tempo = 1.0, _pitch = 1.0;
    private int _discardFrames;
    private long _writtenFrames;
    private double _playedSourceFrames;

    public RubberBandStretcher(bool highQuality = true)
    {
        RubberBandInterop.SetDefaultDebugLevel(0);

        var options = RubberBandInterop.Options.ProcessRealTime
                      | RubberBandInterop.Options.ChannelsTogether
                      | RubberBandInterop.Options.PitchHighConsistency;
        if (highQuality) options |= RubberBandInterop.Options.EngineFiner;

        _state = RubberBandInterop.New(AudioFormat.SampleRate, AudioFormat.Channels, (int)options, 1.0, 1.0);
        if (_state == 0) throw new InvalidOperationException("Não foi possível iniciar o Rubber Band.");
        RubberBandInterop.SetMaxProcessSize(_state, BlockFrames);

        nuint bytes = BlockFrames * sizeof(float);
        _inLeft = (float*)NativeMemory.AllocZeroed(bytes);
        _inRight = (float*)NativeMemory.AllocZeroed(bytes);
        _outLeft = (float*)NativeMemory.AllocZeroed(bytes);
        _outRight = (float*)NativeMemory.AllocZeroed(bytes);
        _inPlanes = (float**)NativeMemory.Alloc((nuint)(2 * sizeof(float*)));
        _outPlanes = (float**)NativeMemory.Alloc((nuint)(2 * sizeof(float*)));
        _inPlanes[0] = _inLeft;
        _inPlanes[1] = _inRight;
        _outPlanes[0] = _outLeft;
        _outPlanes[1] = _outRight;

        Prime();
    }

    public double Tempo
    {
        get => _tempo;
        set
        {
            double tempo = Math.Clamp(value, MinTempo, MaxTempo);
            if (tempo == _tempo) return;
            _tempo = tempo;
            RubberBandInterop.SetTimeRatio(_state, 1.0 / tempo);
        }
    }

    public double PitchScale
    {
        get => _pitch;
        set
        {
            double pitch = Math.Clamp(value, MinPitchScale, MaxPitchScale);
            if (pitch == _pitch) return;
            _pitch = pitch;
            RubberBandInterop.SetPitchScale(_state, pitch);
        }
    }

    public int FramesRequired => (int)RubberBandInterop.GetSamplesRequired(_state);

    public int LatencyFrames => (int)Math.Clamp(_writtenFrames - _playedSourceFrames, 0, int.MaxValue);

    public void Write(ReadOnlySpan<float> interleaved)
    {
        int frames = interleaved.Length / AudioFormat.Channels;
        int done = 0;
        while (done < frames)
        {
            int n = Math.Min(BlockFrames, frames - done);
            for (int i = 0; i < n; i++)
            {
                _inLeft[i] = interleaved[(done + i) * 2];
                _inRight[i] = interleaved[(done + i) * 2 + 1];
            }
            RubberBandInterop.Process(_state, _inPlanes, (uint)n, 0);
            done += n;
        }
        _writtenFrames += frames;
    }

    public int Read(Span<float> interleaved, int maxFrames)
    {
        maxFrames = Math.Min(maxFrames, interleaved.Length / AudioFormat.Channels);
        int done = 0;
        while (done < maxFrames)
        {
            int available = RubberBandInterop.Available(_state);
            if (available <= 0) break;

            if (_discardFrames > 0)
            {
                // Atraso de partida do algoritmo: essas amostras não correspondem a áudio real.
                int drop = (int)RubberBandInterop.Retrieve(_state, _outPlanes,
                    (uint)Math.Min(Math.Min(available, _discardFrames), BlockFrames));
                _discardFrames -= drop;
                if (drop == 0) break;
                continue;
            }

            int n = (int)RubberBandInterop.Retrieve(_state, _outPlanes,
                (uint)Math.Min(Math.Min(available, maxFrames - done), BlockFrames));
            if (n == 0) break;
            for (int i = 0; i < n; i++)
            {
                interleaved[(done + i) * 2] = _outLeft[i];
                interleaved[(done + i) * 2 + 1] = _outRight[i];
            }
            done += n;
        }
        _playedSourceFrames = Math.Min(_writtenFrames, _playedSourceFrames + done * _tempo);
        return done;
    }

    public void Reset()
    {
        RubberBandInterop.Reset(_state);
        Prime();
    }

    /// <summary>Alimenta o silêncio inicial pedido pelo algoritmo e agenda o descarte do atraso de partida.</summary>
    private void Prime()
    {
        _writtenFrames = 0;
        _playedSourceFrames = 0;

        int pad = (int)RubberBandInterop.GetPreferredStartPad(_state);
        _discardFrames = (int)RubberBandInterop.GetStartDelay(_state);

        new Span<float>(_inLeft, BlockFrames).Clear();
        new Span<float>(_inRight, BlockFrames).Clear();
        while (pad > 0)
        {
            int n = Math.Min(pad, BlockFrames);
            RubberBandInterop.Process(_state, _inPlanes, (uint)n, 0);
            pad -= n;
        }
    }

    public void Dispose()
    {
        if (_state == 0) return;
        RubberBandInterop.Delete(_state);
        _state = 0;
        NativeMemory.Free(_inLeft);
        NativeMemory.Free(_inRight);
        NativeMemory.Free(_outLeft);
        NativeMemory.Free(_outRight);
        NativeMemory.Free(_inPlanes);
        NativeMemory.Free(_outPlanes);
    }
}
