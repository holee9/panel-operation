using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Models;

public sealed class RegBankModel : GoldenModelBase
{
    private readonly ushort[] _regs = new ushort[32];
    private bool _stsBusy;
    private bool _stsDone;
    private bool _stsError;
    private bool _stsLineReady;
    private ushort _stsLineIndex;
    private byte _stsErrCode;

    private uint _inRegAddr;
    private uint _inRegWData;
    private uint _inRegWrEn;
    private uint _inRegRdEn;

    public RegBankModel()
    {
        Reset();
    }

    public bool TLineClamped { get; private set; }

    public override void Reset()
    {
        var defaults = FoundationConstants.MakeDefaultRegisters();
        Array.Copy(defaults, _regs, _regs.Length);
        _regs[FoundationConstants.kRegCtrl] = 0;
        TLineClamped = false;
        _stsBusy = false;
        _stsDone = false;
        _stsError = false;
        _stsLineReady = false;
        _stsLineIndex = 0;
        _stsErrCode = 0;
        _inRegAddr = 0;
        _inRegWData = 0;
        _inRegWrEn = 0;
        _inRegRdEn = 0;
        CycleCount = 0;
    }

    public override void Step()
    {
        if (_inRegWrEn != 0U)
        {
            Write((byte)_inRegAddr, (ushort)_inRegWData);
        }

        _regs[FoundationConstants.kRegCtrl] &= unchecked((ushort)~0x0003U);
        CycleCount++;
    }

    public override void SetInputs(SignalMap inputs)
    {
        _inRegAddr = SignalHelpers.GetScalar(inputs, "reg_addr", _inRegAddr) & 0x1FU;
        _inRegWData = SignalHelpers.GetScalar(inputs, "reg_wdata", _inRegWData) & 0xFFFFU;
        _inRegWrEn = SignalHelpers.GetScalar(inputs, "reg_wr_en", _inRegWrEn) & 0x1U;
        _inRegRdEn = SignalHelpers.GetScalar(inputs, "reg_rd_en", _inRegRdEn) & 0x1U;
        _stsBusy = (SignalHelpers.GetScalar(inputs, "sts_busy", _stsBusy ? 1U : 0U) & 0x1U) != 0U;
        _stsDone = (SignalHelpers.GetScalar(inputs, "sts_done", _stsDone ? 1U : 0U) & 0x1U) != 0U;
        _stsError = (SignalHelpers.GetScalar(inputs, "sts_error", _stsError ? 1U : 0U) & 0x1U) != 0U;
        _stsLineReady = (SignalHelpers.GetScalar(inputs, "sts_line_rdy", _stsLineReady ? 1U : 0U) & 0x1U) != 0U;
        _stsLineIndex = (ushort)(SignalHelpers.GetScalar(inputs, "sts_line_idx", _stsLineIndex) & 0x0FFFU);
        _stsErrCode = (byte)(SignalHelpers.GetScalar(inputs, "sts_err_code", _stsErrCode) & 0xFFU);
    }

    public override SignalMap GetOutputs()
    {
        var status = (ushort)(
            (_stsBusy ? 0x1U : 0U) |
            (_stsDone ? 0x2U : 0U) |
            (_stsError ? 0x4U : 0U) |
            (_stsLineReady ? 0x8U : 0U));

        return new SignalMap
        {
            ["reg_rdata"] = Read((byte)_inRegAddr),
            ["cfg_mode"] = (uint)(_regs[FoundationConstants.kRegMode] & 0x7U),
            ["cfg_combo"] = (uint)(_regs[FoundationConstants.kRegCombo] & 0x7U),
            ["cfg_nrows"] = (uint)(_regs[FoundationConstants.kRegNRows] & 0x0FFFU),
            ["cfg_ncols"] = (uint)(_regs[FoundationConstants.kRegNCols] & 0x0FFFU),
            ["cfg_tline"] = _regs[FoundationConstants.kRegTLine],
            ["cfg_treset"] = _regs[FoundationConstants.kRegTReset],
            ["cfg_tinteg"] = ((uint)(_regs[FoundationConstants.kRegTIntegHi] & 0x00FFU) << 16) |
                             _regs[FoundationConstants.kRegTInteg],
            ["cfg_tgate_on"] = (uint)(_regs[FoundationConstants.kRegTGateOn] & 0x0FFFU),
            ["cfg_tgate_settle"] = (uint)(_regs[FoundationConstants.kRegTGateSettle] & 0xFFU),
            ["cfg_afe_ifs"] = (uint)(_regs[FoundationConstants.kRegAfeIfs] & 0x3FU),
            ["cfg_afe_lpf"] = (uint)(_regs[FoundationConstants.kRegAfeLpf] & 0xFU),
            ["cfg_afe_pmode"] = (uint)(_regs[FoundationConstants.kRegAfePMode] & 0x3U),
            ["cfg_cic_en"] = (uint)(_regs[FoundationConstants.kRegCicEn] & 0x1U),
            ["cfg_cic_profile"] = (uint)(_regs[FoundationConstants.kRegCicProfile] & 0xFU),
            ["cfg_pipeline_en"] = (uint)((_regs[FoundationConstants.kRegCicProfile] >> 4) & 0x1U),
            ["cfg_tp_sel"] = (uint)((_regs[FoundationConstants.kRegCicProfile] >> 5) & 0x1U),
            ["cfg_scan_dir"] = (uint)(_regs[FoundationConstants.kRegScanDir] & 0x1U),
            ["cfg_gate_sel"] = (uint)(_regs[FoundationConstants.kRegGateSel] & 0x3U),
            ["cfg_afe_nchip"] = (uint)(_regs[FoundationConstants.kRegAfeNChip] & 0xFU),
            ["cfg_sync_dly"] = (uint)(_regs[FoundationConstants.kRegSyncDly] & 0xFFU),
            ["cfg_nreset"] = (uint)(_regs[FoundationConstants.kRegNReset] & 0xFFU),
            ["cfg_irq_en"] = (uint)((_regs[FoundationConstants.kRegCtrl] >> 3) & 0xFFU),
            ["ctrl_start"] = (uint)(_regs[FoundationConstants.kRegCtrl] & 0x1U),
            ["ctrl_abort"] = (uint)((_regs[FoundationConstants.kRegCtrl] >> 1) & 0x1U),
            ["ctrl_irq_global_en"] = (uint)((_regs[FoundationConstants.kRegCtrl] >> 2) & 0x1U),
            ["status_word"] = status,
            ["tline_clamped"] = TLineClamped ? 1U : 0U,
        };
    }

    public ushort Read(byte addr)
    {
        addr &= 0x1F;
        return addr switch
        {
            FoundationConstants.kRegStatus => (ushort)(
                (_stsBusy ? 0x1U : 0U) |
                (_stsDone ? 0x2U : 0U) |
                (_stsError ? 0x4U : 0U) |
                (_stsLineReady ? 0x8U : 0U)),
            FoundationConstants.kRegLineIdx => (ushort)(_stsLineIndex & 0x0FFFU),
            FoundationConstants.kRegErrCode => _stsErrCode,
            _ => _regs[addr],
        };
    }

    public void Write(byte addr, ushort value)
    {
        addr &= 0x1F;
        if (FoundationConstants.IsReadOnlyRegister(addr))
        {
            return;
        }

        var combo = (byte)(((addr == FoundationConstants.kRegCombo ? value : _regs[FoundationConstants.kRegCombo])) & 0x7U);

        switch (addr)
        {
            case FoundationConstants.kRegCombo:
                _regs[FoundationConstants.kRegCombo] = (ushort)(combo & 0x7U);
                _regs[FoundationConstants.kRegNCols] = FoundationConstants.ComboDefaultNCols(combo);
                if (_regs[FoundationConstants.kRegTLine] < FoundationConstants.ComboMinTLine(combo))
                {
                    _regs[FoundationConstants.kRegTLine] = FoundationConstants.ComboMinTLine(combo);
                    TLineClamped = true;
                }
                break;
            case FoundationConstants.kRegNCols:
                _regs[FoundationConstants.kRegNCols] =
                    (ushort)Math.Min(value & 0x0FFFU, FoundationConstants.ComboDefaultNCols(combo));
                break;
            case FoundationConstants.kRegTLine:
                if (value < FoundationConstants.ComboMinTLine(combo))
                {
                    _regs[FoundationConstants.kRegTLine] = FoundationConstants.ComboMinTLine(combo);
                    TLineClamped = true;
                }
                else
                {
                    _regs[FoundationConstants.kRegTLine] = value;
                }
                break;
            case FoundationConstants.kRegTIntegHi:
                _regs[FoundationConstants.kRegTIntegHi] = (ushort)(value & 0x00FFU);
                break;
            default:
                _regs[addr] = value;
                break;
        }
    }

    public void ClearTLineClamped()
    {
        TLineClamped = false;
    }

    public void SetStatus(bool busy, bool done, bool error, bool lineReady, ushort lineIndex, byte errCode)
    {
        _stsBusy = busy;
        _stsDone = done;
        _stsError = error;
        _stsLineReady = lineReady;
        _stsLineIndex = (ushort)(lineIndex & 0x0FFFU);
        _stsErrCode = errCode;
    }
}
