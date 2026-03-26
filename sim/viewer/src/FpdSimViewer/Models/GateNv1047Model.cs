using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Models;

public sealed class GateNv1047Model : GoldenModelBase
{
    private uint _rowIndex;
    private uint _gateOnPulse;
    private uint _scanDir;
    private uint _resetAll;
    private uint _cfgClkPeriod = 2200U;
    private uint _cfgGateOn = 2200U;
    private uint _cfgGateSettle = 100U;
    private uint _nvSd1;
    private uint _nvSd2;
    private uint _nvClk;
    private uint _nvOe = 1U;
    private uint _nvOna = 1U;
    private uint _nvLr;
    private uint _nvRst = 1U;
    private uint _rowDone;
    private uint _gateOnPrev;
    private uint _bbmCount;
    private uint _bbmPending;
    private uint _clkDiv;

    public override void Reset()
    {
        _rowIndex = 0U;
        _gateOnPulse = 0U;
        _scanDir = 0U;
        _resetAll = 0U;
        _cfgClkPeriod = 2200U;
        _cfgGateOn = 2200U;
        _cfgGateSettle = 100U;
        _nvSd1 = 0U;
        _nvSd2 = 0U;
        _nvClk = 0U;
        _nvOe = 1U;
        _nvOna = 1U;
        _nvLr = 0U;
        _nvRst = 1U;
        _rowDone = 0U;
        _gateOnPrev = 0U;
        _bbmCount = 0U;
        _bbmPending = 0U;
        _clkDiv = 0U;
        CycleCount = 0;
    }

    public override void Step()
    {
        _rowDone = 0U;
        _nvLr = _scanDir;

        if (_gateOnPrev != 0U && _gateOnPulse == 0U)
        {
            _bbmCount = _cfgGateSettle == 0U ? 1U : _cfgGateSettle;
            _bbmPending = 1U;
        }
        else if (_bbmCount != 0U)
        {
            if (_bbmPending != 0U && _bbmCount == 1U)
            {
                _rowDone = 1U;
                _bbmPending = 0U;
            }

            _bbmCount--;
        }

        if (_resetAll != 0U)
        {
            _nvOna = 0U;
            _nvRst = 0U;
            _nvOe = 1U;
            _bbmPending = 0U;
        }
        else
        {
            _nvRst = 1U;
            _nvOna = 1U;
            if (_gateOnPulse != 0U)
            {
                _clkDiv++;
                if (_clkDiv >= (_cfgClkPeriod / 2U))
                {
                    _clkDiv = 0U;
                    _nvClk ^= 1U;
                }

                _nvSd1 = (_rowIndex >> 0) & 0x1U;
                _nvSd2 = (_rowIndex >> 1) & 0x1U;
                _nvOe = _bbmCount == 0U ? 0U : 1U;
            }
            else
            {
                _nvClk = 0U;
                _nvOe = 1U;
                _clkDiv = 0U;
            }
        }

        _gateOnPrev = _gateOnPulse;
        CycleCount++;
    }

    public override void SetInputs(SignalMap inputs)
    {
        _rowIndex = SignalHelpers.GetScalar(inputs, "row_index", _rowIndex);
        _gateOnPulse = SignalHelpers.GetScalar(inputs, "gate_on_pulse", _gateOnPulse);
        _scanDir = SignalHelpers.GetScalar(inputs, "scan_dir", _scanDir);
        _resetAll = SignalHelpers.GetScalar(inputs, "reset_all", _resetAll);
        _cfgClkPeriod = SignalHelpers.GetScalar(inputs, "cfg_clk_period", _cfgClkPeriod);
        _cfgGateOn = SignalHelpers.GetScalar(inputs, "cfg_gate_on", _cfgGateOn);
        _cfgGateSettle = SignalHelpers.GetScalar(inputs, "cfg_gate_settle", _cfgGateSettle);
    }

    public override SignalMap GetOutputs()
    {
        return new SignalMap
        {
            ["nv_sd1"] = _nvSd1,
            ["nv_sd2"] = _nvSd2,
            ["nv_clk"] = _nvClk,
            ["nv_oe"] = _nvOe,
            ["nv_ona"] = _nvOna,
            ["nv_lr"] = _nvLr,
            ["nv_rst"] = _nvRst,
            ["row_done"] = _rowDone,
        };
    }
}
