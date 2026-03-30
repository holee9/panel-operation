using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FpdSimViewer.Engine;
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
    private readonly IReadOnlyList<HardwarePresetProfile> _hardwareProfiles;
    private bool _isSynchronizingHardwareSelection;

    [ObservableProperty]
    private ComboChoice? _selectedCombo;

    [ObservableProperty]
    private PanelChoice? _selectedPanel;

    [ObservableProperty]
    private GateChoice? _selectedGate;

    [ObservableProperty]
    private AfeChoice? _selectedAfe;

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
        _hardwareProfiles = CreateHardwareProfiles();

        ComboOptions = new ObservableCollection<ComboChoice>(
        [
            new ComboChoice(0, "Custom"),
            .. _hardwareProfiles.Select(profile => profile.Preset),
        ]);
        PanelOptions = new ObservableCollection<PanelChoice>(
        [
            .. _hardwareProfiles.Select(profile => profile.Panel).Distinct(),
        ]);
        GateOptions = new ObservableCollection<GateChoice>();
        AfeOptions = new ObservableCollection<AfeChoice>();
        ModeOptions = new ObservableCollection<ModeChoice>(
        [
            new ModeChoice(SimulationMode.Static, "STATIC"),
            new ModeChoice(SimulationMode.Continuous, "CONTINUOUS"),
            new ModeChoice(SimulationMode.Triggered, "TRIGGERED"),
            new ModeChoice(SimulationMode.DarkFrame, "DARK_FRAME"),
            new ModeChoice(SimulationMode.ResetOnly, "RESET_ONLY"),
        ]);
        PowerTargetModes = new ObservableCollection<int>(Enumerable.Range(0, 7));

        var defaultProfile = _hardwareProfiles.First(profile => profile.ComboId == 1);
        InitializeHardwareSelection(defaultProfile);
        SelectedMode = ModeOptions[0];

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        _timer.Tick += OnTimerTick;
        ApplyExternalInputs();
    }

    public ObservableCollection<ComboChoice> ComboOptions { get; }

    public ObservableCollection<PanelChoice> PanelOptions { get; }

    public ObservableCollection<GateChoice> GateOptions { get; }

    public ObservableCollection<AfeChoice> AfeOptions { get; }

    public ObservableCollection<ModeChoice> ModeOptions { get; }

    public ObservableCollection<int> PowerTargetModes { get; }

    public string PlayPauseLabel => IsPlaying ? "Pause" : "Play";

    public string CurrentComboLabel => $"C{_engine.ComboConfig.ComboId}";

    public string PanelName => SelectedPanel is null ? string.Empty : $"{SelectedPanel.Rows} x {SelectedPanel.Cols} Panel";

    public uint PanelRows => SelectedPanel?.Rows ?? 0U;

    public uint PanelCols => SelectedPanel?.Cols ?? 0U;

    public string GateIcName => SelectedGate?.Label ?? string.Empty;

    public string AfeName => SelectedAfe?.Label ?? string.Empty;

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

    partial void OnSelectedComboChanged(ComboChoice? value)
    {
        if (_isSynchronizingHardwareSelection || value is null || value.Id == 0)
        {
            RaiseHardwareSummaryChanged();
            return;
        }

        var profile = _hardwareProfiles.First(profile => profile.ComboId == value.Id);
        ApplyPresetSelection(profile);
    }

    partial void OnSelectedPanelChanged(PanelChoice? value)
    {
        if (_isSynchronizingHardwareSelection || value is null)
        {
            return;
        }

        _isSynchronizingHardwareSelection = true;
        try
        {
            RebuildHardwareChoices(value, SelectedGate, SelectedAfe);
        }
        finally
        {
            _isSynchronizingHardwareSelection = false;
        }

        ApplyCustomHardwareSelection();
    }

    partial void OnSelectedGateChanged(GateChoice? value)
    {
        if (_isSynchronizingHardwareSelection || value is null)
        {
            return;
        }

        ApplyCustomHardwareSelection();
    }

    partial void OnSelectedAfeChanged(AfeChoice? value)
    {
        if (_isSynchronizingHardwareSelection || value is null)
        {
            return;
        }

        ApplyCustomHardwareSelection();
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

    private void InitializeHardwareSelection(HardwarePresetProfile profile)
    {
        _isSynchronizingHardwareSelection = true;
        try
        {
            SelectedCombo = ComboOptions.First(option => option.Id == profile.ComboId);
            SelectedPanel = PanelOptions.First(option => option.Key == profile.Panel.Key);
            RebuildHardwareChoices(profile.Panel, profile.Gate, profile.Afe);
        }
        finally
        {
            _isSynchronizingHardwareSelection = false;
        }

        ApplyEngineCombo(profile.ComboId);
    }

    private void ApplyPresetSelection(HardwarePresetProfile profile)
    {
        _isSynchronizingHardwareSelection = true;
        try
        {
            SelectedPanel = PanelOptions.First(option => option.Key == profile.Panel.Key);
            RebuildHardwareChoices(profile.Panel, profile.Gate, profile.Afe);
        }
        finally
        {
            _isSynchronizingHardwareSelection = false;
        }

        ApplyEngineCombo(profile.ComboId);
    }

    private void ApplyCustomHardwareSelection()
    {
        if (SelectedPanel is null || SelectedGate is null || SelectedAfe is null)
        {
            return;
        }

        var profile = _hardwareProfiles.FirstOrDefault(candidate =>
            candidate.Panel.Key == SelectedPanel.Key &&
            candidate.Gate == SelectedGate &&
            candidate.Afe == SelectedAfe);

        if (profile is null)
        {
            return;
        }

        _isSynchronizingHardwareSelection = true;
        try
        {
            SelectedCombo = ComboOptions.First(option => option.Id == 0);
        }
        finally
        {
            _isSynchronizingHardwareSelection = false;
        }

        ApplyEngineCombo(profile.ComboId);
    }

    private void RebuildHardwareChoices(PanelChoice panel, GateChoice? preferredGate, AfeChoice? preferredAfe)
    {
        ReplaceChoices(
            GateOptions,
            _hardwareProfiles
                .Where(profile => profile.Panel.Key == panel.Key)
                .Select(profile => profile.Gate)
                .Distinct());

        ReplaceChoices(
            AfeOptions,
            _hardwareProfiles
                .Where(profile => profile.Panel.Key == panel.Key)
                .Select(profile => profile.Afe)
                .Distinct());

        SelectedGate = GateOptions.FirstOrDefault(option => option == preferredGate) ?? GateOptions[0];
        SelectedAfe = AfeOptions.FirstOrDefault(option => option == preferredAfe) ?? AfeOptions[0];
    }

    private void ApplyEngineCombo(int comboId)
    {
        if (_engine.ComboConfig.ComboId != comboId)
        {
            _engine.SetCombo(comboId);
        }

        RaiseHardwareSummaryChanged();
        if (SelectedMode is not null)
        {
            _engine.SetMode((uint)SelectedMode.Mode);
            ApplyExternalInputs();
            _snapshotCallback(_engine.RefreshSnapshot());
        }
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

    private static void ReplaceChoices<TChoice>(ObservableCollection<TChoice> target, IEnumerable<TChoice> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }

    private static IReadOnlyList<HardwarePresetProfile> CreateHardwareProfiles()
    {
        var panel2048 = new PanelChoice("2048x2048", "2048 x 2048", 2048U, 2048U);
        var panel1664 = new PanelChoice("2048x1664", "2048 x 1664", 2048U, 1664U);
        var panel3072 = new PanelChoice("3072x3072", "3072 x 3072", 3072U, 3072U);
        var gateNv1047 = new GateChoice("NV1047");
        var gateNt39565d = new GateChoice("NT39565D");
        var afeAd71124 = new AfeChoice("AD71124");
        var afeAd71143 = new AfeChoice("AD71143");
        var afe2256 = new AfeChoice("AFE2256");

        return
        [
            new HardwarePresetProfile(1, new ComboChoice(1, "C1"), panel2048, gateNv1047, afeAd71124),
            new HardwarePresetProfile(2, new ComboChoice(2, "C2"), panel2048, gateNv1047, afeAd71143),
            new HardwarePresetProfile(3, new ComboChoice(3, "C3"), panel2048, gateNv1047, afe2256),
            new HardwarePresetProfile(4, new ComboChoice(4, "C4"), panel1664, gateNv1047, afeAd71124),
            new HardwarePresetProfile(5, new ComboChoice(5, "C5"), panel1664, gateNv1047, afe2256),
            new HardwarePresetProfile(6, new ComboChoice(6, "C6"), panel3072, gateNt39565d, afeAd71124),
            new HardwarePresetProfile(7, new ComboChoice(7, "C7"), panel3072, gateNt39565d, afe2256),
        ];
    }
}

public sealed record ComboChoice(int Id, string Label)
{
    public override string ToString() => Label;
}

public sealed record PanelChoice(string Key, string Label, uint Rows, uint Cols)
{
    public override string ToString() => Label;
}

public sealed record GateChoice(string Label)
{
    public override string ToString() => Label;
}

public sealed record AfeChoice(string Label)
{
    public override string ToString() => Label;
}

public sealed record ModeChoice(SimulationMode Mode, string Label)
{
    public override string ToString() => Label;
}

internal sealed record HardwarePresetProfile(
    int ComboId,
    ComboChoice Preset,
    PanelChoice Panel,
    GateChoice Gate,
    AfeChoice Afe);
