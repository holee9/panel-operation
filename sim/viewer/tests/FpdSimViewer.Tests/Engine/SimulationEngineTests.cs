using FluentAssertions;
using FpdSimViewer.Engine;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Engine;

public sealed class SimulationEngineTests
{
    [Fact]
    public void Reset_ShouldInitializeAllModels()
    {
        var engine = new SimulationEngine();
        engine.Reset();

        engine.CurrentSnapshot.FsmState.Should().Be(0U);
        engine.CurrentSnapshot.RowIndex.Should().Be(0U);
        engine.CycleCount.Should().Be(0UL);
        engine.TraceCapture.Count.Should().Be(0);
    }

    [Fact]
    public void StaticCycle_ShouldCompleteSingleFrame()
    {
        var engine = new SimulationEngine();
        engine.SetCombo(1);
        engine.SetMode(0U);
        engine.WriteRegister(FoundationConstants.kRegNRows, 1);
        engine.WriteRegister(FoundationConstants.kRegTReset, 1);
        engine.WriteRegister(FoundationConstants.kRegTInteg, 1);
        engine.WriteRegister(FoundationConstants.kRegNReset, 1);
        engine.WriteRegister(FoundationConstants.kRegSyncDly, 1);
        engine.WriteRegister(FoundationConstants.kRegTGateSettle, 1);
        engine.WriteRegister(FoundationConstants.kRegCtrl, 0x0001);

        SimulationSnapshot snapshot = engine.CurrentSnapshot;
        for (var i = 0; i < 20; i++)
        {
            snapshot = engine.Step();
            if (snapshot.Done)
            {
                break;
            }
        }

        snapshot.Done.Should().BeTrue();
        snapshot.FsmState.Should().Be(0U);
        engine.TraceCapture.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ComboSwitch_ShouldReconfigureGateAndAfe()
    {
        var engine = new SimulationEngine();
        engine.SetCombo(1);
        engine.ComboConfig.GateDriver.Should().BeOfType<GateNv1047Model>();

        engine.SetCombo(6);

        engine.ComboConfig.GateDriver.Should().BeOfType<GateNt39565dModel>();
        engine.ComboConfig.AfeModel.Should().BeOfType<AfeAd711xxModel>();
        engine.ComboConfig.AfeChips.Should().Be(12U);
    }

    [Fact]
    public void WriteRegister_ShouldUpdateRegBank()
    {
        var engine = new SimulationEngine();
        engine.WriteRegister(FoundationConstants.kRegTLine, 5000);

        var snapshot = engine.Step();

        snapshot.Registers[FoundationConstants.kRegTLine].Should().Be(5000);
    }
}
