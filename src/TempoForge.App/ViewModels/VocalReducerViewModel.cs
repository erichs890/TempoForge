using CommunityToolkit.Mvvm.ComponentModel;
using TempoForge.Core.Dsp;

namespace TempoForge.App.ViewModels;

public sealed partial class VocalReducerViewModel : ObservableObject
{
    private readonly EffectSettings _settings;

    public VocalReducerViewModel(EffectSettings settings) => _settings = settings;

    [ObservableProperty] private bool _isEnabled;

    /// <summary>Intensidade, de 0 a 100 %.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AmountText))]
    private double _amount = 100;

    /// <summary>Limite grave da faixa removida, em Hz.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LowCutText))]
    private double _lowCut = 120;

    /// <summary>Limite agudo da faixa removida, em Hz.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HighCutText))]
    private double _highCut = 8000;

    public string AmountText => $"{Amount:0} %";
    public string LowCutText => $"{LowCut:0} Hz";
    public string HighCutText => $"{HighCut / 1000:0.0} kHz";

    public void RefreshTexts()
    {
        OnPropertyChanged(nameof(AmountText));
        OnPropertyChanged(nameof(LowCutText));
        OnPropertyChanged(nameof(HighCutText));
    }

    partial void OnIsEnabledChanged(bool value) => _settings.VocalReducerEnabled = value;
    partial void OnAmountChanged(double value) => _settings.VocalReducerAmount = (float)(value / 100);
    partial void OnLowCutChanged(double value) => _settings.VocalReducerLowCutHz = (float)value;
    partial void OnHighCutChanged(double value) => _settings.VocalReducerHighCutHz = (float)value;
}
