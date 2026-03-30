using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FpdSimViewer.Engine;
using FpdSimViewer.Models.Core;
using System.Windows.Threading;

namespace FpdSimViewer.ViewModels;

public enum SimulationMode
{
    Static = 0,
    Continuous = 1,
    Triggered = 2,
    DarkFrame = 3,
    ResetOnly = 4,
}

public sealed partial class SimControlViewModel : ObservableObject, IDisposable
{
    private readonly SimulationEngine _engine;
    private readonly Action<SimulationSnapshot> _snapshotCallback;
    private readonly DispatcherTimer _timer;

    [ObservableProperty]
    private ComboChoice _selectedCombo;

    [ObservableProperty]
    private ModeChoice _selectedMode;

    [ObservableProperty]
    private int _speed = 1;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private int _powerTargetMode = 1;

    [ObservableProperty]
    private bool _vglStable = true;

    [ObservableProperty]
    private bool _vghStable = true;

    [ObservableProperty]
    private bool _xrayReady;

    [ObservableProperty]
    private bool _xrayPrepRequest;

    [ObservableProperty]
    private bool _xrayOn;

    [ObservableProperty]
    private bool _xrayOff;

    [ObservableProperty]
    private bool _faultVghOver;

    [ObservableProperty]
    private bool _faultVghUnder;

    [ObservableProperty]
    private bool _faultTempOver;

    [ObservableProperty]
    private bool _faultPllUnlocked;

    [ObservableProperty]
    private bool _hardwareEmergencyActive;

    public SimControlViewModel(SimulationEngine engine, Action<SimulationSnapshot> snapshotCallback)
    {
        _engine = engine;
        _snapshotCallback = snapshotCallback;
        ComboOptions =
        [
            new ComboChoice(1, "C1"),
            new ComboChoice(2, "C2"),
            new ComboChoice(3, "C3"),
            new ComboChoice(4, "C4"),
            new ComboChoice(5, "C5"),
            new ComboChoice(6, "C6"),
            new ComboChoice(7, "C7"),
        ];
        ModeOptions =
        [
            new ModeChoice(SimulationMode.Static, "STATIC"),
            new ModeChoice(SimulationMode.Continuous, "CONTINUOUS"),
            new ModeChoice(SimulationMode.Triggered, "TRIGGERED"),
            new ModeChoice(SimulationMode.DarkFrame, "DARK_FRAME"),
            new ModeChoice(SimulationMode.ResetOnly, "RESET_ONLY"),
        ];
        SelectedCombo = ComboOptions[0];
        SelectedMode = ModeOptions[0];
        PowerTargetModes = new ObservableCollection<int>(Enumerable.Range(0, 7));
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _timer.Tick += OnTimerTick;
        ApplyExternalInputs();
    }

    public ObservableCollection<ComboChoice> ComboOptions { get; }

    public ObservableCollection<ModeChoice> ModeOptions { get; }

    public ObservableCollection<int> PowerTargetModes { get; }

    public string PlayPauseLabel => IsPlaying ? "Pause" : "Play";

    public string CurrentComboLabel => SelectedCombo is null ? string.Empty : $"C{SelectedCombo.Id}";

    public string PanelName => $"{_engine.ComboConfig.Rows} x {_engine.ComboConfig.Cols} Panel";

    public uint PanelRows => _engine.ComboConfig.Rows;

    public uint PanelCols => _engine.ComboConfig.Cols;

    public string GateIcName => _engine.ComboConfig.GateIcName;

    public string AfeName => _engine.ComboConfig.AfeName;

    public uint AfeChips => _engine.ComboConfig.AfeChips;

    public string ModeLabel => SelectedMode?.Label ?? string.Empty;

    [RelayCommand]
    private void Reset()
    {
        IsPlaying = false;
        _engine.Reset();
        _engine.SetMode((uint)SelectedMode.Mode);
        ApplyExternalInputs();
        _snapshotCallback(_engine.RefreshSnapshot());
    }

    [RelayCommand]
    private void Step()
    {
        _snapshotCallback(_engine.Step());
    }

    [RelayCommand]
    private void TogglePlay()
    {
        IsPlaying = !IsPlaying;
    }

    partial void OnSelectedComboChanged(ComboChoice value)
    {
        _engine.SetCombo(value.Id);
        RaiseHardwareSummaryChanged();
        if (SelectedMode is not null)
        {
            _engine.SetMode((uint)SelectedMode.Mode);
            ApplyExternalInputs();
            _snapshotCallback(_engine.RefreshSnapshot());
        }
    }

    partial void OnSelectedModeChanged(ModeChoice value)
    {
        _engine.SetMode((uint)value.Mode);
        OnPropertyChanged(nameof(ModeLabel));
        _snapshotCallback(_engine.RefreshSnapshot());
    }

    partial void OnIsPlayingChanged(bool value)
    {
        if (value)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }

        OnPropertyChanged(nameof(PlayPauseLabel));
    }

    partial void OnPowerTargetModeChanged(int value) => RefreshExternalInputs();
    partial void OnVglStableChanged(bool value) => RefreshExternalInputs();
    partial void OnVghStableChanged(bool value) => RefreshExternalInputs();
    partial void OnXrayReadyChanged(bool value) => RefreshExternalInputs();
    partial void OnXrayPrepRequestChanged(bool value) => RefreshExternalInputs();
    partial void OnXrayOnChanged(bool value) => RefreshExternalInputs();
    partial void OnXrayOffChanged(bool value) => RefreshExternalInputs();
    partial void OnFaultVghOverChanged(bool value) => RefreshExternalInputs();
    partial void OnFaultVghUnderChanged(bool value) => RefreshExternalInputs();
    partial void OnFaultTempOverChanged(bool value) => RefreshExternalInputs();
    partial void OnFaultPllUnlockedChanged(bool value) => RefreshExternalInputs();
    partial void OnHardwareEmergencyActiveChanged(bool value) => RefreshExternalInputs();

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
    }

    private void RefreshExternalInputs()
    {
        ApplyExternalInputs();
        _snapshotCallback(_engine.RefreshSnapshot());
    }

    private void ApplyExternalInputs()
    {
        _engine.SetPowerInputs((uint)PowerTargetMode, VglStable, VghStable);
        _engine.SetFaultInputs(FaultVghOver, FaultVghUnder, FaultTempOver, FaultPllUnlocked, HardwareEmergencyActive);
        _engine.SetXrayInputs(XrayReady, XrayPrepRequest, XrayOn, XrayOff);
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var steps = Speed switch
        {
            <= 1 => 1,
            <= 10 => Speed,
            <= 100 => Math.Max(2, Speed / 5),
            _ => Math.Max(4, Speed / 20),
        };

        SimulationSnapshot snapshot = _engine.CurrentSnapshot;
        for (var i = 0; i < steps; i++)
        {
            snapshot = _engine.Step();
        }

        _snapshotCallback(snapshot);
    }

    private void RaiseHardwareSummaryChanged()
    {
        OnPropertyChanged(nameof(CurrentComboLabel));
        OnPropertyChanged(nameof(PanelName));
        OnPropertyChanged(nameof(PanelRows));
        OnPropertyChanged(nameof(PanelCols));
        OnPropertyChanged(nameof(GateIcName));
        OnPropertyChanged(nameof(AfeName));
        OnPropertyChanged(nameof(AfeChips));
    }
}

public sealed record ComboChoice(int Id, string Label)
{
    public override string ToString() => Label;
}

public sealed record ModeChoice(SimulationMode Mode, string Label)
{
    public override string ToString() => Label;
}
