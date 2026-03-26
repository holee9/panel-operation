using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Models;

public sealed class ClkRstModel : GoldenModelBase
{
    private const ulong kSysClkHz = 100000000UL;
    private const ulong kMclkHz = 32000000UL;

    private readonly ulong _afeClkHz;

    private ulong _phaseAccAclk;
    private ulong _phaseAccMclk;
    private bool _rstExtN;
    private byte _afeTypeSel;
    private bool _clkAfe;
    private bool _clkAclk;
    private bool _clkMclk;
    private bool _pllLocked;
    private bool _rstFf1;
    private bool _rstFf2;
    private uint _lockCounter;

    public ClkRstModel(ulong afeClkHz = 10000000UL)
    {
        _afeClkHz = afeClkHz;
        Reset();
    }

    public override void Reset()
    {
        _phaseAccAclk = 0UL;
        _phaseAccMclk = 0UL;
        _rstExtN = false;
        _afeTypeSel = 0;
        _clkAfe = false;
        _clkAclk = false;
        _clkMclk = false;
        _pllLocked = false;
        _rstFf1 = false;
        _rstFf2 = false;
        _lockCounter = 0U;
        CycleCount = 0;
    }

    public override void Step()
    {
        if (!_rstExtN)
        {
            _phaseAccAclk = 0UL;
            _phaseAccMclk = 0UL;
            _clkAfe = false;
            _clkAclk = false;
            _clkMclk = false;
            _pllLocked = false;
            _rstFf1 = false;
            _rstFf2 = false;
            _lockCounter = 0U;
        }
        else
        {
            var phaseStepAclk = (_afeClkHz << 32) / kSysClkHz;
            var phaseStepMclk = (kMclkHz << 32) / kSysClkHz;
            _phaseAccAclk = (_phaseAccAclk + phaseStepAclk) & 0xFFFFFFFFUL;
            _phaseAccMclk = (_phaseAccMclk + phaseStepMclk) & 0xFFFFFFFFUL;
            _clkAclk = ((_phaseAccAclk >> 31) & 0x1UL) != 0UL;
            _clkMclk = ((_phaseAccMclk >> 31) & 0x1UL) != 0UL;
            _clkAfe = _afeTypeSel == 2U ? _clkMclk : _clkAclk;

            if (!_pllLocked)
            {
                _lockCounter++;
                if (_lockCounter >= 16U)
                {
                    _pllLocked = true;
                }
            }

            // Intentionally matches the C++ golden model's same-cycle propagation.
            _rstFf1 = _pllLocked;
            _rstFf2 = _rstFf1;
        }

        CycleCount++;
    }

    public override void SetInputs(SignalMap inputs)
    {
        _rstExtN = (SignalHelpers.GetScalar(inputs, "rst_ext_n", _rstExtN ? 1U : 0U) & 0x1U) != 0U;
        _afeTypeSel = (byte)(SignalHelpers.GetScalar(inputs, "afe_type_sel", _afeTypeSel) & 0x3U);
    }

    public override SignalMap GetOutputs()
    {
        var rstSync = _rstFf2 ? 1U : 0U;
        return new SignalMap
        {
            ["clk_afe"] = _clkAfe ? 1U : 0U,
            ["clk_aclk"] = _clkAclk ? 1U : 0U,
            ["clk_mclk"] = _clkMclk ? 1U : 0U,
            ["clk_sys_out"] = 1U,
            ["pll_locked"] = _pllLocked ? 1U : 0U,
            ["rst_sync"] = rstSync,
            ["rst_sync_n"] = rstSync,
        };
    }
}
