using NAudio.Wave;

namespace TempoForge.Tests;

/// <summary>Gera arquivos WAV sintéticos para os testes e os apaga ao final.</summary>
public sealed class TempWave : IDisposable
{
    public string Path { get; }

    private TempWave(string path) => Path = path;

    public static TempWave Sine(double seconds, double frequency, int sampleRate = 44100, int channels = 2,
        float amplitude = 0.5f)
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tempoforge-{Guid.NewGuid():N}.wav");
        int frames = (int)(seconds * sampleRate);
        var samples = new float[frames * channels];
        for (int i = 0; i < frames; i++)
        {
            float v = amplitude * MathF.Sin(2 * MathF.PI * (float)frequency * i / sampleRate);
            for (int c = 0; c < channels; c++) samples[i * channels + c] = v;
        }
        using (var writer = new WaveFileWriter(path, WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels)))
            writer.WriteSamples(samples, 0, samples.Length);
        return new TempWave(path);
    }

    public void Dispose()
    {
        try { File.Delete(Path); } catch (IOException) { }
    }
}
