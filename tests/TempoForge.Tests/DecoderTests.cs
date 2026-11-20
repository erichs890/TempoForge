using TempoForge.Core;
using TempoForge.Core.Decoding;
using TempoForge.Core.Engine;
using TempoForge.Core.Waveform;

namespace TempoForge.Tests;

public class DecoderTests
{
    [Theory]
    [InlineData(44100, 1)]
    [InlineData(48000, 2)]
    [InlineData(22050, 2)]
    public void ConvertsAnySourceToEngineFormat(int sampleRate, int channels)
    {
        using TempWave wave = TempWave.Sine(2.0, 440, sampleRate, channels);
        using AudioDecoder decoder = AudioDecoder.Open(wave.Path);

        Assert.InRange(decoder.TotalFrames, 95_900, 96_100);

        var buffer = new float[4096 * 2];
        long frames = 0;
        float peak = 0;
        int got;
        while ((got = decoder.Read(buffer, 0, 4096)) > 0)
        {
            frames += got;
            for (int i = 0; i < got * 2; i++) peak = Math.Max(peak, Math.Abs(buffer[i]));
        }

        Assert.InRange(frames, 95_500, 96_500);
        Assert.InRange(peak, 0.45f, 0.55f);
    }

    [Fact]
    public void SeekLandsOnTheRequestedFrame()
    {
        using TempWave wave = TempWave.Sine(3.0, 440, 48000);
        using AudioDecoder decoder = AudioDecoder.Open(wave.Path);

        decoder.Seek(AudioFormat.SecondsToFrames(2.0));
        Assert.Equal(96_000, decoder.Position);

        var buffer = new float[8192 * 2];
        long frames = 0;
        int got;
        while ((got = decoder.Read(buffer, 0, 8192)) > 0) frames += got;
        Assert.InRange(frames, 47_500, 48_500);
    }

    [Fact]
    public async Task PeakScanCoversTheWholeFile()
    {
        using TempWave wave = TempWave.Sine(2.0, 220);
        PeakData peaks = PeakData.Build(wave.Path, 200, CancellationToken.None, out Task done);
        await done;

        Assert.True(peaks.IsComplete);
        Assert.InRange(peaks.Max[100], 0.4f, 0.55f);
        Assert.InRange(peaks.Min[100], -0.55f, -0.4f);
    }

    [Fact]
    [Trait("Category", "Device")]
    public void EnginePlaysSeeksAndReachesTheEnd()
    {
        using TempWave wave = TempWave.Sine(3.0, 440, amplitude: 0.001f);
        using var engine = new AudioEngine();
        engine.Open(wave.Path);
        Assert.InRange(engine.DurationSeconds, 2.99, 3.01);

        engine.Play();
        Thread.Sleep(1000);
        Assert.InRange(engine.PositionSeconds, 0.7, 1.2);

        engine.Pause();
        Thread.Sleep(200);
        double paused = engine.PositionSeconds;
        Thread.Sleep(300);
        Assert.Equal(paused, engine.PositionSeconds, 3);

        engine.Seek(2.5);
        engine.Play();
        Thread.Sleep(300);
        Assert.InRange(engine.PositionSeconds, 2.6, 2.95);

        Thread.Sleep(600);
        Assert.True(engine.HasEnded);
        Assert.False(engine.IsPlaying);
        Assert.Equal(0, engine.UnderrunCount);
    }

    [Fact]
    [Trait("Category", "Device")]
    public void TempoScalesHowFastThePositionAdvances()
    {
        using TempWave wave = TempWave.Sine(8.0, 440, amplitude: 0.001f);
        using var engine = new AudioEngine { Tempo = 2.0 };
        engine.Open(wave.Path);
        engine.Play();
        Thread.Sleep(600);
        double start = engine.PositionSeconds;
        Thread.Sleep(1000);
        Assert.InRange(engine.PositionSeconds - start, 1.8, 2.2);

        engine.Tempo = 0.5;
        Thread.Sleep(500);
        start = engine.PositionSeconds;
        Thread.Sleep(1000);
        Assert.InRange(engine.PositionSeconds - start, 0.4, 0.6);
        Assert.Equal(0, engine.UnderrunCount);
    }
}
