using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TempoForge.Core.Dsp;

namespace TempoForge.App.ViewModels;

public sealed partial class EqBandViewModel : ObservableObject
{
    private readonly EffectSettings _settings;
    private readonly int _index;

    public EqBandViewModel(EffectSettings settings, int index)
    {
        _settings = settings;
        _index = index;
        float hz = EffectSettings.EqFrequencies[index];
        Label = hz >= 1000 ? $"{hz / 1000:0.#}k" : $"{hz:0}";
    }

    public string Label { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GainText))]
    private double _gainDb;

    public string GainText => GainDb == 0 ? "0" : $"{GainDb:+0.0;−0.0}";

    partial void OnGainDbChanged(double value) => _settings.SetEqGainDb(_index, (float)value);
}

public sealed record EqPreset(string Name, double[] GainsDb);

public sealed partial class EqualizerViewModel : ObservableObject
{
    private readonly EffectSettings _settings;
    private bool _applyingPreset;

    public EqualizerViewModel(EffectSettings settings)
    {
        _settings = settings;
        for (int i = 0; i < EffectSettings.EqBandCount; i++)
        {
            var band = new EqBandViewModel(settings, i);
            band.PropertyChanged += (_, e) =>
            {
                // Mexer numa banda à mão deixa de corresponder a qualquer predefinição.
                if (e.PropertyName == nameof(EqBandViewModel.GainDb) && !_applyingPreset) SelectedPreset = null;
            };
            Bands.Add(band);
        }
        _selectedPreset = Presets[0];
    }

    public ObservableCollection<EqBandViewModel> Bands { get; } = [];

    public IReadOnlyList<EqPreset> Presets { get; } =
    [
        new("Plano", [0, 0, 0, 0, 0, 0, 0, 0]),
        new("Mais graves", [6, 4.5, 2, 0, 0, 0, 0, 0]),
        new("Menos graves", [-8, -5, -2, 0, 0, 0, 0, 0]),
        new("Mais brilho", [0, 0, 0, 0, 1, 3, 5, 6]),
        new("Voz em destaque", [-4, -2, 0, 2.5, 4, 3, 1, 0]),
        new("Instrumento em destaque", [2, 1, -1, -3, -3, -1, 1.5, 2]),
        new("Volume baixo", [6, 3.5, 0, -1, 0, 1.5, 4, 5]),
    ];

    [ObservableProperty] private bool _isEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreampText))]
    private double _preampDb;

    [ObservableProperty] private EqPreset? _selectedPreset;

    public string PreampText => PreampDb == 0 ? "0 dB" : $"{PreampDb:+0.0;−0.0} dB";

    partial void OnIsEnabledChanged(bool value) => _settings.EqEnabled = value;

    partial void OnPreampDbChanged(double value) => _settings.PreampDb = (float)value;

    partial void OnSelectedPresetChanged(EqPreset? value)
    {
        if (value == null) return;
        _applyingPreset = true;
        for (int i = 0; i < Bands.Count; i++) Bands[i].GainDb = value.GainsDb[i];
        _applyingPreset = false;
    }

    [RelayCommand]
    private void Reset()
    {
        SelectedPreset = Presets[0];
        PreampDb = 0;
    }

    public double[] GetGains() => Bands.Select(b => b.GainDb).ToArray();

    public void SetGains(IReadOnlyList<double> gains)
    {
        _applyingPreset = true;
        for (int i = 0; i < Bands.Count && i < gains.Count; i++) Bands[i].GainDb = gains[i];
        _applyingPreset = false;
        SelectedPreset = Presets.FirstOrDefault(p => p.GainsDb.SequenceEqual(GetGains()));
    }
}
