using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Models;

public sealed class PowerSeqModel : GoldenModelBase
{
    private const double kSlewRateVPerMs = 5.0;
    private const double kStepDtMs = 0.00001;

    private uint _targetMode;
    private uint _vglStable;
    private uint _vghStable;
    private uint _currentMode;
    private uint _enVgl;
    private uint _enVgh;
    private uint _enAvdd1;
    private uint _enAvdd2;
    private uint _enDvdd;
    private uint _powerGood;
    private uint _seqError;
    private double _vglCurrent;
    private double _vghCurrent;

    public override void Reset()
    {
        _targetMode = 0U;
        _vglStable = 0U;
        _vghStable = 0U;
        _currentMode = 0U;
        _enVgl = 0U;
        _enVgh = 0U;
        _enAvdd1 = 0U;
        _enAvdd2 = 0U;
        _enDvdd = 0U;
        _powerGood = 0U;
        _seqError = 0U;
        _vglCurrent = 0.0;
        _vghCurrent = 0.0;
        CycleCount = 0;
    }

    public override void Step()
    {
        _enDvdd = 1U;
        _enAvdd1 = _targetMode <= 5U ? 1U : 0U;
        _enAvdd2 = _targetMode <= 2U ? 1U : 0U;
        _enVgl = _targetMode <= 3U ? 1U : 0U;
        _enVgh = _enVgl != 0U && _vglStable != 0U ? 1U : 0U;
        _currentMode = _targetMode;
        _powerGood = _enVgh != 0U && _vghStable != 0U ? 1U : 0U;
        _seqError = _enVgh != 0U && _enVgl == 0U ? 1U : 0U;
        _vglCurrent = MoveToward(_vglCurrent, _enVgl != 0U ? -10.0 : 0.0, kSlewRateVPerMs * kStepDtMs);
        _vghCurrent = MoveToward(_vghCurrent, _enVgh != 0U ? 20.0 : 0.0, kSlewRateVPerMs * kStepDtMs);
        CycleCount++;
    }

    public override void SetInputs(SignalMap inputs)
    {
        _targetMode = SignalHelpers.GetScalar(inputs, "target_mode", _targetMode);
        _vglStable = SignalHelpers.GetScalar(inputs, "vgl_stable", _vglStable);
        _vghStable = SignalHelpers.GetScalar(inputs, "vgh_stable", _vghStable);
    }

    public override SignalMap GetOutputs()
    {
        return new SignalMap
        {
            ["current_mode"] = _currentMode,
            ["en_vgl"] = _enVgl,
            ["en_vgh"] = _enVgh,
            ["en_avdd1"] = _enAvdd1,
            ["en_avdd2"] = _enAvdd2,
            ["en_dvdd"] = _enDvdd,
            ["power_good"] = _powerGood,
            ["seq_error"] = _seqError,
            ["vgl_rail_voltage"] = (uint)Math.Round((_vglCurrent + 15.0) * 100.0),
            ["vgh_rail_voltage"] = (uint)Math.Round(_vghCurrent * 100.0),
        };
    }

    private static double MoveToward(double current, double target, double maxDelta)
    {
        if (Math.Abs(target - current) <= maxDelta)
        {
            return target;
        }

        return current + (Math.Sign(target - current) * maxDelta);
    }
}
