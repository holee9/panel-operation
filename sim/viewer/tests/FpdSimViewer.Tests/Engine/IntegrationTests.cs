using FluentAssertions;
using FpdSimViewer.Engine;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Engine;

public sealed class IntegrationTests
{
    [Fact]
    public void Engine_1000Steps_ShouldNotThrow()
    {
        var engine = new SimulationEngine();
        ConfigureFastCycle(engine, mode: 0U, nrows: 4);

        Action act = () =>
        {
            for (var i = 0; i < 1000; i++)
            {
                engine.Step();
            }
        };

        act.Should().NotThrow();
        engine.TraceCapture.Count.Should().BeLessOrEqualTo(1000);
    }

    [Fact]
    public void Engine_AllCombos_ShouldComplete()
    {
        foreach (var combo in Enumerable.Range(1, 7))
        {
            var engine = new SimulationEngine();
            engine.SetCombo(combo);
            ConfigureFastCycle(engine, mode: 0U, nrows: 4);

            SimulationSnapshot snapshot = engine.CurrentSnapshot;
            for (var i = 0; i < 400; i++)
            {
                snapshot = engine.Step();
                if (snapshot.Done)
                {
                    break;
                }
            }

            snapshot.Done.Should().BeTrue($"combo C{combo} should complete a reduced static frame");
        }
    }

    [Fact]
    public void Engine_AllModes_ShouldNotCrash()
    {
        foreach (var mode in new uint[] { 0U, 1U, 2U, 3U, 4U })
        {
            var engine = new SimulationEngine();
            ConfigureFastCycle(engine, mode, nrows: 2);

            if (mode == 2U)
            {
                engine.SetXrayInputs(ready: true, prepReq: true, xrayOn: true, xrayOff: false);
            }

            Action act = () =>
            {
                for (var i = 0; i < 100; i++)
                {
                    engine.Step();
                }
            };

            act.Should().NotThrow($"mode {mode} should remain executable for 100 steps");
            engine.CycleCount.Should().Be(100UL);
        }
    }

    [Fact]
    public void Engine_ComboSwitchMidRun_ShouldResetCleanly()
    {
        var engine = new SimulationEngine();
        ConfigureFastCycle(engine, mode: 0U, nrows: 4);

        for (var i = 0; i < 50; i++)
        {
            engine.Step();
        }

        engine.SetCombo(6);

        engine.CycleCount.Should().Be(0UL);
        engine.CurrentSnapshot.FsmState.Should().Be(0U);
        engine.ComboConfig.ComboId.Should().Be(6);

        ConfigureFastCycle(engine, mode: 0U, nrows: 4);
        Action act = () =>
        {
            for (var i = 0; i < 50; i++)
            {
                engine.Step();
            }
        };

        act.Should().NotThrow();
        engine.TraceCapture.Count.Should().BeGreaterThan(0);
    }

    private static void ConfigureFastCycle(SimulationEngine engine, uint mode, ushort nrows)
    {
        engine.SetMode(mode);
        engine.WriteRegister(FoundationConstants.kRegNRows, nrows);
        if (engine.ComboConfig.AfeModel is AfeAfe2256Model)
        {
            engine.WriteRegister(FoundationConstants.kRegTLine, 5120);
        }

        engine.WriteRegister(FoundationConstants.kRegTReset, 1);
        engine.WriteRegister(FoundationConstants.kRegTInteg, 1);
        engine.WriteRegister(FoundationConstants.kRegNReset, 1);
        engine.WriteRegister(FoundationConstants.kRegSyncDly, 1);
        engine.WriteRegister(FoundationConstants.kRegTGateSettle, 1);
        engine.WriteRegister(FoundationConstants.kRegCtrl, 0x0001);
    }
}
