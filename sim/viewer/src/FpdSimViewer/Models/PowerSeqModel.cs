using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Models;

public sealed class PowerSeqModel : GoldenModelBase
{
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
        };
    }
}
