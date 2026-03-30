using FluentAssertions;
using FpdSimViewer.Engine;
using FpdSimViewer.Models.Core;
using FpdSimViewer.ViewModels;

namespace FpdSimViewer.Tests.ViewModels;

public sealed class VerificationViewModelTests
{
    [Fact]
    public void UpdateFromSnapshot_ShouldPopulateTimingChecks()
    {
        var traceCapture = new TraceCapture();
        var viewModel = new VerificationViewModel(traceCapture);
        var combo = HardwareComboConfig.Create(1);
        var snapshot = CreateSnapshot(
            comboId: combo.ComboId,
            fsmState: 7U,
            fsmStateName: "READOUT",
            cycle: 10,
            elapsedUs: 0.10,
            rowIndex: 1U,
            totalRows: 2048U,
            tGateOn: 2000,
            tSettle: 100,
            tLine: 2200);

        viewModel.UpdateFromSnapshot(snapshot, combo);

        viewModel.TimingChecks[0].Result.Should().Be("PASS");
        viewModel.TimingChecks[1].Result.Should().Be("FAIL");
        viewModel.TimingChecks[2].Result.Should().Be("PASS");
        viewModel.TimingChecks[3].Result.Should().Be("FAIL");
        viewModel.TimingChecks[4].Result.Should().Be("INFO");
    }

    [Fact]
    public void UpdateFromSnapshot_ShouldCapEventLogAtOneHundredEntries()
    {
        var traceCapture = new TraceCapture();
        var viewModel = new VerificationViewModel(traceCapture);
        var combo = HardwareComboConfig.Create(1);

        for (var index = 0; index < 105; index++)
        {
            var snapshot = CreateSnapshot(
                comboId: combo.ComboId,
                fsmState: (uint)(index % 2 == 0 ? 2 : 7),
                fsmStateName: index % 2 == 0 ? "RESET" : "READOUT",
                cycle: (ulong)index,
                elapsedUs: index * 0.01,
                rowIndex: (uint)index,
                totalRows: 2048U,
                tGateOn: 2000,
                tSettle: 300,
                tLine: 2200);

            viewModel.UpdateFromSnapshot(snapshot, combo);
        }

        viewModel.EventLog.Count.Should().Be(100);
        viewModel.EventLog[0].TimeText.Should().Be("0.05 us");
        viewModel.EventLog[^1].Description.Should().Contain("->");
    }

    [Fact]
    public void BuildCsvContent_ShouldIncludeRecordedSnapshots()
    {
        var traceCapture = new TraceCapture();
        traceCapture.Record(CreateSnapshot(1, 7U, "READOUT", 1, 0.01, 1U, 2048U, 2000, 300, 2200));
        traceCapture.Record(CreateSnapshot(1, 8U, "SETTLE", 2, 0.02, 2U, 2048U, 2000, 300, 2200));
        var viewModel = new VerificationViewModel(traceCapture);

        var csv = viewModel.BuildCsvContent();

        csv.Should().Contain("Cycle,ElapsedUs,FsmState");
        csv.Should().Contain("READOUT");
        csv.Should().Contain("SETTLE");
    }

    [Fact]
    public void BuildVcdContent_ShouldEmitHeaderAndSignalDump()
    {
        var traceCapture = new TraceCapture();
        traceCapture.Record(CreateSnapshot(1, 7U, "READOUT", 1, 0.01, 1U, 2048U, 2000, 300, 2200));
        traceCapture.Record(CreateSnapshot(1, 10U, "DONE", 3, 0.03, 2U, 2048U, 2000, 300, 2200));
        var viewModel = new VerificationViewModel(traceCapture);

        var vcd = viewModel.BuildVcdContent();

        vcd.Should().Contain("$timescale 10ns $end");
        vcd.Should().Contain("$dumpvars");
        vcd.Should().Contain("#3");
        vcd.Should().Contain("fsm_state");
    }

    private static SimulationSnapshot CreateSnapshot(
        int comboId,
        uint fsmState,
        string fsmStateName,
        ulong cycle,
        double elapsedUs,
        uint rowIndex,
        uint totalRows,
        ushort tGateOn,
        ushort tSettle,
        ushort tLine)
    {
        var registers = FoundationConstants.MakeDefaultRegisters((byte)comboId);
        registers[FoundationConstants.kRegTGateOn] = tGateOn;
        registers[FoundationConstants.kRegTGateSettle] = tSettle;
        registers[FoundationConstants.kRegTLine] = tLine;

        return new SimulationSnapshot(
            Cycle: cycle,
            FsmState: fsmState,
            FsmStateName: fsmStateName,
            Busy: true,
            Done: fsmState == 10U,
            Error: false,
            ErrCode: 0U,
            RowIndex: rowIndex,
            TotalRows: totalRows,
            GateOnPulse: true,
            GateSettle: false,
            ScanActive: true,
            ScanDone: false,
            GateSignals: new SignalMap(),
            AfeReady: true,
            AfeDoutValid: true,
            AfeLineCount: 10U,
            ProtTimeout: false,
            ProtError: false,
            ForceGateOff: false,
            PowerGood: true,
            Registers: registers)
        {
            ElapsedMicroseconds = elapsedUs,
            VglRailVoltage = -10.0,
            VghRailVoltage = 20.0,
            GateOeVoltage = 20.0,
            GateClkVoltage = 3.3,
            AfeSyncVoltage = 3.3,
        };
    }
}
