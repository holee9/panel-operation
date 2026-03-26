using FluentAssertions;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Models;

public sealed class EmergencyShutdownModelTests
{
    [Fact]
    public void NoFaults_ShouldNotTriggerShutdown()
    {
        var model = new EmergencyShutdownModel();
        model.Reset();

        model.Step();
        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "shutdown_req").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "force_gate_off").Should().Be(0U);
    }

    [Fact]
    public void VghOver_ShouldTriggerShutdown()
    {
        var model = new EmergencyShutdownModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["vgh_over"] = 1U,
        });

        model.Step();
        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "shutdown_req").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "shutdown_code").Should().Be(1U);
    }

    [Fact]
    public void HwEmergency_ShouldForceGateOff()
    {
        var model = new EmergencyShutdownModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["hw_emergency_n"] = 0U,
        });

        model.Step();
        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "shutdown_req").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "force_gate_off").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "shutdown_code").Should().Be(0xEEU);
    }
}
