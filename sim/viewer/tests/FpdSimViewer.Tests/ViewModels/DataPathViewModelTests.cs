using FluentAssertions;
using FpdSimViewer.Engine;
using FpdSimViewer.Models.Core;
using FpdSimViewer.ViewModels;

namespace FpdSimViewer.Tests.ViewModels;

public sealed class DataPathViewModelTests
{
    [Fact]
    public void UpdateFromSnapshot_ShouldReflectPingPongBanksAndPacketSummary()
    {
        var viewModel = new DataPathViewModel();
        var combo = HardwareComboConfig.Create(6);
        var snapshot = CreateSnapshot(
            comboId: combo.ComboId,
            rowIndex: 1U,
            totalRows: combo.Rows,
            afeReady: true,
            afeDoutValid: true,
            protError: false,
            currentRowPixels: Enumerable.Range(0, 256).Select(index => (ushort)((index * 13) & 0x0FFF)).ToArray());

        viewModel.UpdateFromSnapshot(snapshot, combo);

        viewModel.PathSummary.Should().Contain("AD71124 x12");
        viewModel.BufferBanks[1].Role.Should().Be("Write");
        viewModel.BufferBanks[0].Role.Should().Be("CSI-2 TX");
        viewModel.CsiPacketSummary.Should().Contain("DT 0x2B");
        viewModel.CsiPacketSummary.Should().Contain("Lines(1)");
    }

    [Fact]
    public void UpdateFromSnapshot_ShouldExposeIntegrityFailureAndIdlePreview()
    {
        var viewModel = new DataPathViewModel();
        var combo = HardwareComboConfig.Create(3);
        var snapshot = CreateSnapshot(
            comboId: combo.ComboId,
            rowIndex: 0U,
            totalRows: combo.Rows,
            afeReady: false,
            afeDoutValid: false,
            protError: true,
            currentRowPixels: []);

        viewModel.UpdateFromSnapshot(snapshot, combo);

        viewModel.BufferBanks[0].Role.Should().Be("Standby");
        viewModel.BufferBanks[1].Role.Should().Be("Standby");
        viewModel.CsiIntegritySummary.Should().Contain("FAIL");
        viewModel.PixelPreviewSummary.Should().Contain("preview idle");
    }

    private static SimulationSnapshot CreateSnapshot(
        int comboId,
        uint rowIndex,
        uint totalRows,
        bool afeReady,
        bool afeDoutValid,
        bool protError,
        ushort[] currentRowPixels)
    {
        return new SimulationSnapshot(
            Cycle: 42,
            FsmState: 7U,
            FsmStateName: "READOUT",
            Busy: true,
            Done: false,
            Error: false,
            ErrCode: 0U,
            RowIndex: rowIndex,
            TotalRows: totalRows,
            GateOnPulse: true,
            GateSettle: false,
            ScanActive: true,
            ScanDone: false,
            GateSignals: new SignalMap(),
            AfeReady: afeReady,
            AfeDoutValid: afeDoutValid,
            AfeLineCount: 128U,
            ProtTimeout: false,
            ProtError: protError,
            ForceGateOff: false,
            PowerGood: true,
            Registers: FoundationConstants.MakeDefaultRegisters((byte)comboId))
        {
            AfePhaseLabel = "OUT",
            AfePhaseProgress = 0.5,
            CurrentRowPixels = currentRowPixels,
            ElapsedMicroseconds = 0.42,
        };
    }
}
