using FluentAssertions;
using FpdSimViewer.Engine;
using FpdSimViewer.Models.Core;
using FpdSimViewer.ViewModels;

namespace FpdSimViewer.Tests.ViewModels;

public sealed class OperationMonitorViewModelTests
{
    [Fact]
    public void Constructor_ShouldSeedInitialCollections()
    {
        var viewModel = new OperationMonitorViewModel();

        viewModel.PipelineStates.Should().HaveCount(6);
        viewModel.PanelSegments.Should().HaveCount(72);
        viewModel.ScopeChannels.Should().BeEmpty();
        viewModel.TimeScaleLabel.Should().Be("50 us/div");
        viewModel.CycleSummary.Should().Be("Cycle 0");
    }

    [Fact]
    public void UpdateFromSnapshot_ShouldPopulateProgressAndHeatmap()
    {
        var viewModel = new OperationMonitorViewModel();
        var combo = HardwareComboConfig.Create(1);
        var snapshot = CreateSnapshot(
            comboId: combo.ComboId,
            fsmState: 7U,
            fsmStateName: "READOUT",
            rowIndex: 512U,
            totalRows: 2048U,
            currentRowPixels: Enumerable.Range(0, 64).Select(index => (ushort)((index * 17) & 0x0FFF)).ToArray());

        viewModel.UpdateFromSnapshot(snapshot, combo);

        viewModel.RowProgressText.Should().Contain("512");
        viewModel.PixelHeatmap.Should().NotBeNull();
        viewModel.ScopeChannels.Should().HaveCount(6);
        viewModel.PipelineStates[3].Detail.Should().Be("Now");
    }

    [Fact]
    public void UpdateFromSnapshot_ShouldReplaceScopeChannelsWhenComboChanges()
    {
        var viewModel = new OperationMonitorViewModel();
        var combo1 = HardwareComboConfig.Create(1);
        var combo6 = HardwareComboConfig.Create(6);

        viewModel.UpdateFromSnapshot(CreateSnapshot(combo1.ComboId, 7U, "READOUT", 1U, combo1.Rows, [1, 2, 3]), combo1);
        var firstChannelName = viewModel.ScopeChannels[0].Name;

        viewModel.UpdateFromSnapshot(CreateSnapshot(combo6.ComboId, 7U, "READOUT", 1U, combo6.Rows, [1, 2, 3]), combo6);

        firstChannelName.Should().Be("Ch1 Gate OE");
        viewModel.ScopeChannels[0].Name.Should().Be("Ch1 OE1 (L+R)");
    }

    private static SimulationSnapshot CreateSnapshot(
        int comboId,
        uint fsmState,
        string fsmStateName,
        uint rowIndex,
        uint totalRows,
        ushort[] currentRowPixels)
    {
        return new SimulationSnapshot(
            Cycle: 42,
            FsmState: fsmState,
            FsmStateName: fsmStateName,
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
            AfeReady: true,
            AfeDoutValid: true,
            AfeLineCount: 128U,
            ProtTimeout: false,
            ProtError: false,
            ForceGateOff: false,
            PowerGood: true,
            Registers: FoundationConstants.MakeDefaultRegisters((byte)comboId))
        {
            AfePhaseLabel = "OUT",
            AfePhaseProgress = 0.5,
            CurrentRowPixels = currentRowPixels,
            ElapsedMicroseconds = 0.42,
            GateOeVoltage = 20.0,
            GateClkVoltage = 3.3,
            AfeSyncVoltage = 3.3,
            VglRailVoltage = -10.0,
            VghRailVoltage = 20.0,
        };
    }
}
