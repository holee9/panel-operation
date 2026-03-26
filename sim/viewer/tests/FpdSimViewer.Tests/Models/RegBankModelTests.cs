using FluentAssertions;
using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Models;

public sealed class RegBankModelTests
{
    [Fact]
    public void RegisterDefaultsAndReadOnlyRules_ShouldMatchCppBehavior()
    {
        var model = new RegBankModel();

        model.Read(FoundationConstants.kRegVersion).Should().Be(FoundationConstants.kVersion10);

        model.Write(FoundationConstants.kRegMode, 0x0004);
        model.Read(FoundationConstants.kRegMode).Should().Be(0x0004);

        model.Write(FoundationConstants.kRegVersion, 0xABCD);
        model.Read(FoundationConstants.kRegVersion).Should().Be(FoundationConstants.kVersion10);
    }

    [Fact]
    public void StatusMirrorsAndComboClamps_ShouldMatchCppTests()
    {
        var model = new RegBankModel();

        model.SetStatus(true, true, false, true, 0x0123, 0x55);
        model.Read(FoundationConstants.kRegStatus).Should().Be(0x000B);
        model.Read(FoundationConstants.kRegLineIdx).Should().Be(0x0123);
        model.Read(FoundationConstants.kRegErrCode).Should().Be(0x0055);
        model.Read(FoundationConstants.kRegNReset).Should().Be(0x0003);

        model.Write(FoundationConstants.kRegAfeLpf, 0x0007);
        model.Read(FoundationConstants.kRegAfeLpf).Should().Be(0x0007);

        model.Write(FoundationConstants.kRegCombo, FoundationConstants.kComboC6);
        model.Read(FoundationConstants.kRegNCols).Should().Be(3072);
        model.Read(FoundationConstants.kRegTLine).Should().Be(2200);

        model.Write(FoundationConstants.kRegNCols, 4095);
        model.Read(FoundationConstants.kRegNCols).Should().Be(3072);

        model.Write(FoundationConstants.kRegCombo, FoundationConstants.kComboC2);
        model.Write(FoundationConstants.kRegTLine, 1024);
        model.Read(FoundationConstants.kRegTLine).Should().Be(6000);

        model.Write(FoundationConstants.kRegTIntegHi, 0x01FF);
        model.Read(FoundationConstants.kRegTIntegHi).Should().Be(0x00FF);
    }

    [Fact]
    public void TLineClampFlag_ShouldMatchCppTests()
    {
        var model = new RegBankModel();
        model.Write(FoundationConstants.kRegCombo, FoundationConstants.kComboC1);
        model.ClearTLineClamped();
        model.Write(FoundationConstants.kRegTLine, 5000);
        model.TLineClamped.Should().BeFalse();

        model.Write(FoundationConstants.kRegCombo, FoundationConstants.kComboC2);
        model.TLineClamped.Should().BeTrue();
        model.Read(FoundationConstants.kRegTLine).Should().Be(6000);

        var model2 = new RegBankModel();
        model2.Write(FoundationConstants.kRegCombo, FoundationConstants.kComboC3);
        model2.ClearTLineClamped();
        model2.Write(FoundationConstants.kRegTLine, 1000);
        model2.TLineClamped.Should().BeTrue();
        model2.Read(FoundationConstants.kRegTLine).Should().Be(5120);
    }
}
