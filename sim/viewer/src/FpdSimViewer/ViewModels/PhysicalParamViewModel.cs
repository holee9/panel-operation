using CommunityToolkit.Mvvm.ComponentModel;
using FpdSimViewer.Engine;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;
using System.Windows.Media;

namespace FpdSimViewer.ViewModels;

public sealed partial class PhysicalParamViewModel : ObservableObject
{
    private const double CyclesPerMicrosecond = 100.0;
    private const double CyclesPerMillisecond = 100_000.0;

    private readonly SimulationEngine _engine;
    private readonly Action<SimulationSnapshot> _snapshotCallback;
    private bool _isUpdating;

    [ObservableProperty]
    private double _gateOnMicroseconds;

    [ObservableProperty]
    private double _gateSettleMicroseconds;

    [ObservableProperty]
    private double _lineMicroseconds;

    [ObservableProperty]
    private int _ifsRange;

    [ObservableProperty]
    private double _integrateMilliseconds;

    [ObservableProperty]
    private int _nResetScans;

    [ObservableProperty]
    private bool _cicEnabled;

    [ObservableProperty]
    private int _cicProfile;

    [ObservableProperty]
    private bool _isCicAvailable;

    [ObservableProperty]
    private int _ifsMax = 63;

    [ObservableProperty]
    private double _minLineMicroseconds = 22.0;

    [ObservableProperty]
    private double _vghTargetVoltage = SimulationEngine.VghDefault;

    [ObservableProperty]
    private double _vglTargetVoltage = SimulationEngine.VglDefault;

    public Brush GateOnBackground => ResolveSpecBrush(GateOnMicroseconds, minimum: 1.0, maximum: 50.0);

    public Brush GateSettleBackground => ResolveSpecBrush(GateSettleMicroseconds, minimum: 0.0, maximum: 10.0);

    public Brush LineBackground => ResolveSpecBrush(LineMicroseconds, minimum: MinLineMicroseconds, maximum: 120.0);

    public Brush IfsBackground => ResolveSpecBrush(IfsRange, minimum: 0, maximum: IfsMax);

    public Brush IntegrateBackground => ResolveSpecBrush(IntegrateMilliseconds, minimum: 0.0, maximum: 160.0);

    public Brush NResetBackground => ResolveSpecBrush(NResetScans, minimum: 0, maximum: 16);

    public Brush CicProfileBackground => ResolveSpecBrush(CicProfile, minimum: 0, maximum: 15);

    public PhysicalParamViewModel(SimulationEngine engine, Action<SimulationSnapshot> snapshotCallback)
    {
        _engine = engine;
        _snapshotCallback = snapshotCallback;
    }

    public void UpdateFromSnapshot(SimulationSnapshot snapshot, HardwareComboConfig comboConfig)
    {
        _isUpdating = true;
        try
        {
            GateOnMicroseconds = snapshot.Registers[FoundationConstants.kRegTGateOn] / CyclesPerMicrosecond;
            GateSettleMicroseconds = snapshot.Registers[FoundationConstants.kRegTGateSettle] / CyclesPerMicrosecond;
            LineMicroseconds = snapshot.Registers[FoundationConstants.kRegTLine] / CyclesPerMicrosecond;
            IfsRange = snapshot.Registers[FoundationConstants.kRegAfeIfs];

            var integrateCycles = ((uint)(snapshot.Registers[FoundationConstants.kRegTIntegHi] & 0x00FFU) << 16) |
                                  snapshot.Registers[FoundationConstants.kRegTInteg];
            IntegrateMilliseconds = integrateCycles / CyclesPerMillisecond;

            NResetScans = snapshot.Registers[FoundationConstants.kRegNReset];
            CicEnabled = snapshot.Registers[FoundationConstants.kRegCicEn] != 0U;
            CicProfile = snapshot.Registers[FoundationConstants.kRegCicProfile] & 0x000F;
            IsCicAvailable = comboConfig.AfeModel is AfeAfe2256Model;
            IfsMax = comboConfig.AfeTypeId == 1U ? 31 : 63;
            MinLineMicroseconds = FoundationConstants.ComboMinTLine((byte)comboConfig.ComboId) / CyclesPerMicrosecond;
            RaiseSpecBackgroundChanged();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    partial void OnGateOnMicrosecondsChanged(double value)
    {
        OnPropertyChanged(nameof(GateOnBackground));
        if (_isUpdating)
        {
            return;
        }

        WriteRegister(FoundationConstants.kRegTGateOn, ToCycles(value, minimum: 100, maximum: 4095));
    }

    partial void OnGateSettleMicrosecondsChanged(double value)
    {
        OnPropertyChanged(nameof(GateSettleBackground));
        if (_isUpdating)
        {
            return;
        }

        WriteRegister(FoundationConstants.kRegTGateSettle, ToCycles(value, minimum: 0, maximum: 255));
    }

    partial void OnLineMicrosecondsChanged(double value)
    {
        OnPropertyChanged(nameof(LineBackground));
        if (_isUpdating)
        {
            return;
        }

        var minCycles = (ushort)Math.Round(MinLineMicroseconds * CyclesPerMicrosecond);
        WriteRegister(FoundationConstants.kRegTLine, ToCycles(value, minimum: minCycles, maximum: ushort.MaxValue));
    }

    partial void OnIfsRangeChanged(int value)
    {
        OnPropertyChanged(nameof(IfsBackground));
        if (_isUpdating)
        {
            return;
        }

        WriteRegister(FoundationConstants.kRegAfeIfs, (ushort)Math.Clamp(value, 0, IfsMax));
    }

    partial void OnIntegrateMillisecondsChanged(double value)
    {
        OnPropertyChanged(nameof(IntegrateBackground));
        if (_isUpdating)
        {
            return;
        }

        var cycles = (uint)Math.Clamp(Math.Round(value * CyclesPerMillisecond), 0.0, 0x00FF_FFFF);
        _engine.WriteRegister(FoundationConstants.kRegTInteg, (ushort)(cycles & 0xFFFFU));
        _engine.WriteRegister(FoundationConstants.kRegTIntegHi, (ushort)((cycles >> 16) & 0x00FFU));
        _snapshotCallback(_engine.RefreshSnapshot());
    }

    partial void OnNResetScansChanged(int value)
    {
        OnPropertyChanged(nameof(NResetBackground));
        if (_isUpdating)
        {
            return;
        }

        WriteRegister(FoundationConstants.kRegNReset, (ushort)Math.Clamp(value, 0, 255));
    }

    partial void OnCicEnabledChanged(bool value)
    {
        if (_isUpdating || !IsCicAvailable)
        {
            return;
        }

        WriteRegister(FoundationConstants.kRegCicEn, value ? (ushort)1 : (ushort)0);
    }

    partial void OnCicProfileChanged(int value)
    {
        OnPropertyChanged(nameof(CicProfileBackground));
        if (_isUpdating || !IsCicAvailable)
        {
            return;
        }

        var clamped = (ushort)Math.Clamp(value, 0, 15);
        WriteRegister(FoundationConstants.kRegCicProfile, clamped);
    }

    partial void OnIfsMaxChanged(int value) => OnPropertyChanged(nameof(IfsBackground));

    partial void OnMinLineMicrosecondsChanged(double value) => OnPropertyChanged(nameof(LineBackground));

    private void WriteRegister(byte address, ushort value)
    {
        _engine.WriteRegister(address, value);
        _snapshotCallback(_engine.RefreshSnapshot());
    }

    private static ushort ToCycles(double value, ushort minimum, ushort maximum)
    {
        var cycles = (ushort)Math.Clamp(Math.Round(value * CyclesPerMicrosecond), minimum, maximum);
        return cycles;
    }

    private void RaiseSpecBackgroundChanged()
    {
        OnPropertyChanged(nameof(GateOnBackground));
        OnPropertyChanged(nameof(GateSettleBackground));
        OnPropertyChanged(nameof(LineBackground));
        OnPropertyChanged(nameof(IfsBackground));
        OnPropertyChanged(nameof(IntegrateBackground));
        OnPropertyChanged(nameof(NResetBackground));
        OnPropertyChanged(nameof(CicProfileBackground));
    }

    private static Brush ResolveSpecBrush(double value, double minimum, double maximum)
    {
        return value < minimum || value > maximum ? Brushes.LightCoral : Brushes.White;
    }

    private static Brush ResolveSpecBrush(int value, int minimum, int maximum)
    {
        return value < minimum || value > maximum ? Brushes.LightCoral : Brushes.White;
    }
}
