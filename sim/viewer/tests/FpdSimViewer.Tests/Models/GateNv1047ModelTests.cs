using FluentAssertions;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Models;

public sealed class GateNv1047ModelTests
{
    [Fact]
    public void ResetState_ShouldHaveOeHighAndRstHigh()
    {
        var model = new GateNv1047Model();
        model.Reset();

        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "nv_oe").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "nv_rst").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "nv_ona").Should().Be(1U);
    }

    [Fact]
    public void GateOnPulse_ShouldTriggerBbmAndSdShift()
    {
        var model = new GateNv1047Model();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["row_index"] = 5U,
            ["gate_on_pulse"] = 1U,
            ["cfg_clk_period"] = 2U,
            ["cfg_gate_settle"] = 2U,
        });

        model.Step();
        var activeOutputs = model.GetOutputs();
        SignalHelpers.GetScalar(activeOutputs, "nv_sd1").Should().Be(1U);
        SignalHelpers.GetScalar(activeOutputs, "nv_sd2").Should().Be(0U);
        SignalHelpers.GetScalar(activeOutputs, "nv_oe").Should().Be(0U);

        model.SetInputs(new SignalMap
        {
            ["row_index"] = 5U,
            ["gate_on_pulse"] = 0U,
            ["cfg_clk_period"] = 2U,
            ["cfg_gate_settle"] = 2U,
        });
        model.Step();
        SignalHelpers.GetScalar(model.GetOutputs(), "row_done").Should().Be(0U);

        model.Step();
        SignalHelpers.GetScalar(model.GetOutputs(), "row_done").Should().Be(0U);

        model.Step();
        var settledOutputs = model.GetOutputs();
        SignalHelpers.GetScalar(settledOutputs, "row_done").Should().Be(1U);
        SignalHelpers.GetScalar(settledOutputs, "nv_oe").Should().Be(1U);
    }

    [Fact]
    public void ResetAll_ShouldDeactivateAllOutputs()
    {
        var model = new GateNv1047Model();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["gate_on_pulse"] = 1U,
            ["reset_all"] = 1U,
        });

        model.Step();
        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "nv_ona").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "nv_rst").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "nv_oe").Should().Be(1U);
    }
}
