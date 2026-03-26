using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Models;

public sealed class PanelFsmModel : GoldenModelBase
{
    private uint _state;
    private uint _mode;
    private uint _combo = 1;
    private uint _nrows;
    private uint _treset;
    private uint _tinteg;
    private uint _nreset;
    private uint _syncDly;
    private uint _tgateSettle;
    private uint _lineIdx;
    private uint _timer;
    private uint _waitTimer;
    private uint _ctrlStart;
    private uint _ctrlAbort;
    private uint _gateRowDone;
    private uint _afeConfigDone;
    private uint _afeLineValid;
    private uint _xrayPrepReq;
    private uint _xrayOn;
    private uint _xrayOff;
    private uint _protError;
    private uint _protForceStop;
    private uint _radiographyMode;
    private uint _busy;
    private uint _done;
    private uint _error;
    private uint _errCode;

    public override void Reset()
    {
        _state = 0;
        _mode = 0;
        _combo = 1;
        _nrows = 0;
        _treset = 0;
        _tinteg = 0;
        _nreset = 0;
        _syncDly = 0;
        _tgateSettle = 0;
        _lineIdx = 0;
        _timer = 0;
        _waitTimer = 0;
        _ctrlStart = 0;
        _ctrlAbort = 0;
        _gateRowDone = 0;
        _afeConfigDone = 0;
        _afeLineValid = 0;
        _xrayPrepReq = 0;
        _xrayOn = 0;
        _xrayOff = 0;
        _protError = 0;
        _protForceStop = 0;
        _radiographyMode = 0;
        _busy = 0;
        _done = 0;
        _error = 0;
        _errCode = 0;
        CycleCount = 0;
    }

    public override void Step()
    {
        var effNRows = EffectiveOrDefault(_nrows, ComboDefaultRows(_combo));
        var effTReset = EffectiveOrDefault(_treset, ComboDefaultReset(_combo));
        var effTInteg = EffectiveOrDefault(_tinteg, ComboDefaultIntegrate(_combo));
        var effNReset = EffectiveOrDefault(_nreset, 1U);
        var effSyncDly = EffectiveOrDefault(_syncDly, 1U);
        var effTGateSettle = EffectiveOrDefault(_tgateSettle, 1U);
        var effReadyTimeout = _radiographyMode != 0U ? 30U : 5U;

        _done = 0;

        if (_protError != 0U || _protForceStop != 0U)
        {
            _state = 15;
            _busy = 0;
            _error = 1;
            _errCode = 3;
        }
        else if (_ctrlAbort != 0U)
        {
            _state = 0;
            _busy = 0;
            _error = 1;
            _errCode = 1;
        }
        else
        {
            switch (_state)
            {
                case 0:
                    _busy = 0;
                    _error = 0;
                    _errCode = 0;
                    _lineIdx = 0;
                    _timer = 0;
                    _waitTimer = 0;
                    if (_ctrlStart != 0U)
                    {
                        _state = 1;
                        _busy = 1;
                    }
                    break;
                case 1:
                    _timer = 0;
                    _waitTimer = 0;
                    _state = 2;
                    break;
                case 2:
                    if (++_timer >= (effTReset + effNReset))
                    {
                        _timer = 0;
                        _state = _mode == 4U ? 10U : ((_mode == 2U || _radiographyMode != 0U) ? 3U : 4U);
                    }
                    break;
                case 3:
                    if (++_waitTimer >= effReadyTimeout)
                    {
                        _state = 15;
                        _error = 1;
                        _errCode = 2;
                    }
                    else if (_xrayPrepReq != 0U)
                    {
                        _waitTimer = 0;
                        _timer = 0;
                        _state = 5;
                    }
                    break;
                case 4:
                    if (++_timer >= effTInteg)
                    {
                        _timer = 0;
                        _state = 6;
                    }
                    break;
                case 5:
                    if (++_waitTimer >= effReadyTimeout)
                    {
                        _state = 15;
                        _error = 1;
                        _errCode = 2;
                    }
                    else if (_xrayOn != 0U || _xrayPrepReq != 0U)
                    {
                        if (++_timer >= effTInteg || _xrayOff != 0U)
                        {
                            _timer = 0;
                            _state = 6;
                        }
                    }
                    break;
                case 6:
                    if (_afeConfigDone != 0U || ++_timer >= effSyncDly)
                    {
                        _timer = 0;
                        _lineIdx = 0;
                        _state = 7;
                    }
                    break;
                case 7:
                    var rowReady = ((_mode != 3U) && _gateRowDone != 0U && _afeLineValid != 0U) ||
                                   ((_mode == 3U) && _afeLineValid != 0U);
                    if (rowReady)
                    {
                        if (_lineIdx + 1U >= effNRows)
                        {
                            _timer = 0;
                            _state = 8;
                        }
                        else
                        {
                            _lineIdx++;
                        }
                    }
                    break;
                case 8:
                    if (++_timer >= effTGateSettle)
                    {
                        _timer = 0;
                        _state = 9;
                    }
                    break;
                case 9:
                    _state = 10;
                    break;
                case 10:
                    _busy = 0;
                    _done = 1;
                    _state = _mode == 1U ? 1U : 0U;
                    break;
                default:
                    if (_ctrlStart == 0U)
                    {
                        _state = 0;
                    }
                    break;
            }
        }

        CycleCount++;
    }

    public override void SetInputs(SignalMap inputs)
    {
        _ctrlStart = SignalHelpers.GetScalar(inputs, "ctrl_start", _ctrlStart);
        _ctrlAbort = SignalHelpers.GetScalar(inputs, "ctrl_abort", _ctrlAbort);
        _mode = SignalHelpers.GetScalar(inputs, "cfg_mode", _mode);
        _combo = SignalHelpers.GetScalar(inputs, "cfg_combo", _combo);
        _nrows = SignalHelpers.GetScalar(inputs, "cfg_nrows", _nrows);
        _treset = SignalHelpers.GetScalar(inputs, "cfg_treset", _treset);
        _tinteg = SignalHelpers.GetScalar(inputs, "cfg_tinteg", _tinteg);
        _nreset = SignalHelpers.GetScalar(inputs, "cfg_nreset", _nreset);
        _syncDly = SignalHelpers.GetScalar(inputs, "cfg_sync_dly", _syncDly);
        _tgateSettle = SignalHelpers.GetScalar(inputs, "cfg_tgate_settle", _tgateSettle);
        _gateRowDone = SignalHelpers.GetScalar(inputs, "gate_row_done", _gateRowDone);
        _afeConfigDone = SignalHelpers.GetScalar(inputs, "afe_config_done", _afeConfigDone);
        _afeLineValid = SignalHelpers.GetScalar(inputs, "afe_line_valid", _afeLineValid);
        _xrayPrepReq = SignalHelpers.GetScalar(inputs, "xray_prep_req", _xrayPrepReq);
        _xrayOn = SignalHelpers.GetScalar(inputs, "xray_on", _xrayOn);
        _xrayOff = SignalHelpers.GetScalar(inputs, "xray_off", _xrayOff);
        _protError = SignalHelpers.GetScalar(inputs, "prot_error", _protError);
        _protForceStop = SignalHelpers.GetScalar(inputs, "prot_force_stop", _protForceStop);
        _radiographyMode = SignalHelpers.GetScalar(inputs, "radiography_mode", _radiographyMode);
    }

    public override SignalMap GetOutputs()
    {
        return new SignalMap
        {
            ["fsm_state"] = _state,
            ["sts_busy"] = _busy,
            ["sts_done"] = _done,
            ["sts_error"] = _error,
            ["sts_line_idx"] = _lineIdx,
            ["sts_err_code"] = _errCode,
        };
    }

    private static uint ComboDefaultRows(uint combo)
    {
        return combo is 6U or 7U ? 3072U : 2048U;
    }

    private static uint ComboDefaultReset(uint combo)
    {
        return combo >= 6U ? 4U : 2U;
    }

    private static uint ComboDefaultIntegrate(uint combo)
    {
        return combo switch
        {
            2U => 6000U,
            3U => 5120U,
            _ => 2200U,
        };
    }

    private static uint EffectiveOrDefault(uint value, uint fallback)
    {
        return value == 0U ? fallback : value;
    }
}
