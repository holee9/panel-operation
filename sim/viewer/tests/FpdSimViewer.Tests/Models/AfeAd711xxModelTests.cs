using FluentAssertions;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Models;

public sealed class AfeAd711xxModelTests
{
    [Fact]
    public void ConfigHandshake_ShouldSetConfigDone()
    {
        var model = new AfeAd711xxModel();
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
    public void TLineUnderMin_ShouldSetTlineError()
    {
        var model = new AfeAd711xxModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["config_req"] = 1U,
            ["cfg_tline"] = 5000U,
            ["afe_type"] = 1U,
        });

        model.Step();
        SignalHelpers.GetScalar(model.GetOutputs(), "tline_error").Should().Be(1U);
    }

    [Fact]
    public void AfeStart_ShouldProduceDoutValid()
    {
        var model = new AfeAd711xxModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["config_req"] = 1U,
            ["cfg_tline"] = 2200U,
            ["cfg_nchip"] = 2U,
            ["cfg_ifs"] = 3U,
        });
        model.Step();

        model.SetInputs(new SignalMap
        {
            ["afe_start"] = 1U,
            ["cfg_tline"] = 2200U,
            ["cfg_nchip"] = 2U,
            ["cfg_ifs"] = 3U,
        });
        model.Step();

        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "dout_window_valid").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "line_count").Should().Be(1U);
        SignalHelpers.GetVector(outputs, "sample_line").Should().NotBeEmpty();
    }
}
