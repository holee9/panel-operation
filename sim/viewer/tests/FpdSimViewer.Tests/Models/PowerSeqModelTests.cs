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

    [Fact]
    public void RailOutputs_ShouldRampTowardTargets()
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
        var firstStep = model.GetOutputs();
        SignalHelpers.GetScalar(firstStep, "vgl_rail_voltage").Should().BeLessOrEqualTo(1500U);
        SignalHelpers.GetScalar(firstStep, "vgh_rail_voltage").Should().BeGreaterOrEqualTo(0U);

        for (var index = 0; index < 1000; index++)
        {
            model.Step();
        }

        var laterStep = model.GetOutputs();
        SignalHelpers.GetScalar(laterStep, "vgl_rail_voltage").Should().BeLessThan(SignalHelpers.GetScalar(firstStep, "vgl_rail_voltage"));
        SignalHelpers.GetScalar(laterStep, "vgh_rail_voltage").Should().BeGreaterThan(SignalHelpers.GetScalar(firstStep, "vgh_rail_voltage"));
    }
}
