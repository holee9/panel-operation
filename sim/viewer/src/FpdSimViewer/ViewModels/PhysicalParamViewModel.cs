using CommunityToolkit.Mvvm.ComponentModel;
using FpdSimViewer.Engine;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

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
        }
        finally
        {
            _isUpdating = false;
        }
    }

    partial void OnGateOnMicrosecondsChanged(double value)
    {
        if (_isUpdating)
        {
            return;
        }

        WriteRegister(FoundationConstants.kRegTGateOn, ToCycles(value, minimum: 100, maximum: 4095));
    }

    partial void OnGateSettleMicrosecondsChanged(double value)
    {
        if (_isUpdating)
        {
            return;
        }

        WriteRegister(FoundationConstants.kRegTGateSettle, ToCycles(value, minimum: 0, maximum: 255));
    }

    partial void OnLineMicrosecondsChanged(double value)
    {
        if (_isUpdating)
        {
            return;
        }

        var minCycles = (ushort)Math.Round(MinLineMicroseconds * CyclesPerMicrosecond);
        WriteRegister(FoundationConstants.kRegTLine, ToCycles(value, minimum: minCycles, maximum: ushort.MaxValue));
    }

    partial void OnIfsRangeChanged(int value)
    {
        if (_isUpdating)
        {
            return;
        }

        WriteRegister(FoundationConstants.kRegAfeIfs, (ushort)Math.Clamp(value, 0, IfsMax));
    }

    partial void OnIntegrateMillisecondsChanged(double value)
    {
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
        if (_isUpdating || !IsCicAvailable)
        {
            return;
        }

        var clamped = (ushort)Math.Clamp(value, 0, 15);
        WriteRegister(FoundationConstants.kRegCicProfile, clamped);
    }

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
}
