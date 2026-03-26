using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Models;

public sealed class AfeAd711xxModel : GoldenModelBase
{
    private uint _afeStart;
    private uint _configReq;
    private uint _cfgCombo = 1U;
    private uint _cfgTLine;
    private uint _cfgIfs;
    private uint _cfgLpf;
    private uint _cfgPMode;
    private uint _cfgNChip = 1U;
    private uint _afeType;
    private uint _afeReady;
    private uint _configDone;
    private uint _doutWindowValid;
    private uint _lineCount;
    private uint _tlineError;
    private uint _ifsWidthError;
    private uint _expectedNCols = 2048U;
    private ushort[] _sampleLine = [];

    public override void Reset()
    {
        _afeStart = 0U;
        _configReq = 0U;
        _cfgCombo = 1U;
        _cfgTLine = 0U;
        _cfgIfs = 0U;
        _cfgLpf = 0U;
        _cfgPMode = 0U;
        _cfgNChip = 1U;
        _afeType = 0U;
        _afeReady = 0U;
        _configDone = 0U;
        _doutWindowValid = 0U;
        _lineCount = 0U;
        _tlineError = 0U;
        _ifsWidthError = 0U;
        _expectedNCols = 2048U;
        _sampleLine = [];
        CycleCount = 0;
    }

    public override void Step()
    {
        _configDone = 0U;
        _expectedNCols = ComboDefaultNCols(_cfgCombo);
        var effectiveTLine = _cfgTLine == 0U ? MinTLine(_afeType) : _cfgTLine;
        var effectiveNChip = _cfgNChip == 0U ? 1U : _cfgNChip;
        var effectiveIfs = EffectiveIfs(_afeType, _cfgIfs);

        _tlineError = effectiveTLine < MinTLine(_afeType) ? 1U : 0U;
        _ifsWidthError = _afeType == 1U && _cfgIfs > 31U ? 1U : 0U;

        if (_configReq != 0U && _afeReady == 0U)
        {
            _afeReady = 1U;
            _configDone = 1U;
        }

        if (_afeStart != 0U && _afeReady != 0U && _tlineError == 0U)
        {
            _doutWindowValid = 1U;
            _lineCount++;
            var lineChannels = Math.Min(_expectedNCols, 256U * effectiveNChip);
            _sampleLine = MakeSampleLine(lineChannels, effectiveIfs + (effectiveNChip << 8));
            if (_lineCount >= effectiveTLine)
            {
                _lineCount = 0U;
                _doutWindowValid = 0U;
            }
        }
        else
        {
            _doutWindowValid = 0U;
            _lineCount = 0U;
        }

        CycleCount++;
    }

    public override void SetInputs(SignalMap inputs)
    {
        _afeStart = SignalHelpers.GetScalar(inputs, "afe_start", _afeStart);
        _configReq = SignalHelpers.GetScalar(inputs, "config_req", _configReq);
        _cfgCombo = SignalHelpers.GetScalar(inputs, "cfg_combo", _cfgCombo);
        _cfgTLine = SignalHelpers.GetScalar(inputs, "cfg_tline", _cfgTLine);
        _cfgIfs = SignalHelpers.GetScalar(inputs, "cfg_ifs", _cfgIfs);
        _cfgLpf = SignalHelpers.GetScalar(inputs, "cfg_lpf", _cfgLpf);
        _cfgPMode = SignalHelpers.GetScalar(inputs, "cfg_pmode", _cfgPMode);
        _cfgNChip = SignalHelpers.GetScalar(inputs, "cfg_nchip", _cfgNChip);
        _afeType = SignalHelpers.GetScalar(inputs, "afe_type", _afeType);
    }

    public override SignalMap GetOutputs()
    {
        return new SignalMap
        {
            ["afe_type"] = _afeType,
            ["afe_ready"] = _afeReady,
            ["config_done"] = _configDone,
            ["dout_window_valid"] = _doutWindowValid,
            ["line_count"] = _lineCount,
            ["tline_error"] = _tlineError,
            ["ifs_width_error"] = _ifsWidthError,
            ["expected_ncols"] = _expectedNCols,
            ["cfg_mix"] = (EffectiveIfs(_afeType, _cfgIfs) ^ _cfgLpf ^ _cfgPMode ^ _cfgNChip) & 0xFFU,
            ["sample_line"] = _sampleLine,
            ["line_pixels"] = _sampleLine,
        };
    }

    private static uint ComboDefaultNCols(uint combo)
    {
        return combo switch
        {
            4U or 5U => 1664U,
            6U or 7U => 3072U,
            _ => 2048U,
        };
    }

    private static uint MinTLine(uint afeType)
    {
        return afeType == 1U ? 6000U : 2200U;
    }

    private static uint EffectiveIfs(uint afeType, uint ifs)
    {
        return afeType == 1U ? (ifs & 0x1FU) : (ifs & 0x3FU);
    }

    private static ushort[] MakeSampleLine(uint channels, uint seed)
    {
        var samples = new ushort[channels];
        for (var index = 0; index < channels; index++)
        {
            samples[index] = (ushort)((seed + (index * 17U)) & 0xFFFFU);
        }

        return samples;
    }
}
