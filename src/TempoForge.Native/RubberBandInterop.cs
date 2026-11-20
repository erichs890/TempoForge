using System.Runtime.InteropServices;

namespace TempoForge.Native;

/// <summary>Declarações da API C da biblioteca Rubber Band (rubberband.dll, GPL).</summary>
public static unsafe partial class RubberBandInterop
{
    private const string Library = "rubberband";

    [Flags]
    public enum Options
    {
        ProcessRealTime = 0x00000001,
        WindowShort = 0x00100000,
        FormantPreserved = 0x01000000,
        PitchHighConsistency = 0x04000000,
        ChannelsTogether = 0x10000000,
        EngineFiner = 0x20000000,
    }

    [LibraryImport(Library, EntryPoint = "rubberband_new")]
    public static partial nint New(uint sampleRate, uint channels, int options, double timeRatio, double pitchScale);

    [LibraryImport(Library, EntryPoint = "rubberband_delete")]
    public static partial void Delete(nint state);

    [LibraryImport(Library, EntryPoint = "rubberband_reset")]
    public static partial void Reset(nint state);

    [LibraryImport(Library, EntryPoint = "rubberband_set_time_ratio")]
    public static partial void SetTimeRatio(nint state, double ratio);

    [LibraryImport(Library, EntryPoint = "rubberband_set_pitch_scale")]
    public static partial void SetPitchScale(nint state, double scale);

    [LibraryImport(Library, EntryPoint = "rubberband_get_preferred_start_pad")]
    public static partial uint GetPreferredStartPad(nint state);

    [LibraryImport(Library, EntryPoint = "rubberband_get_start_delay")]
    public static partial uint GetStartDelay(nint state);

    [LibraryImport(Library, EntryPoint = "rubberband_set_max_process_size")]
    public static partial void SetMaxProcessSize(nint state, uint samples);

    [LibraryImport(Library, EntryPoint = "rubberband_get_samples_required")]
    public static partial uint GetSamplesRequired(nint state);

    [LibraryImport(Library, EntryPoint = "rubberband_process")]
    public static partial void Process(nint state, float** input, uint samples, int final);

    [LibraryImport(Library, EntryPoint = "rubberband_available")]
    public static partial int Available(nint state);

    [LibraryImport(Library, EntryPoint = "rubberband_retrieve")]
    public static partial uint Retrieve(nint state, float** output, uint samples);

    [LibraryImport(Library, EntryPoint = "rubberband_set_default_debug_level")]
    public static partial void SetDefaultDebugLevel(int level);
}
