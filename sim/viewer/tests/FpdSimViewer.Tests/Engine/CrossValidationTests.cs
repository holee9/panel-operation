using FluentAssertions;
using FpdSimViewer.Engine;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Engine;

public sealed class CrossValidationTests
{
    [Fact]
    public void RegBank_DefaultsMatchCpp()
    {
        foreach (byte combo in new byte[] { 1, 2, 3, 4, 5, 6, 7 })
        {
            var model = new RegBankModel();
            if (combo != FoundationConstants.kComboC1)
            {
                model.Write(FoundationConstants.kRegCombo, combo);
            }

            var expected = FoundationConstants.MakeDefaultRegisters(combo);
            for (byte address = 0; address < expected.Length; address++)
            {
                model.Read(address).Should().Be(expected[address], $"combo C{combo} register 0x{address:X2} should match the C++ default scenario");
            }
        }
    }

    [Fact]
    public void PanelFsm_StaticCycleSequence()
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

        var sequence = new List<uint> { SignalHelpers.GetScalar(model.GetOutputs(), "fsm_state") };
        for (var step = 0; step < 12 && sequence[^1] != 10U; step++)
        {
            model.Step();
            sequence.Add(SignalHelpers.GetScalar(model.GetOutputs(), "fsm_state"));
        }

        sequence.Should().Equal(0U, 1U, 2U, 2U, 4U, 6U, 7U, 7U, 8U, 9U, 10U);
    }

    [Fact]
    public void GateNv1047_BbmTiming()
    {
        var model = new GateNv1047Model();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["row_index"] = 12U,
            ["gate_on_pulse"] = 1U,
            ["cfg_clk_period"] = 2U,
            ["cfg_gate_settle"] = 2U,
        });

        model.Step();
        SignalHelpers.GetScalar(model.GetOutputs(), "nv_oe").Should().Be(0U);
        SignalHelpers.GetScalar(model.GetOutputs(), "row_done").Should().Be(0U);

        model.SetInputs(new SignalMap
        {
            ["row_index"] = 12U,
            ["gate_on_pulse"] = 0U,
            ["cfg_clk_period"] = 2U,
            ["cfg_gate_settle"] = 2U,
        });

        model.Step();
        SignalHelpers.GetScalar(model.GetOutputs(), "row_done").Should().Be(0U);

        model.Step();
        SignalHelpers.GetScalar(model.GetOutputs(), "row_done").Should().Be(0U);

        model.Step();
        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "row_done").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "nv_oe").Should().Be(1U);
    }

    [Fact]
    public void AfeAd711xx_TLineClampBehavior()
    {
        var ad71124 = new AfeAd711xxModel();
        ad71124.Reset();
        ad71124.SetInputs(new SignalMap
        {
            ["config_req"] = 1U,
            ["cfg_tline"] = 2200U,
            ["afe_type"] = 0U,
        });
        ad71124.Step();
        SignalHelpers.GetScalar(ad71124.GetOutputs(), "tline_error").Should().Be(0U);

        var ad71143Bad = new AfeAd711xxModel();
        ad71143Bad.Reset();
        ad71143Bad.SetInputs(new SignalMap
        {
            ["config_req"] = 1U,
            ["cfg_tline"] = 5000U,
            ["afe_type"] = 1U,
        });
        ad71143Bad.Step();
        SignalHelpers.GetScalar(ad71143Bad.GetOutputs(), "tline_error").Should().Be(1U);

        var ad71143Good = new AfeAd711xxModel();
        ad71143Good.Reset();
        ad71143Good.SetInputs(new SignalMap
        {
            ["config_req"] = 1U,
            ["cfg_tline"] = 6000U,
            ["afe_type"] = 1U,
        });
        ad71143Good.Step();
        SignalHelpers.GetScalar(ad71143Good.GetOutputs(), "tline_error").Should().Be(0U);
    }

    [Fact]
    public void FullEngine_C6StaticCycle()
    {
        var engine = new SimulationEngine();
        engine.SetCombo(6);
        ConfigureFastCycle(engine, mode: 0U, nrows: 2);

        SimulationSnapshot snapshot = engine.CurrentSnapshot;
        for (var i = 0; i < 400; i++)
        {
            snapshot = engine.Step();
            if (snapshot.Done)
            {
                break;
            }
        }

        snapshot.Done.Should().BeTrue();
        snapshot.FsmState.Should().Be(0U);
        engine.ComboConfig.GateIcName.Should().Be("NT39565D");
        engine.ComboConfig.AfeName.Should().Be("AD71124");
        engine.CycleCount.Should().BeGreaterThan(0UL);
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
