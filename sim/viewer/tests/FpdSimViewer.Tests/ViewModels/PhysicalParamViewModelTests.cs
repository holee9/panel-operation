using FluentAssertions;
using FpdSimViewer.Engine;
using FpdSimViewer.Models.Core;
using FpdSimViewer.ViewModels;

namespace FpdSimViewer.Tests.ViewModels;

public sealed class PhysicalParamViewModelTests
{
    [Fact]
    public void UpdateFromSnapshot_ShouldReflectInitialRegisterState()
    {
        var engine = new SimulationEngine();
        var viewModel = new PhysicalParamViewModel(engine, _ => { });

        viewModel.UpdateFromSnapshot(engine.CurrentSnapshot, engine.ComboConfig);

        viewModel.GateOnMicroseconds.Should().Be(22.0);
        viewModel.GateSettleMicroseconds.Should().Be(1.0);
        viewModel.LineMicroseconds.Should().Be(22.0);
        viewModel.NResetScans.Should().Be(3);
        viewModel.IsCicAvailable.Should().BeFalse();
    }

    [Fact]
    public void ChangingGateOnSlider_ShouldWriteRegisterAndRefreshSnapshot()
    {
        var engine = new SimulationEngine();
        SimulationSnapshot? callbackSnapshot = null;
        var viewModel = new PhysicalParamViewModel(engine, snapshot => callbackSnapshot = snapshot);
        viewModel.UpdateFromSnapshot(engine.CurrentSnapshot, engine.ComboConfig);

        viewModel.GateOnMicroseconds = 25.0;

        engine.CurrentSnapshot.Registers[FoundationConstants.kRegTGateOn].Should().Be(2500);
        callbackSnapshot.Should().NotBeNull();
    }

    [Fact]
    public void UpdateFromSnapshot_ShouldReadBackAfe2256Registers()
    {
        var engine = new SimulationEngine();
        var viewModel = new PhysicalParamViewModel(engine, _ => { });
        engine.SetCombo(3);
        engine.WriteRegister(FoundationConstants.kRegCicEn, 1);
        engine.WriteRegister(FoundationConstants.kRegCicProfile, 4);
        engine.WriteRegister(FoundationConstants.kRegTLine, 6000);
        var snapshot = engine.RefreshSnapshot();

        viewModel.UpdateFromSnapshot(snapshot, engine.ComboConfig);

        viewModel.IsCicAvailable.Should().BeTrue();
        viewModel.CicEnabled.Should().BeTrue();
        viewModel.CicProfile.Should().Be(4);
        viewModel.LineMicroseconds.Should().Be(60.0);
    }
}
