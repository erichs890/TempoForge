using TempoForge.Core;
using TempoForge.Core.Decoding;
using TempoForge.Core.Dsp;
using TempoForge.Core.Export;
using TempoForge.Core.Looping;

namespace TempoForge.Tests;

public class ExportTests : IDisposable
{
    private readonly List<string> _files = [];

    private string NewOutput(string extension)
    {
        string path = Path.Combine(Path.GetTempPath(), $"tempoforge-out-{Guid.NewGuid():N}{extension}");
        _files.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (string file in _files)
            try { File.Delete(file); } catch (IOException) { }
    }

    private static float[] ReadAll(string path)
    {
        using AudioDecoder decoder = AudioDecoder.Open(path);
        var all = new List<float>();
        var buffer = new float[4096 * 2];
        int got;
        while ((got = decoder.Read(buffer, 0, 4096)) > 0) all.AddRange(buffer.AsSpan(0, got * 2).ToArray());
        return all.ToArray();
    }

    [Theory]
    [InlineData(0.5, 0.0, 8.0, 440.0)]
    [InlineData(2.0, 0.0, 2.0, 440.0)]
    [InlineData(1.0, 12.0, 4.0, 880.0)]
    [InlineData(0.75, -5.0, 5.3333, 329.63)]
    public void WavExportAppliesTempoAndPitch(double tempo, double semitones, double seconds, double frequency)
    {
        using TempWave wave = TempWave.Sine(4.0, 440);
        string output = NewOutput(".wav");

        AudioExporter.Export(new ExportOptions
        {
            SourcePath = wave.Path, OutputPath = output, Tempo = tempo, PitchSemitones = semitones,
        });

        float[] audio = ReadAll(output);
        double duration = audio.Length / 2.0 / AudioFormat.SampleRate;
        Assert.InRange(duration, seconds * 0.98, seconds * 1.02);

        double measured = StretchHarness.DominantFrequency(audio, audio.Length / 4 - 16384);
        Assert.InRange(1200 * Math.Log2(measured / frequency), -5, 5);
        Assert.False(File.Exists(output + ".parcial"));
    }

    [Fact]
    public void Mp3ExportIsReadableAndHasTheRightLength()
    {
        using TempWave wave = TempWave.Sine(3.0, 440);
        string output = NewOutput(".mp3");

        AudioExporter.Export(new ExportOptions
        {
            SourcePath = wave.Path, OutputPath = output, Format = ExportFormat.Mp3, Mp3Bitrate = 192, Tempo = 1.5,
        });

        // 192 kbps durante 2 s ≈ 48 kB
        Assert.InRange(new FileInfo(output).Length, 40_000, 60_000);
        float[] audio = ReadAll(output);
        Assert.InRange(audio.Length / 2.0 / AudioFormat.SampleRate, 1.95, 2.15);
        double measured = StretchHarness.DominantFrequency(audio, 20000);
        Assert.InRange(1200 * Math.Log2(measured / 440), -5, 5);
    }

    [Fact]
    public void ExportsOnlyTheSelectedRange()
    {
        using TempWave wave = TempWave.Sine(4.0, 440);
        string output = NewOutput(".wav");

        AudioExporter.Export(new ExportOptions
        {
            SourcePath = wave.Path, OutputPath = output,
            Range = new LoopRegion(AudioFormat.SecondsToFrames(1.0), AudioFormat.SecondsToFrames(2.5)),
        });

        Assert.InRange(ReadAll(output).Length / 2.0 / AudioFormat.SampleRate, 1.47, 1.53);
    }

    [Fact]
    public void ExportAppliesEffects()
    {
        using TempWave wave = TempWave.Sine(2.0, 1000, amplitude: 0.1f);
        string output = NewOutput(".wav");
        var effects = new EffectSettings();
        effects.SetEqGainDb(3, 12); // +12 dB em 1 kHz

        AudioExporter.Export(new ExportOptions { SourcePath = wave.Path, OutputPath = output, Effects = effects });

        float[] audio = ReadAll(output);
        float peak = 0;
        for (int i = 48000; i < 72000; i++) peak = Math.Max(peak, Math.Abs(audio[i * 2]));
        Assert.InRange(20 * Math.Log10(peak / 0.1), 10.5, 12.5);
    }

    [Fact]
    public void CancellingLeavesNoFileBehind()
    {
        using TempWave wave = TempWave.Sine(20.0, 440);
        string output = NewOutput(".wav");
        using var cancel = new CancellationTokenSource();
        var progress = new SyncProgress(p => { if (p > 0.2) cancel.Cancel(); });

        Assert.Throws<OperationCanceledException>(() => AudioExporter.Export(
            new ExportOptions { SourcePath = wave.Path, OutputPath = output }, progress, cancel.Token));

        Assert.False(File.Exists(output));
        Assert.False(File.Exists(output + ".parcial"));
    }

    private sealed class SyncProgress(Action<double> action) : IProgress<double>
    {
        public void Report(double value) => action(value);
    }
}
