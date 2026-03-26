using FluentAssertions;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Models;

public sealed class PowerSeqModelTests
{
    [Fact]
    public void PowerUp_ShouldSequenceVglBeforeVgh()
    {
        var model = new PowerSeqModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["target_mode"] = 1U,
            ["vgl_stable"] = 0U,
            ["vgh_stable"] = 0U,
        });

        model.Step();
        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "en_vgl").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "en_vgh").Should().Be(0U);

        model.SetInputs(new SignalMap
        {
            ["target_mode"] = 1U,
            ["vgl_stable"] = 1U,
            ["vgh_stable"] = 0U,
        });
        model.Step();
        SignalHelpers.GetScalar(model.GetOutputs(), "en_vgh").Should().Be(1U);
    }

    [Fact]
    public void PowerGood_ShouldRequireAllRails()
    {
        var model = new PowerSeqModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["target_mode"] = 1U,
            ["vgl_stable"] = 1U,
            ["vgh_stable"] = 1U,
        });

        model.Step();
        SignalHelpers.GetScalar(model.GetOutputs(), "power_good").Should().Be(1U);
    }

    [Fact]
    public void TargetOff_ShouldDisableInReverseOrder()
    {
        var model = new PowerSeqModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["target_mode"] = 1U,
            ["vgl_stable"] = 1U,
            ["vgh_stable"] = 1U,
        });
        model.Step();

        model.SetInputs(new SignalMap
        {
            ["target_mode"] = 6U,
            ["vgl_stable"] = 0U,
            ["vgh_stable"] = 0U,
        });
        model.Step();

        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "en_vgh").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "en_vgl").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "en_avdd1").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "en_dvdd").Should().Be(1U);
    }
}
