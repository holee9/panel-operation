using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Models;

public sealed class ProtMonModel : GoldenModelBase
{
    private const uint kDefaultTimeout = 500000U;
    private const uint kRadiogTimeout = 3000000U;

    private uint _fsmState;
    private uint _xrayActive;
    private uint _cfgMaxExposure;
    private uint _radiographyMode;
    private uint _exposureCount;
    private uint _errTimeout;
    private uint _errFlag;
    private uint _forceGateOff;

    public override void Reset()
    {
        _fsmState = 0U;
        _xrayActive = 0U;
        _cfgMaxExposure = 0U;
        _radiographyMode = 0U;
        _exposureCount = 0U;
        _errTimeout = 0U;
        _errFlag = 0U;
        _forceGateOff = 0U;
        CycleCount = 0;
    }

    public override void Step()
    {
        var effectiveLimit = _cfgMaxExposure != 0U
            ? _cfgMaxExposure
            : (_radiographyMode != 0U ? kRadiogTimeout : kDefaultTimeout);

        if ((_fsmState == 4U || _fsmState == 5U) && _xrayActive != 0U)
        {
            _exposureCount++;
            if (_exposureCount >= effectiveLimit)
            {
                _errTimeout = 1U;
                _errFlag = 1U;
                _forceGateOff = 1U;
            }
        }
        else if (_fsmState == 0U)
        {
            _exposureCount = 0U;
            _errTimeout = 0U;
            _errFlag = 0U;
            _forceGateOff = 0U;
        }

        CycleCount++;
    }

    public override void SetInputs(SignalMap inputs)
    {
        _fsmState = SignalHelpers.GetScalar(inputs, "fsm_state", _fsmState);
        _xrayActive = SignalHelpers.GetScalar(inputs, "xray_active", _xrayActive);
        _cfgMaxExposure = SignalHelpers.GetScalar(inputs, "cfg_max_exposure", _cfgMaxExposure);
        _radiographyMode = SignalHelpers.GetScalar(inputs, "radiography_mode", _radiographyMode);
    }

    public override SignalMap GetOutputs()
    {
        return new SignalMap
        {
            ["err_timeout"] = _errTimeout,
            ["err_flag"] = _errFlag,
            ["force_gate_off"] = _forceGateOff,
            ["exposure_count"] = _exposureCount,
        };
    }
}
