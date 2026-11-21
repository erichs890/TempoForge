using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TempoForge.App.Localization;
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

    public void RefreshTexts() => OnPropertyChanged(nameof(GainText));

    partial void OnGainDbChanged(double value) => _settings.SetEqGainDb(_index, (float)value);
}

/// <summary>Uma curva pronta; o nome exibido acompanha o idioma.</summary>
public sealed class EqPreset(string key, double[] gainsDb) : ObservableObject
{
    public double[] GainsDb { get; } = gainsDb;
    public string Name => Loc.T(key);
    public void RefreshName() => OnPropertyChanged(nameof(Name));
    public override string ToString() => Name;
}

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
                // Mexer numa banda à mão normalmente deixa de corresponder a qualquer predefinição;
                // se a curva voltar a coincidir com uma (ex.: tudo zerado), ela é selecionada.
                if (e.PropertyName == nameof(EqBandViewModel.GainDb) && !_applyingPreset)
                    SelectedPreset = Presets.FirstOrDefault(p => p.GainsDb.SequenceEqual(GetGains()));
            };
            Bands.Add(band);
        }
        _selectedPreset = Presets[0];
        Loc.Changed += () =>
        {
            foreach (EqPreset preset in Presets) preset.RefreshName();
        };
    }

    public ObservableCollection<EqBandViewModel> Bands { get; } = [];

    public IReadOnlyList<EqPreset> Presets { get; } =
    [
        new("PresetFlat", [0, 0, 0, 0, 0, 0, 0, 0]),
        new("PresetMoreBass", [6, 4.5, 2, 0, 0, 0, 0, 0]),
        new("PresetLessBass", [-8, -5, -2, 0, 0, 0, 0, 0]),
        new("PresetBright", [0, 0, 0, 0, 1, 3, 5, 6]),
        new("PresetVoice", [-4, -2, 0, 2.5, 4, 3, 1, 0]),
        new("PresetInstrument", [2, 1, -1, -3, -3, -1, 1.5, 2]),
        new("PresetLowVolume", [6, 3.5, 0, -1, 0, 1.5, 4, 5]),
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

    /// <summary>Reemite os textos formatados (o separador decimal muda com o idioma).</summary>
    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(PreampText));
        foreach (EqBandViewModel band in Bands) band.RefreshTexts();
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
