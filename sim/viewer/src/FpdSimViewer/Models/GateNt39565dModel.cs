using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Models;

public sealed class GateNt39565dModel : GoldenModelBase
{
    private uint _rowIndex;
    private uint _gateOnPulse;
    private uint _scanDir;
    private uint _chipSel;
    private uint _modeSel;
    private uint _cascadeStvReturn;
    private uint _stv1L;
    private uint _stv2L;
    private uint _stv1R;
    private uint _stv2R;
    private uint _oe1L;
    private uint _oe1R;
    private uint _oe2L;
    private uint _oe2R;
    private uint _cascadeComplete;

    public override void Reset()
    {
        _rowIndex = 0U;
        _gateOnPulse = 0U;
        _scanDir = 0U;
        _chipSel = 0U;
        _modeSel = 0U;
        _cascadeStvReturn = 0U;
        _stv1L = 0U;
        _stv2L = 0U;
        _stv1R = 0U;
        _stv2R = 0U;
        _oe1L = 0U;
        _oe1R = 0U;
        _oe2L = 0U;
        _oe2R = 0U;
        _cascadeComplete = 0U;
        CycleCount = 0;
    }

    public override void Step()
    {
        var phase = _scanDir != 0U ? ((_rowIndex + 1U) & 0x1U) : (_rowIndex & 0x1U);
        var chipPhase = _rowIndex / 541U;
        var leftActive = _chipSel != 1U;
        var rightActive = _chipSel != 0U;

        _stv1L = _gateOnPulse != 0U && leftActive && phase == 0U ? 1U : 0U;
        _stv2L = _gateOnPulse != 0U && leftActive && phase == 1U ? 1U : 0U;
        _stv1R = _gateOnPulse != 0U && rightActive && phase == 0U ? 1U : 0U;
        _stv2R = _gateOnPulse != 0U && rightActive && phase == 1U ? 1U : 0U;
        _oe1L = _gateOnPulse != 0U && leftActive && (_rowIndex & 0x1U) == 0U ? 1U : 0U;
        _oe1R = _gateOnPulse != 0U && rightActive && (_rowIndex & 0x1U) == 0U ? 1U : 0U;
        _oe2L = _gateOnPulse != 0U && leftActive && (_rowIndex & 0x1U) != 0U ? 1U : 0U;
        _oe2R = _gateOnPulse != 0U && rightActive && (_rowIndex & 0x1U) != 0U ? 1U : 0U;
        _cascadeComplete = _gateOnPulse != 0U && _cascadeStvReturn != 0U &&
                           (_modeSel != 0U || _chipSel == 2U || chipPhase >= 5U)
            ? 1U
            : 0U;
        CycleCount++;
    }

    public override void SetInputs(SignalMap inputs)
    {
        _rowIndex = SignalHelpers.GetScalar(inputs, "row_index", _rowIndex);
        _gateOnPulse = SignalHelpers.GetScalar(inputs, "gate_on_pulse", _gateOnPulse);
        _scanDir = SignalHelpers.GetScalar(inputs, "scan_dir", _scanDir);
        _chipSel = SignalHelpers.GetScalar(inputs, "chip_sel", _chipSel);
        _modeSel = SignalHelpers.GetScalar(inputs, "mode_sel", _modeSel);
        _cascadeStvReturn = SignalHelpers.GetScalar(inputs, "cascade_stv_return", _cascadeStvReturn);
    }

    public override SignalMap GetOutputs()
    {
        return new SignalMap
        {
            ["stv1l"] = _stv1L,
            ["stv2l"] = _stv2L,
            ["stv1r"] = _stv1R,
            ["stv2r"] = _stv2R,
            ["oe1l"] = _oe1L,
            ["oe1r"] = _oe1R,
            ["oe2l"] = _oe2L,
            ["oe2r"] = _oe2R,
            ["cascade_complete"] = _cascadeComplete,
            ["row_done"] = _cascadeComplete,
        };
    }
}
