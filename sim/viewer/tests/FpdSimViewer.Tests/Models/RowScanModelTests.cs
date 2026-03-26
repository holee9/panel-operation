using FluentAssertions;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Models;

public sealed class RowScanModelTests
{
    [Fact]
    public void ForwardScan_ShouldVisitRowsAndFinish()
    {
        var model = new RowScanModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["scan_start"] = 1U,
            ["scan_dir"] = 0U,
            ["cfg_nrows"] = 2U,
        });

        model.Step();
        var cycle1 = model.GetOutputs();
        SignalHelpers.GetScalar(cycle1, "scan_active").Should().Be(1U);
        SignalHelpers.GetScalar(cycle1, "row_index").Should().Be(0U);
        SignalHelpers.GetScalar(cycle1, "gate_on_pulse").Should().Be(1U);

        model.Step();
        var cycle2 = model.GetOutputs();
        SignalHelpers.GetScalar(cycle2, "gate_on_pulse").Should().Be(0U);
        SignalHelpers.GetScalar(cycle2, "gate_settle").Should().Be(1U);

        model.Step();
        var cycle3 = model.GetOutputs();
        SignalHelpers.GetScalar(cycle3, "row_done").Should().Be(1U);
        SignalHelpers.GetScalar(cycle3, "row_index").Should().Be(1U);
        SignalHelpers.GetScalar(cycle3, "gate_on_pulse").Should().Be(1U);
        SignalHelpers.GetScalar(cycle3, "scan_done").Should().Be(0U);

        model.Step();
        model.Step();
        var cycle5 = model.GetOutputs();
        SignalHelpers.GetScalar(cycle5, "row_done").Should().Be(1U);
        SignalHelpers.GetScalar(cycle5, "scan_done").Should().Be(1U);
        SignalHelpers.GetScalar(cycle5, "scan_active").Should().Be(0U);
        SignalHelpers.GetScalar(cycle5, "row_index").Should().Be(1U);
    }

    [Fact]
    public void ReverseScan_ShouldStartFromLastRow()
    {
        var model = new RowScanModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["scan_start"] = 1U,
            ["scan_dir"] = 1U,
            ["cfg_nrows"] = 3U,
        });

        model.Step();
        var cycle1 = model.GetOutputs();
        SignalHelpers.GetScalar(cycle1, "row_index").Should().Be(2U);
        SignalHelpers.GetScalar(cycle1, "gate_on_pulse").Should().Be(1U);

        model.Step();
        model.Step();
        var cycle3 = model.GetOutputs();
        SignalHelpers.GetScalar(cycle3, "row_index").Should().Be(1U);
        SignalHelpers.GetScalar(cycle3, "row_done").Should().Be(1U);
        SignalHelpers.GetScalar(cycle3, "scan_done").Should().Be(0U);
    }

    [Fact]
    public void Abort_ShouldClearActiveScanOutputs()
    {
        var model = new RowScanModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["scan_start"] = 1U,
            ["cfg_nrows"] = 4U,
        });

        model.Step();
        model.SetInputs(new SignalMap
        {
            ["scan_start"] = 1U,
            ["scan_abort"] = 1U,
            ["cfg_nrows"] = 4U,
        });

        model.Step();
        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "scan_active").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "gate_on_pulse").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "gate_settle").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "scan_done").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "row_done").Should().Be(0U);
    }
}
