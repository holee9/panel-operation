using FluentAssertions;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Models;

public sealed class GateNt39565dModelTests
{
    [Fact]
    public void ResetState_ShouldClearAllStvAndOe()
    {
        var model = new GateNt39565dModel();
        model.Reset();

        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "stv1l").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "stv2l").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "oe1l").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "oe2r").Should().Be(0U);
    }

    [Fact]
    public void GateOnPulse_ShouldActivateDualStv()
    {
        var model = new GateNt39565dModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["row_index"] = 0U,
            ["gate_on_pulse"] = 1U,
            ["chip_sel"] = 0U,
        });

        model.Step();
        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "stv1l").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "stv2l").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "stv1r").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "oe1l").Should().Be(1U);
    }

    [Fact]
    public void CascadeComplete_ShouldSetOnStvReturn()
    {
        var model = new GateNt39565dModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["row_index"] = 0U,
            ["gate_on_pulse"] = 1U,
            ["chip_sel"] = 2U,
            ["mode_sel"] = 1U,
            ["cascade_stv_return"] = 1U,
        });

        model.Step();
        SignalHelpers.GetScalar(model.GetOutputs(), "cascade_complete").Should().Be(1U);
    }
}
