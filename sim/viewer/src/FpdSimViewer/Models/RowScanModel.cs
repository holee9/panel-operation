using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Models;

public sealed class RowScanModel : GoldenModelBase
{
    private uint _scanStart;
    private uint _scanAbort;
    private uint _scanDir;
    private uint _cfgNRows = 2048U;
    private uint _rowIndex;
    private uint _gateOnPulse;
    private uint _gateSettle;
    private uint _scanActive;
    private uint _rowDone;
    private uint _scanDone;

    public override void Reset()
    {
        _scanStart = 0U;
        _scanAbort = 0U;
        _scanDir = 0U;
        _cfgNRows = 2048U;
        _rowIndex = 0U;
        _gateOnPulse = 0U;
        _gateSettle = 0U;
        _scanActive = 0U;
        _rowDone = 0U;
        _scanDone = 0U;
        CycleCount = 0;
    }

    public override void Step()
    {
        _rowDone = 0U;
        _scanDone = 0U;

        if (_scanAbort != 0U)
        {
            _scanActive = 0U;
            _gateOnPulse = 0U;
            _gateSettle = 0U;
        }
        else if (_scanActive == 0U && _scanStart != 0U)
        {
            _scanActive = 1U;
            _rowIndex = _scanDir != 0U ? _cfgNRows - 1U : 0U;
            _gateOnPulse = 1U;
        }
        else if (_scanActive != 0U && _gateOnPulse != 0U)
        {
            _gateOnPulse = 0U;
            _gateSettle = 1U;
        }
        else if (_scanActive != 0U && _gateSettle != 0U)
        {
            _gateSettle = 0U;
            _rowDone = 1U;

            if ((_scanDir != 0U && _rowIndex == 0U) ||
                (_scanDir == 0U && _rowIndex + 1U >= _cfgNRows))
            {
                _scanActive = 0U;
                _scanDone = 1U;
            }
            else
            {
                _rowIndex = _scanDir != 0U ? _rowIndex - 1U : _rowIndex + 1U;
                _gateOnPulse = 1U;
            }
        }

        CycleCount++;
    }

    public override void SetInputs(SignalMap inputs)
    {
        _scanStart = SignalHelpers.GetScalar(inputs, "scan_start", _scanStart);
        _scanAbort = SignalHelpers.GetScalar(inputs, "scan_abort", _scanAbort);
        _scanDir = SignalHelpers.GetScalar(inputs, "scan_dir", _scanDir);
        _cfgNRows = SignalHelpers.GetScalar(inputs, "cfg_nrows", _cfgNRows);
    }

    public override SignalMap GetOutputs()
    {
        return new SignalMap
        {
            ["row_index"] = _rowIndex,
            ["gate_on_pulse"] = _gateOnPulse,
            ["gate_settle"] = _gateSettle,
            ["scan_active"] = _scanActive,
            ["row_done"] = _rowDone,
            ["scan_done"] = _scanDone,
        };
    }
}
