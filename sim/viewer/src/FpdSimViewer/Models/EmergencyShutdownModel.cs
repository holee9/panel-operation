using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Models;

public sealed class EmergencyShutdownModel : GoldenModelBase
{
    private uint _vghOver;
    private uint _vghUnder;
    private uint _tempOver;
    private uint _pllUnlocked;
    private uint _hwEmergencyN = 1U;
    private uint _shutdownReq;
    private uint _forceGateOff;
    private uint _shutdownCode;

    public override void Reset()
    {
        _vghOver = 0U;
        _vghUnder = 0U;
        _tempOver = 0U;
        _pllUnlocked = 0U;
        _hwEmergencyN = 1U;
        _shutdownReq = 0U;
        _forceGateOff = 0U;
        _shutdownCode = 0U;
        CycleCount = 0;
    }

    public override void Step()
    {
        _shutdownReq = 0U;
        _forceGateOff = 0U;
        _shutdownCode = 0U;

        if (_hwEmergencyN == 0U)
        {
            _shutdownReq = 1U;
            _forceGateOff = 1U;
            _shutdownCode = 0xEEU;
        }
        else if (_vghOver != 0U)
        {
            _shutdownReq = 1U;
            _forceGateOff = 1U;
            _shutdownCode = 1U;
        }
        else if (_tempOver != 0U)
        {
            _shutdownReq = 1U;
            _forceGateOff = 1U;
            _shutdownCode = 2U;
        }
        else if (_pllUnlocked != 0U)
        {
            _shutdownReq = 1U;
            _forceGateOff = 1U;
            _shutdownCode = 3U;
        }
        else if (_vghUnder != 0U)
        {
            _shutdownReq = 1U;
            _forceGateOff = 1U;
            _shutdownCode = 4U;
        }

        CycleCount++;
    }

    public override void SetInputs(SignalMap inputs)
    {
        _vghOver = SignalHelpers.GetScalar(inputs, "vgh_over", _vghOver);
        _vghUnder = SignalHelpers.GetScalar(inputs, "vgh_under", _vghUnder);
        _tempOver = SignalHelpers.GetScalar(inputs, "temp_over", _tempOver);
        _pllUnlocked = SignalHelpers.GetScalar(inputs, "pll_unlocked", _pllUnlocked);
        _hwEmergencyN = SignalHelpers.GetScalar(inputs, "hw_emergency_n", _hwEmergencyN);
    }

    public override SignalMap GetOutputs()
    {
        return new SignalMap
        {
            ["shutdown_req"] = _shutdownReq,
            ["force_gate_off"] = _forceGateOff,
            ["shutdown_code"] = _shutdownCode,
        };
    }
}
