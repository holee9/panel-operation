using FluentAssertions;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Models;

public sealed class ProtMonModelTests
{
    [Fact]
    public void ResetState_ShouldHaveNoErrors()
    {
        var model = new ProtMonModel();
        model.Reset();

        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "err_timeout").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "err_flag").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "force_gate_off").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "exposure_count").Should().Be(0U);
    }

    [Fact]
    public void ExposureTimeout_ShouldSetErrTimeout()
    {
        var model = new ProtMonModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["fsm_state"] = 5U,
            ["xray_active"] = 1U,
            ["cfg_max_exposure"] = 2U,
        });

        model.Step();
        model.Step();

        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "err_timeout").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "err_flag").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "force_gate_off").Should().Be(1U);
    }

    [Fact]
    public void IdleState_ShouldClearExposureCount()
    {
        var model = new ProtMonModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["fsm_state"] = 4U,
            ["xray_active"] = 1U,
            ["cfg_max_exposure"] = 10U,
        });
        model.Step();
        model.Step();

        model.SetInputs(new SignalMap
        {
            ["fsm_state"] = 0U,
            ["xray_active"] = 0U,
        });
        model.Step();

        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "exposure_count").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "err_timeout").Should().Be(0U);
    }
}
