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
            SignalHelpers.GetScalar(powerOutputs, "power_good") != 0U,
            (ushort[])registers.Clone());
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
