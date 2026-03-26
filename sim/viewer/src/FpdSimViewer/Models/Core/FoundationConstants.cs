namespace FpdSimViewer.Models.Core;

public static class FoundationConstants
{
    public const byte kRegCtrl = 0x00;
    public const byte kRegStatus = 0x01;
    public const byte kRegMode = 0x02;
    public const byte kRegCombo = 0x03;
    public const byte kRegNRows = 0x04;
    public const byte kRegNCols = 0x05;
    public const byte kRegTLine = 0x06;
    public const byte kRegTReset = 0x07;
    public const byte kRegTInteg = 0x08;
    public const byte kRegTGateOn = 0x09;
    public const byte kRegTGateSettle = 0x0A;
    public const byte kRegAfeIfs = 0x0B;
    public const byte kRegAfeLpf = 0x0C;
    public const byte kRegAfePMode = 0x0D;
    public const byte kRegCicEn = 0x0E;
    public const byte kRegCicProfile = 0x0F;
    public const byte kRegScanDir = 0x10;
    public const byte kRegGateSel = 0x11;
    public const byte kRegAfeNChip = 0x12;
    public const byte kRegSyncDly = 0x13;
    public const byte kRegLineIdx = 0x14;
    public const byte kRegErrCode = 0x15;
    public const byte kRegNReset = 0x16;
    public const byte kRegTIntegHi = 0x17;
    public const byte kRegVersion = 0x1F;

    public const ushort kVersion10 = 0x0010;
    public const byte kComboC1 = 0x01;
    public const byte kComboC2 = 0x02;
    public const byte kComboC3 = 0x03;
    public const byte kComboC4 = 0x04;
    public const byte kComboC5 = 0x05;
    public const byte kComboC6 = 0x06;
    public const byte kComboC7 = 0x07;

    public static ushort ComboDefaultNCols(byte combo)
    {
        return (combo & 0x7U) switch
        {
            kComboC4 or kComboC5 => (ushort)1664,
            kComboC6 or kComboC7 => (ushort)3072,
            _ => (ushort)2048,
        };
    }

    public static ushort ComboMinTLine(byte combo)
    {
        return (combo & 0x7U) switch
        {
            kComboC2 => (ushort)6000,
            kComboC3 => (ushort)5120,
            _ => (ushort)2200,
        };
    }

    public static ushort[] MakeDefaultRegisters(byte combo = kComboC1)
    {
        var regs = new ushort[32];
        regs[kRegMode] = 0x0000;
        regs[kRegCombo] = (ushort)(combo & 0x7U);
        regs[kRegNRows] = 2048;
        regs[kRegNCols] = ComboDefaultNCols(combo);
        regs[kRegTLine] = ComboMinTLine(combo);
        regs[kRegTReset] = 100;
        regs[kRegTInteg] = 1000;
        regs[kRegTGateOn] = 2200;
        regs[kRegTGateSettle] = 100;
        regs[kRegAfeIfs] = 0x0000;
        regs[kRegAfeLpf] = 0x0000;
        regs[kRegAfePMode] = 0x0000;
        regs[kRegCicEn] = 0x0000;
        regs[kRegCicProfile] = 0x0000;
        regs[kRegScanDir] = 0x0000;
        regs[kRegGateSel] = 0x0000;
        regs[kRegAfeNChip] = 0x0001;
        regs[kRegSyncDly] = 0x0000;
        regs[kRegNReset] = 0x0003;
        regs[kRegTIntegHi] = 0x0000;
        regs[kRegVersion] = kVersion10;
        return regs;
    }

    public static bool IsReadOnlyRegister(byte addr)
    {
        return addr is kRegStatus or kRegLineIdx or kRegErrCode or kRegVersion;
    }
}
