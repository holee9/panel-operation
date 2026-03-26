using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Models;

public sealed class RadiogModel : GoldenModelBase
{
    private uint _start;
    private uint _xrayReady;
    private uint _xrayOn;
    private uint _xrayOff;
    private uint _darkFrameMode;
    private uint _frameValid;
    private uint _cfgDarkCnt = 64U;
    private uint _cfgTSettle = 100U;
    private uint _cfgPrepTimeout = 30U;
    private uint _state;
    private uint _xrayEnable;
    private uint _error;
    private uint _done;
    private uint _darkAvgReady;
    private uint _darkFramesCaptured;
    private uint _timer;
    private uint _xraySeenOn;
    private uint _prevFrameValid;
    private ushort[] _framePixels = [];
    private uint[] _darkAccum = [];
    private ushort[] _avgDarkFrame = [];

    public override void Reset()
    {
        _start = 0U;
        _xrayReady = 0U;
        _xrayOn = 0U;
        _xrayOff = 0U;
        _darkFrameMode = 0U;
        _frameValid = 0U;
        _cfgDarkCnt = 64U;
        _cfgTSettle = 100U;
        _cfgPrepTimeout = 30U;
        _state = 0U;
        _xrayEnable = 0U;
        _error = 0U;
        _done = 0U;
        _darkAvgReady = 0U;
        _darkFramesCaptured = 0U;
        _timer = 0U;
        _xraySeenOn = 0U;
        _prevFrameValid = 0U;
        _framePixels = [];
        _darkAccum = [];
        _avgDarkFrame = [];
        CycleCount = 0;
    }

    public override void Step()
    {
        _done = 0U;
        _darkAvgReady = 0U;

        switch (_state)
        {
            case 0U:
                if (_start != 0U)
                {
                    _error = 0U;
                    _darkFramesCaptured = 0U;
                    _timer = 0U;
                    _xraySeenOn = 0U;
                    _darkAccum = [];
                    _avgDarkFrame = [];
                    _state = _darkFrameMode != 0U ? 4U : 1U;
                    if (_darkFrameMode != 0U &&
                        (((_frameValid != 0U) && (_prevFrameValid == 0U)) || _framePixels.Length == 0))
                    {
                        CaptureDarkFrame();
                        if (_darkFramesCaptured >= _cfgDarkCnt)
                        {
                            _darkAvgReady = 1U;
                            _done = 1U;
                            _state = 0U;
                        }
                    }
                }

                break;
            case 1U:
                if (_xrayReady != 0U)
                {
                    _state = 2U;
                    _xrayEnable = 1U;
                    _timer = 0U;
                }
                else if (++_timer >= _cfgPrepTimeout)
                {
                    _error = 1U;
                    _state = 7U;
                }

                break;
            case 2U:
                _xrayEnable = 1U;
                if (_xrayOn != 0U)
                {
                    _xraySeenOn = 1U;
                }

                if (_xraySeenOn != 0U && _xrayOff != 0U)
                {
                    _state = 3U;
                    _timer = 0U;
                }

                break;
            case 3U:
                if (++_timer >= _cfgTSettle)
                {
                    _done = 1U;
                    _xrayEnable = 0U;
                    _state = 0U;
                }

                break;
            case 4U:
                if (((_frameValid != 0U) && (_prevFrameValid == 0U)) || _framePixels.Length == 0)
                {
                    CaptureDarkFrame();
                }

                if (_darkFramesCaptured >= _cfgDarkCnt)
                {
                    _darkAvgReady = 1U;
                    _done = 1U;
                    _state = 0U;
                }

                break;
            default:
                break;
        }

        _prevFrameValid = _frameValid;
        CycleCount++;
    }

    public override void SetInputs(SignalMap inputs)
    {
        _start = SignalHelpers.GetScalar(inputs, "start", _start);
        _xrayReady = SignalHelpers.GetScalar(inputs, "xray_ready", _xrayReady);
        _xrayOn = SignalHelpers.GetScalar(inputs, "xray_on", _xrayOn);
        _xrayOff = SignalHelpers.GetScalar(inputs, "xray_off", _xrayOff);
        _darkFrameMode = SignalHelpers.GetScalar(inputs, "dark_frame_mode", _darkFrameMode);
        _frameValid = SignalHelpers.GetScalar(inputs, "frame_valid", _frameValid);
        _cfgDarkCnt = SignalHelpers.GetScalar(inputs, "cfg_dark_cnt", _cfgDarkCnt);
        _cfgTSettle = SignalHelpers.GetScalar(inputs, "cfg_tsettle", _cfgTSettle);
        _cfgPrepTimeout = SignalHelpers.GetScalar(inputs, "cfg_prep_timeout", _cfgPrepTimeout);
        _framePixels = SignalHelpers.GetVector(inputs, "frame_pixels");
    }

    public override SignalMap GetOutputs()
    {
        return new SignalMap
        {
            ["state"] = _state,
            ["xray_enable"] = _xrayEnable,
            ["frame_valid"] = _frameValid,
            ["error"] = _error,
            ["done"] = _done,
            ["dark_avg_ready"] = _darkAvgReady,
            ["dark_frames_captured"] = _darkFramesCaptured,
            ["frame_pixels"] = _framePixels,
            ["dark_avg_frame"] = _avgDarkFrame,
        };
    }

    private void CaptureDarkFrame()
    {
        var frame = _framePixels.Length == 0 ? BuildSyntheticDarkFrame(_darkFramesCaptured) : _framePixels;
        if (_darkAccum.Length != frame.Length)
        {
            _darkAccum = new uint[frame.Length];
        }

        if (_avgDarkFrame.Length != frame.Length)
        {
            _avgDarkFrame = new ushort[frame.Length];
        }

        for (var index = 0; index < frame.Length; index++)
        {
            _darkAccum[index] += frame[index];
            var divisor = _darkFramesCaptured + 1U;
            _avgDarkFrame[index] = (ushort)(_darkAccum[index] / divisor);
        }

        _darkFramesCaptured++;
    }

    private static ushort[] BuildSyntheticDarkFrame(uint frameIndex, int width = 8)
    {
        var frame = new ushort[width];
        for (var index = 0; index < width; index++)
        {
            frame[index] = (ushort)((frameIndex * 11U + ((uint)index * 3U)) & 0x0FFFU);
        }

        return frame;
    }
}
