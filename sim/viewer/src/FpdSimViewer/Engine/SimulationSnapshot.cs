using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Engine;

public sealed record SimulationSnapshot(
    ulong Cycle,
    uint FsmState,
    string FsmStateName,
    bool Busy,
    bool Done,
    bool Error,
    uint ErrCode,
    uint RowIndex,
    uint TotalRows,
    bool GateOnPulse,
    bool GateSettle,
    bool ScanActive,
    bool ScanDone,
    SignalMap GateSignals,
    bool AfeReady,
    bool AfeDoutValid,
    uint AfeLineCount,
    bool ProtTimeout,
    bool ProtError,
    bool ForceGateOff,
    bool PowerGood,
    ushort[] Registers)
{
    public double VglVoltage { get; init; }

    public double VghVoltage { get; init; }

    public double VpdVoltage { get; init; }

    public double VglRailVoltage { get; init; }

    public double VghRailVoltage { get; init; }

    public double GateOeVoltage { get; init; }

    public double GateClkVoltage { get; init; }

    public double AfeSyncVoltage { get; init; }

    public double AfePhaseProgress { get; init; }

    public string AfePhaseLabel { get; init; } = "IDLE";

    public ushort[] CurrentRowPixels { get; init; } = [];

    public double ElapsedMicroseconds { get; init; }

    public bool AfeFclkExpected { get; init; }

    public static SimulationSnapshot Capture(
        ulong cycle,
        SignalMap fsmOutputs,
        SignalMap rowScanOutputs,
        SignalMap gateOutputs,
        SignalMap afeOutputs,
        SignalMap protOutputs,
        SignalMap powerOutputs,
        ushort[] registers)
    {
        var fsmState = SignalHelpers.GetScalar(fsmOutputs, "fsm_state");
        var currentRowPixels = ExtractCurrentRowPixels(afeOutputs);
        var powerGood = SignalHelpers.GetScalar(powerOutputs, "power_good") != 0U;
        var gateOeHigh = ResolveGateOeHigh(gateOutputs);
        var gateClkHigh = ResolveGateClkHigh(gateOutputs);
        var afeSyncHigh = ResolveAfeSyncHigh(afeOutputs);
        var afePhase = ResolveAfePhase(afeOutputs, registers);

        return new SimulationSnapshot(
            cycle,
            fsmState,
            ResolveStateName(fsmState),
            SignalHelpers.GetScalar(fsmOutputs, "sts_busy") != 0U,
            SignalHelpers.GetScalar(fsmOutputs, "sts_done") != 0U,
            SignalHelpers.GetScalar(fsmOutputs, "sts_error") != 0U,
            SignalHelpers.GetScalar(fsmOutputs, "sts_err_code"),
            SignalHelpers.GetScalar(rowScanOutputs, "row_index"),
            registers[FoundationConstants.kRegNRows],
            SignalHelpers.GetScalar(rowScanOutputs, "gate_on_pulse") != 0U,
            SignalHelpers.GetScalar(rowScanOutputs, "gate_settle") != 0U,
            SignalHelpers.GetScalar(rowScanOutputs, "scan_active") != 0U,
            SignalHelpers.GetScalar(rowScanOutputs, "scan_done") != 0U,
            CloneSignalMap(gateOutputs),
            SignalHelpers.GetScalar(afeOutputs, "afe_ready") != 0U,
            SignalHelpers.GetScalar(afeOutputs, "dout_window_valid") != 0U,
            SignalHelpers.GetScalar(afeOutputs, "line_count"),
            SignalHelpers.GetScalar(protOutputs, "err_timeout") != 0U,
            SignalHelpers.GetScalar(protOutputs, "err_flag") != 0U,
            SignalHelpers.GetScalar(protOutputs, "force_gate_off") != 0U,
            powerGood,
            (ushort[])registers.Clone())
        {
            VglVoltage = powerGood ? -10.0 : 0.0,
            VghVoltage = powerGood ? 20.0 : 0.0,
            VpdVoltage = powerGood ? -1.5 : 0.0,
            VglRailVoltage = (SignalHelpers.GetScalar(powerOutputs, "vgl_rail_voltage") / 100.0) - 15.0,
            VghRailVoltage = SignalHelpers.GetScalar(powerOutputs, "vgh_rail_voltage") / 100.0,
            GateOeVoltage = gateOeHigh ? 20.0 : -10.0,
            GateClkVoltage = gateClkHigh ? 3.3 : 0.0,
            AfeSyncVoltage = afeSyncHigh ? 3.3 : 0.0,
            AfePhaseProgress = afePhase.Progress,
            AfePhaseLabel = afePhase.Label,
            CurrentRowPixels = currentRowPixels,
            ElapsedMicroseconds = cycle * 0.01,
            AfeFclkExpected = SignalHelpers.GetScalar(afeOutputs, "fclk_expected") != 0U,
        };
    }

    private static SignalMap CloneSignalMap(SignalMap source)
    {
        var clone = new SignalMap();
        foreach (var pair in source)
        {
            clone[pair.Key] = pair.Value.IsScalar
                ? pair.Value
                : new SignalValue((ushort[])pair.Value.Vector.Clone());
        }

        return clone;
    }

    private static ushort[] ExtractCurrentRowPixels(SignalMap afeOutputs)
    {
        var pixels = SignalHelpers.GetVector(afeOutputs, "line_pixels");
        if (pixels.Length != 0)
        {
            return (ushort[])pixels.Clone();
        }

        pixels = SignalHelpers.GetVector(afeOutputs, "sample_line");
        if (pixels.Length != 0)
        {
            return (ushort[])pixels.Clone();
        }

        pixels = SignalHelpers.GetVector(afeOutputs, "current_row");
        return pixels.Length != 0 ? (ushort[])pixels.Clone() : [];
    }

    private static bool ResolveGateOeHigh(SignalMap gateOutputs)
    {
        if (gateOutputs.ContainsKey("nv_oe"))
        {
            return SignalHelpers.GetScalar(gateOutputs, "nv_oe") != 0U;
        }

        return SignalHelpers.GetScalar(gateOutputs, "oe1l") != 0U ||
               SignalHelpers.GetScalar(gateOutputs, "oe1r") != 0U ||
               SignalHelpers.GetScalar(gateOutputs, "oe2l") != 0U ||
               SignalHelpers.GetScalar(gateOutputs, "oe2r") != 0U;
    }

    private static bool ResolveGateClkHigh(SignalMap gateOutputs)
    {
        if (gateOutputs.ContainsKey("nv_clk"))
        {
            return SignalHelpers.GetScalar(gateOutputs, "nv_clk") != 0U;
        }

        return SignalHelpers.GetScalar(gateOutputs, "stv1l") != 0U ||
               SignalHelpers.GetScalar(gateOutputs, "stv1r") != 0U ||
               SignalHelpers.GetScalar(gateOutputs, "stv2l") != 0U ||
               SignalHelpers.GetScalar(gateOutputs, "stv2r") != 0U;
    }

    private static bool ResolveAfeSyncHigh(SignalMap afeOutputs)
    {
        return SignalHelpers.GetScalar(afeOutputs, "dout_window_valid") != 0U ||
               SignalHelpers.GetScalar(afeOutputs, "config_done") != 0U;
    }

    private static (double Progress, string Label) ResolveAfePhase(SignalMap afeOutputs, ushort[] registers)
    {
        if (SignalHelpers.GetScalar(afeOutputs, "dout_window_valid") == 0U)
        {
            return SignalHelpers.GetScalar(afeOutputs, "config_done") != 0U
                ? (0.0, "CDS")
                : (0.0, "IDLE");
        }

        var configuredTLine = registers.Length > FoundationConstants.kRegTLine
            ? registers[FoundationConstants.kRegTLine]
            : 0U;
        var effectiveTLine = Math.Max(1U, configuredTLine == 0U ? 2200U : configuredTLine);
        var lineCount = SignalHelpers.GetScalar(afeOutputs, "line_count");
        var progress = Math.Clamp(lineCount / (double)effectiveTLine, 0.0, 1.0);
        var label = progress switch
        {
            < 0.34 => "CDS",
            < 0.67 => "ADC",
            _ => "OUT",
        };

        return (progress, label);
    }

    private static string ResolveStateName(uint state)
    {
        return state switch
        {
            0U => "IDLE",
            1U => "POWER_CHECK",
            2U => "RESET",
            3U => "WAIT_PREP",
            4U => "BIAS_STAB",
            5U => "XRAY_INTEG",
            6U => "CONFIG_AFE",
            7U => "READOUT",
            8U => "SETTLE",
            9U => "FRAME_DONE",
            10U => "DONE",
            15U => "ERROR",
            _ => "UNKNOWN",
        };
    }
}
