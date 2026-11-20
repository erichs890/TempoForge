namespace TempoForge.Core;

/// <summary>Formato interno único do motor: float 32, estéreo intercalado, 48 kHz.</summary>
public static class AudioFormat
{
    public const int SampleRate = 48000;
    public const int Channels = 2;

    public static long SecondsToFrames(double seconds) => (long)Math.Round(seconds * SampleRate);

    public static double FramesToSeconds(long frames) => frames / (double)SampleRate;
}
