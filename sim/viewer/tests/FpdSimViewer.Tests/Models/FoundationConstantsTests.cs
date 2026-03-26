using FluentAssertions;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Tests.Models;

public sealed class FoundationConstantsTests
{
    [Theory]
    [InlineData(FoundationConstants.kComboC1, 2048, 2200)]
    [InlineData(FoundationConstants.kComboC2, 2048, 6000)]
    [InlineData(FoundationConstants.kComboC3, 2048, 5120)]
    [InlineData(FoundationConstants.kComboC4, 1664, 2200)]
    [InlineData(FoundationConstants.kComboC6, 3072, 2200)]
    [InlineData(FoundationConstants.kComboC7, 3072, 2200)]
    public void ComboDefaults_ShouldMatchCppValues(byte combo, ushort ncols, ushort minTLine)
    {
        FoundationConstants.ComboDefaultNCols(combo).Should().Be(ncols);
        FoundationConstants.ComboMinTLine(combo).Should().Be(minTLine);
    }

    [Fact]
    public void MakeDefaultRegisters_ShouldApplyComboSpecificDefaults()
    {
        var regs = FoundationConstants.MakeDefaultRegisters(FoundationConstants.kComboC4);

        regs[FoundationConstants.kRegCombo].Should().Be(FoundationConstants.kComboC4);
        regs[FoundationConstants.kRegNCols].Should().Be(1664);
        regs[FoundationConstants.kRegTLine].Should().Be(2200);
        regs[FoundationConstants.kRegVersion].Should().Be(FoundationConstants.kVersion10);
    }
}
