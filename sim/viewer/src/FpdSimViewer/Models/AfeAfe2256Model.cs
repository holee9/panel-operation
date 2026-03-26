using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Models;

public sealed class AfeAfe2256Model : GoldenModelBase
{
    private const uint kAfe2256MinTLine = 5120U;

    private uint _afeStart;
    private uint _configReq;
    private uint _cfgTLine = kAfe2256MinTLine;
    private uint _cfgCicEn;
    private uint _cfgCicProfile;
    private uint _cfgPipelineEn;
    private uint _cfgTpSel;
    private uint _cfgNChip = 1U;
    private uint _afeReady;
    private uint _configDone;
    private uint _doutWindowValid;
    private uint _fclkExpected;
    private uint _lineCount;
    private uint _tlineError;
    private uint _pipelineLatencyRows;
    private ushort[] _previousRow = [];
    private ushort[] _currentRow = [];

    public override void Reset()
    {
        _afeStart = 0U;
        _configReq = 0U;
        _cfgTLine = kAfe2256MinTLine;
        _cfgCicEn = 0U;
        _cfgCicProfile = 0U;
        _cfgPipelineEn = 0U;
        _cfgTpSel = 0U;
        _cfgNChip = 1U;
        _afeReady = 0U;
        _configDone = 0U;
        _doutWindowValid = 0U;
        _fclkExpected = 0U;
        _lineCount = 0U;
        _tlineError = 0U;
        _pipelineLatencyRows = 0U;
        _previousRow = [];
        _currentRow = [];
        CycleCount = 0;
    }

    public override void Step()
    {
        _configDone = 0U;
        _tlineError = _cfgTLine < kAfe2256MinTLine ? 1U : 0U;

        if (_configReq != 0U && _afeReady == 0U)
        {
            _afeReady = 1U;
            _configDone = 1U;
        }

        if (_afeStart != 0U && _afeReady != 0U && _tlineError == 0U)
        {
            _doutWindowValid = 1U;
            _fclkExpected = 1U;
            _lineCount++;
            _currentRow = MakeRow(_cfgCicProfile, _cfgTpSel + (_cfgNChip << 4));
            if (_cfgPipelineEn != 0U)
            {
                _pipelineLatencyRows++;
                if (_previousRow.Length != 0)
                {
                    _currentRow = _previousRow;
                }

                _previousRow = MakeRow(_cfgCicProfile, _cfgTpSel + (_cfgNChip << 4) + 1U);
            }

            if (_cfgCicEn != 0U)
            {
                for (var index = 0; index < _currentRow.Length; index++)
                {
                    _currentRow[index] = (ushort)((_currentRow[index] * (_cfgCicProfile + 1U)) & 0xFFFFU);
                }
            }

            if (_lineCount >= _cfgTLine)
            {
                _lineCount = 0U;
                _doutWindowValid = 0U;
                _fclkExpected = 0U;
            }
        }
        else
        {
            _doutWindowValid = 0U;
            _fclkExpected = 0U;
            _lineCount = 0U;
        }

        CycleCount++;
    }

    public override void SetInputs(SignalMap inputs)
    {
        _afeStart = SignalHelpers.GetScalar(inputs, "afe_start", _afeStart);
        _configReq = SignalHelpers.GetScalar(inputs, "config_req", _configReq);
        _cfgTLine = SignalHelpers.GetScalar(inputs, "cfg_tline", _cfgTLine);
        _cfgCicEn = SignalHelpers.GetScalar(inputs, "cfg_cic_en", _cfgCicEn);
        _cfgCicProfile = SignalHelpers.GetScalar(inputs, "cfg_cic_profile", _cfgCicProfile);
        _cfgPipelineEn = SignalHelpers.GetScalar(inputs, "cfg_pipeline_en", _cfgPipelineEn);
        _cfgTpSel = SignalHelpers.GetScalar(inputs, "cfg_tp_sel", _cfgTpSel);
        _cfgNChip = SignalHelpers.GetScalar(inputs, "cfg_nchip", _cfgNChip);
    }

    public override SignalMap GetOutputs()
    {
        return new SignalMap
        {
            ["afe_ready"] = _afeReady,
            ["config_done"] = _configDone,
            ["dout_window_valid"] = _doutWindowValid,
            ["fclk_expected"] = _fclkExpected,
            ["line_count"] = _lineCount,
            ["tline_error"] = _tlineError,
            ["pipeline_latency_rows"] = _pipelineLatencyRows,
            ["previous_row"] = _previousRow,
            ["current_row"] = _currentRow,
            ["line_pixels"] = _currentRow,
        };
    }

    private static ushort[] MakeRow(uint profile, uint seed)
    {
        var row = new ushort[256];
        var gain = (ushort)((profile + 1U) * 7U);
        for (var index = 0; index < row.Length; index++)
        {
            row[index] = (ushort)((seed + (index * gain)) & 0xFFFFU);
        }

        return row;
    }
}
