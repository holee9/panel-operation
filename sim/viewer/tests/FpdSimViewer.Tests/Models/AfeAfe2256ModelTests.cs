using FluentAssertions;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Models;

public sealed class AfeAfe2256ModelTests
{
    [Fact]
    public void ConfigHandshake_ShouldSetConfigDone()
    {
        var model = new AfeAfe2256Model();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["config_req"] = 1U,
        });

        model.Step();
        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "config_done").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "afe_ready").Should().Be(1U);
    }

    [Fact]
    public void CicEnable_ShouldSetPipelineLatency()
    {
        var model = new AfeAfe2256Model();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["config_req"] = 1U,
            ["cfg_tline"] = 5120U,
        });
        model.Step();

        model.SetInputs(new SignalMap
        {
            ["afe_start"] = 1U,
            ["cfg_tline"] = 5120U,
            ["cfg_cic_en"] = 1U,
            ["cfg_cic_profile"] = 2U,
            ["cfg_pipeline_en"] = 1U,
        });
        model.Step();

        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "pipeline_latency_rows").Should().BeGreaterThan(0U);
        SignalHelpers.GetScalar(outputs, "dout_window_valid").Should().Be(1U);
        SignalHelpers.GetVector(outputs, "current_row").Should().NotBeEmpty();
    }

    [Fact]
    public void TLineUnderMin_ShouldSetTlineError()
    {
        var model = new AfeAfe2256Model();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["cfg_tline"] = 4000U,
        });

        model.Step();
        SignalHelpers.GetScalar(model.GetOutputs(), "tline_error").Should().Be(1U);
    }
}
