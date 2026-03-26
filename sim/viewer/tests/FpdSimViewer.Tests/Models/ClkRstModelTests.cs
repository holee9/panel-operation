using FluentAssertions;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Models;

public sealed class ClkRstModelTests
{
    [Fact]
    public void ResetSync_ShouldFollowExternalReset()
    {
        var model = new ClkRstModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["rst_ext_n"] = 0U,
        });

        model.Step();
        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "pll_locked").Should().Be(0U);
        SignalHelpers.GetScalar(outputs, "rst_sync").Should().Be(0U);
    }

    [Fact]
    public void PllLock_ShouldSetAfterCounter()
    {
        var model = new ClkRstModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["rst_ext_n"] = 1U,
        });

        for (var i = 0; i < 16; i++)
        {
            model.Step();
        }

        var outputs = model.GetOutputs();
        SignalHelpers.GetScalar(outputs, "pll_locked").Should().Be(1U);
        SignalHelpers.GetScalar(outputs, "rst_sync").Should().Be(1U);
    }

    [Fact]
    public void ClockOutputs_ShouldToggle()
    {
        var model = new ClkRstModel();
        model.Reset();
        model.SetInputs(new SignalMap
        {
            ["rst_ext_n"] = 1U,
            ["afe_type_sel"] = 2U,
        });

        var seenAclkChange = false;
        var seenMclkChange = false;
        var initialAclk = 0U;
        var initialMclk = 0U;

        for (var i = 0; i < 20; i++)
        {
            model.Step();
            var outputs = model.GetOutputs();
            if (i == 0)
            {
                initialAclk = SignalHelpers.GetScalar(outputs, "clk_aclk");
                initialMclk = SignalHelpers.GetScalar(outputs, "clk_mclk");
            }

            seenAclkChange |= SignalHelpers.GetScalar(outputs, "clk_aclk") != initialAclk;
            seenMclkChange |= SignalHelpers.GetScalar(outputs, "clk_mclk") != initialMclk;
        }

        seenAclkChange.Should().BeTrue();
        seenMclkChange.Should().BeTrue();
    }
}
