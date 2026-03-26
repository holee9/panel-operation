using FluentAssertions;
using FpdSimViewer.Engine;
using FpdSimViewer.Models;

namespace FpdSimViewer.Tests.Engine;

public sealed class HardwareComboConfigTests
{
    [Fact]
    public void CreateC1_ShouldReturnNv1047AndAd711xx()
    {
        var config = HardwareComboConfig.Create(1);

        config.ComboId.Should().Be(1);
        config.Rows.Should().Be(2048U);
        config.Cols.Should().Be(2048U);
        config.AfeChips.Should().Be(1U);
        config.GateDriver.Should().BeOfType<GateNv1047Model>();
        config.AfeModel.Should().BeOfType<AfeAd711xxModel>();
        config.AfeName.Should().Be("AD71124");
    }

    [Fact]
    public void CreateC7_ShouldReturnNt39565dAndAfe2256()
    {
        var config = HardwareComboConfig.Create(7);

        config.ComboId.Should().Be(7);
        config.Rows.Should().Be(3072U);
        config.Cols.Should().Be(3072U);
        config.AfeChips.Should().Be(12U);
        config.GateDriver.Should().BeOfType<GateNt39565dModel>();
        config.AfeModel.Should().BeOfType<AfeAfe2256Model>();
        config.AfeName.Should().Be("AFE2256");
    }
}
