using FluentAssertions;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Models;

public sealed class PanelFsmModelTests
{
    [Fact]
    public void StaticCycle_ShouldReachDoneAndVisitSettle()
    {
        var model = new PanelFsmModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["ctrl_start"] = 1U,
            ["cfg_mode"] = 0U,
            ["cfg_nrows"] = 2U,
            ["cfg_treset"] = 1U,
            ["cfg_tinteg"] = 1U,
            ["cfg_nreset"] = 1U,
            ["cfg_sync_dly"] = 1U,
            ["cfg_tgate_settle"] = 1U,
            ["afe_config_done"] = 1U,
            ["gate_row_done"] = 1U,
            ["afe_line_valid"] = 1U,
        });

        var seenSettle = false;
        var seenDone = false;
        for (var i = 0; i < 20; i++)
        {
            model.Step();
            var outputs = model.GetOutputs();
            if (SignalHelpers.GetScalar(outputs, "fsm_state") == 8U)
            {
                seenSettle = true;
            }

            if (SignalHelpers.GetScalar(outputs, "sts_done") == 1U)
            {
                seenDone = true;
                break;
            }
        }

        seenSettle.Should().BeTrue();
        seenDone.Should().BeTrue();
    }

    [Fact]
    public void Abort_ShouldReturnToIdleWithAbortError()
    {
        var model = new PanelFsmModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["ctrl_start"] = 1U,
            ["cfg_mode"] = 0U,
        });

        model.Step();
        model.SetInputs(new SignalMap
        {
            ["ctrl_start"] = 1U,
            ["ctrl_abort"] = 1U,
            ["cfg_mode"] = 0U,
        });
        model.Step();

        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "fsm_state").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "sts_error").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "sts_err_code").Should().Be(1U);
    }

    [Fact]
    public void ContinuousMode_ShouldLoopToState1AfterDone()
    {
        var model = new PanelFsmModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["ctrl_start"] = 1U,
            ["cfg_mode"] = 1U,
            ["cfg_nrows"] = 1U,
            ["cfg_treset"] = 1U,
            ["cfg_tinteg"] = 1U,
            ["cfg_nreset"] = 1U,
            ["cfg_sync_dly"] = 1U,
            ["cfg_tgate_settle"] = 1U,
            ["afe_config_done"] = 1U,
            ["gate_row_done"] = 1U,
            ["afe_line_valid"] = 1U,
        });

        for (var i = 0; i < 20; i++)
        {
            model.Step();
            var outputs = model.GetOutputs();
            if (SignalHelpers.GetScalar(outputs, "sts_done") == 1U)
            {
                SignalHelpers.GetScalar(outputs, "fsm_state").Should().Be(1U);
                return;
            }
        }

        throw new Xunit.Sdk.XunitException("Continuous mode did not complete within the expected cycles.");
    }

    [Fact]
    public void TriggeredMode_ShouldWaitForPrepReq()
    {
        var model = new PanelFsmModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["ctrl_start"] = 1U,
            ["cfg_mode"] = 2U,
            ["cfg_treset"] = 1U,
            ["cfg_nreset"] = 1U,
        });

        for (var i = 0; i < 4; i++)
        {
            model.Step();
        }

        SignalHelpers.GetScalar(model.GetOutputs(), "fsm_state").Should().Be(3U);

        model.Step();
        SignalHelpers.GetScalar(model.GetOutputs(), "fsm_state").Should().Be(3U);

        model.SetInputs(new SignalMap
        {
            ["ctrl_start"] = 1U,
            ["cfg_mode"] = 2U,
            ["cfg_treset"] = 1U,
            ["cfg_nreset"] = 1U,
            ["xray_prep_req"] = 1U,
        });
        model.Step();

        SignalHelpers.GetScalar(model.GetOutputs(), "fsm_state").Should().Be(5U);
    }

    [Fact]
    public void DarkFrameMode_ShouldNotRequireGateRowDone()
    {
        var model = new PanelFsmModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["ctrl_start"] = 1U,
            ["cfg_mode"] = 3U,
            ["cfg_nrows"] = 2U,
            ["cfg_treset"] = 1U,
            ["cfg_tinteg"] = 1U,
            ["cfg_nreset"] = 1U,
            ["cfg_sync_dly"] = 1U,
            ["cfg_tgate_settle"] = 1U,
            ["afe_config_done"] = 1U,
            ["gate_row_done"] = 0U,
            ["afe_line_valid"] = 1U,
        });

        for (var i = 0; i < 7; i++)
        {
            model.Step();
        }

        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "fsm_state").Should().Be(7U);
        SignalHelpers.GetScalar(outputs, "sts_line_idx").Should().Be(1U);
    }

    [Fact]
    public void ProtMonTimeout_ShouldGoToErrorState()
    {
        var model = new PanelFsmModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["ctrl_start"] = 1U,
            ["prot_error"] = 1U,
        });

        model.Step();

        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "fsm_state").Should().Be(15U);
        SignalHelpers.GetScalar(outputs, "sts_error").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "sts_err_code").Should().Be(3U);
    }
}
