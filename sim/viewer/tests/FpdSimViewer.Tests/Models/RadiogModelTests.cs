using FluentAssertions;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Models;

public sealed class RadiogModelTests
{
    [Fact]
    public void DarkFrameCapture_ShouldAccumulate()
    {
        var model = new RadiogModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["start"] = 1U,
            ["dark_frame_mode"] = 1U,
            ["frame_valid"] = 1U,
            ["cfg_dark_cnt"] = 2U,
            ["frame_pixels"] = new ushort[] { 10, 20, 30, 40 },
        });

        model.Step();
        SignalHelpers.GetScalar(model.GetOutputs(), "dark_frames_captured").Should().Be(1U);

        model.SetInputs(new SignalMap
        {
            ["dark_frame_mode"] = 1U,
            ["frame_valid"] = 0U,
            ["cfg_dark_cnt"] = 2U,
            ["frame_pixels"] = new ushort[] { 10, 20, 30, 40 },
        });
        model.Step();

        model.SetInputs(new SignalMap
        {
            ["dark_frame_mode"] = 1U,
            ["frame_valid"] = 1U,
            ["cfg_dark_cnt"] = 2U,
            ["frame_pixels"] = new ushort[] { 14, 24, 34, 44 },
        });
        model.Step();

        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "dark_frames_captured").Should().Be(2U);
        SignalHelpers.GetScalar(outputs, "dark_avg_ready").Should().Be(1U);
        SignalHelpers.GetVector(outputs, "dark_avg_frame").Should().Equal(new ushort[] { 12, 22, 32, 42 });
    }

    [Fact]
    public void XrayHandshake_ShouldSetEnable()
    {
        var model = new RadiogModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["start"] = 1U,
        });
        model.Step();

        model.SetInputs(new SignalMap
        {
            ["start"] = 1U,
            ["xray_ready"] = 1U,
        });
        model.Step();

        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "state").Should().Be(2U);
        SignalHelpers.GetScalar(outputs, "xray_enable").Should().Be(1U);
    }

    [Fact]
    public void SettleDelay_ShouldWaitBeforeReadout()
    {
        var model = new RadiogModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["start"] = 1U,
            ["cfg_tsettle"] = 2U,
        });
        model.Step();

        model.SetInputs(new SignalMap
        {
            ["start"] = 1U,
            ["xray_ready"] = 1U,
            ["cfg_tsettle"] = 2U,
        });
        model.Step();

        model.SetInputs(new SignalMap
        {
            ["xray_on"] = 1U,
            ["cfg_tsettle"] = 2U,
        });
        model.Step();

        model.SetInputs(new SignalMap
        {
            ["xray_off"] = 1U,
            ["cfg_tsettle"] = 2U,
        });
        model.Step();
        SignalHelpers.GetScalar(model.GetOutputs(), "state").Should().Be(3U);

        model.Step();
        SignalHelpers.GetScalar(model.GetOutputs(), "done").Should().Be(0U);

        model.Step();
        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "done").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "state").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "xray_enable").Should().Be(0U);
    }
}
