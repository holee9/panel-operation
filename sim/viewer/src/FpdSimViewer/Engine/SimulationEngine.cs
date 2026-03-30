using FpdSimViewer.Models;
using FpdSimViewer.Models.Core;

namespace FpdSimViewer.Engine;

public sealed class SimulationEngine
{
    public const double VghDefault = 20.0;
    public const double VglDefault = -10.0;
    public const double VpdDefault = -1.5;
    public const double LogicHigh = 3.3;

    private uint _latchedCtrlStart;
    private uint _latchedCtrlAbort;
    private uint _previousFsmState;
    private uint _powerTargetMode = 1U;
    private uint _vglStable = 1U;
    private uint _vghStable = 1U;
    private uint _faultVghOver;
    private uint _faultVghUnder;
    private uint _faultTempOver;
    private uint _faultPllUnlocked;
    private uint _hwEmergencyN = 1U;
    private uint _xrayReady;
    private uint _xrayPrepReq;
    private uint _xrayOn;
    private uint _xrayOff;

    public SimulationEngine()
    {
        RegBank = new RegBankModel();
        ClkRst = new ClkRstModel();
        PowerSeq = new PowerSeqModel();
        EmergencyShutdown = new EmergencyShutdownModel();
        PanelFsm = new PanelFsmModel();
        RowScan = new RowScanModel();
        ProtMon = new ProtMonModel();
        Radiog = new RadiogModel();
        TraceCapture = new TraceCapture();
        RequirementTracker = new RequirementTracker();
        ComboConfig = HardwareComboConfig.Create(1);
        CurrentSnapshot = CreateSnapshot();
        Reset();
    }

    public RegBankModel RegBank { get; }

    public ClkRstModel ClkRst { get; private set; }

    public PowerSeqModel PowerSeq { get; }

    public EmergencyShutdownModel EmergencyShutdown { get; }

    public PanelFsmModel PanelFsm { get; }

    public RowScanModel RowScan { get; }

    public ProtMonModel ProtMon { get; }

    public RadiogModel Radiog { get; }

    public HardwareComboConfig ComboConfig { get; private set; }

    public SimulationSnapshot CurrentSnapshot { get; private set; }

    public TraceCapture TraceCapture { get; }

    public RequirementTracker RequirementTracker { get; }

    public ulong CycleCount { get; private set; }

    public void Reset()
    {
        RegBank.Reset();
        ClkRst.Reset();
        PowerSeq.Reset();
        EmergencyShutdown.Reset();
        PanelFsm.Reset();
        RowScan.Reset();
        ComboConfig.GateDriver.Reset();
        ComboConfig.AfeModel.Reset();
        ProtMon.Reset();
        Radiog.Reset();
        CycleCount = 0U;
        _latchedCtrlStart = 0U;
        _latchedCtrlAbort = 0U;
        _previousFsmState = 0U;
        TraceCapture.Clear();
        ApplyComboRegisters();
        CurrentSnapshot = CreateSnapshot();
    }

    public void SetCombo(int comboId)
    {
        ComboConfig = HardwareComboConfig.Create(comboId);
        Reset();
    }

    public void SetMode(uint mode)
    {
        RegBank.Write(FoundationConstants.kRegMode, (ushort)(mode & 0x7U));
    }

    public void SetPowerInputs(uint targetMode, bool vglStable, bool vghStable)
    {
        _powerTargetMode = targetMode;
        _vglStable = vglStable ? 1U : 0U;
        _vghStable = vghStable ? 1U : 0U;
    }

    public void SetFaultInputs(bool vghOver, bool vghUnder, bool tempOver, bool pllUnlocked, bool hwEmergencyActive)
    {
        _faultVghOver = vghOver ? 1U : 0U;
        _faultVghUnder = vghUnder ? 1U : 0U;
        _faultTempOver = tempOver ? 1U : 0U;
        _faultPllUnlocked = pllUnlocked ? 1U : 0U;
        _hwEmergencyN = hwEmergencyActive ? 0U : 1U;
    }

    public void SetXrayInputs(bool ready, bool prepReq, bool xrayOn, bool xrayOff)
    {
        _xrayReady = ready ? 1U : 0U;
        _xrayPrepReq = prepReq ? 1U : 0U;
        _xrayOn = xrayOn ? 1U : 0U;
        _xrayOff = xrayOff ? 1U : 0U;
    }

    public void WriteRegister(byte addr, ushort value)
    {
        if (addr == FoundationConstants.kRegCombo)
        {
            SetCombo(value & 0x7);
            return;
        }

        RegBank.Write(addr, value);
        if (addr == FoundationConstants.kRegCtrl)
        {
            _latchedCtrlStart = (uint)(value & 0x1U);
            _latchedCtrlAbort = (uint)((value >> 1) & 0x1U);
        }
    }

    public SimulationSnapshot Step()
    {
        var previousGateOutputs = ComboConfig.GateDriver.GetOutputs();
        var previousAfeOutputs = ComboConfig.AfeModel.GetOutputs();
        var previousProtOutputs = ProtMon.GetOutputs();

        RegBank.Step();
        var regOutputs = RegBank.GetOutputs();
        if (_latchedCtrlStart != 0U)
        {
            regOutputs["ctrl_start"] = _latchedCtrlStart;
        }

        if (_latchedCtrlAbort != 0U)
        {
            regOutputs["ctrl_abort"] = _latchedCtrlAbort;
        }

        ClkRst.SetInputs(new SignalMap
        {
            ["rst_ext_n"] = 1U,
            ["afe_type_sel"] = ComboConfig.AfeModel is AfeAfe2256Model ? 2U : 0U,
        });
        ClkRst.Step();

        PowerSeq.SetInputs(new SignalMap
        {
            ["target_mode"] = _powerTargetMode,
            ["vgl_stable"] = _vglStable,
            ["vgh_stable"] = _vghStable,
        });
        PowerSeq.Step();
        var powerOutputs = PowerSeq.GetOutputs();

        EmergencyShutdown.SetInputs(new SignalMap
        {
            ["vgh_over"] = _faultVghOver,
            ["vgh_under"] = _faultVghUnder,
            ["temp_over"] = _faultTempOver,
            ["pll_unlocked"] = _faultPllUnlocked,
            ["hw_emergency_n"] = _hwEmergencyN,
        });
        EmergencyShutdown.Step();
        var shutdownOutputs = EmergencyShutdown.GetOutputs();

        var radiographyMode = SignalHelpers.GetScalar(regOutputs, "cfg_mode") == 2U ? 1U : 0U;
        PanelFsm.SetInputs(new SignalMap
        {
            ["ctrl_start"] = SignalHelpers.GetScalar(regOutputs, "ctrl_start"),
            ["ctrl_abort"] = SignalHelpers.GetScalar(regOutputs, "ctrl_abort"),
            ["cfg_mode"] = SignalHelpers.GetScalar(regOutputs, "cfg_mode"),
            ["cfg_combo"] = SignalHelpers.GetScalar(regOutputs, "cfg_combo"),
            ["cfg_nrows"] = SignalHelpers.GetScalar(regOutputs, "cfg_nrows"),
            ["cfg_treset"] = SignalHelpers.GetScalar(regOutputs, "cfg_treset"),
            ["cfg_tinteg"] = SignalHelpers.GetScalar(regOutputs, "cfg_tinteg"),
            ["cfg_nreset"] = SignalHelpers.GetScalar(regOutputs, "cfg_nreset"),
            ["cfg_sync_dly"] = SignalHelpers.GetScalar(regOutputs, "cfg_sync_dly"),
            ["cfg_tgate_settle"] = SignalHelpers.GetScalar(regOutputs, "cfg_tgate_settle"),
            ["gate_row_done"] = SignalHelpers.GetScalar(previousGateOutputs, "row_done"),
            ["afe_config_done"] = SignalHelpers.GetScalar(previousAfeOutputs, "config_done"),
            ["afe_line_valid"] = SignalHelpers.GetScalar(previousAfeOutputs, "dout_window_valid"),
            ["xray_prep_req"] = _xrayPrepReq,
            ["xray_on"] = _xrayOn,
            ["xray_off"] = _xrayOff,
            ["prot_error"] = SignalHelpers.GetScalar(previousProtOutputs, "err_flag"),
            ["prot_force_stop"] = SignalHelpers.GetScalar(previousProtOutputs, "force_gate_off"),
            ["radiography_mode"] = radiographyMode,
        });
        PanelFsm.Step();
        var fsmOutputs = PanelFsm.GetOutputs();

        RowScan.SetInputs(new SignalMap
        {
            ["scan_start"] = SignalHelpers.GetScalar(fsmOutputs, "fsm_state") == 7U && _previousFsmState != 7U ? 1U : 0U,
            ["scan_abort"] = SignalHelpers.GetScalar(shutdownOutputs, "force_gate_off") |
                             SignalHelpers.GetScalar(regOutputs, "ctrl_abort"),
            ["scan_dir"] = SignalHelpers.GetScalar(regOutputs, "cfg_scan_dir"),
            ["cfg_nrows"] = SignalHelpers.GetScalar(regOutputs, "cfg_nrows"),
        });
        RowScan.Step();
        var rowOutputs = RowScan.GetOutputs();

        if (ComboConfig.GateDriver is GateNv1047Model gateNv1047)
        {
            gateNv1047.SetInputs(new SignalMap
            {
                ["row_index"] = SignalHelpers.GetScalar(rowOutputs, "row_index"),
                ["gate_on_pulse"] = SignalHelpers.GetScalar(rowOutputs, "gate_on_pulse"),
                ["scan_dir"] = SignalHelpers.GetScalar(regOutputs, "cfg_scan_dir"),
                ["reset_all"] = SignalHelpers.GetScalar(shutdownOutputs, "force_gate_off"),
                ["cfg_clk_period"] = SignalHelpers.GetScalar(regOutputs, "cfg_tline"),
                ["cfg_gate_on"] = SignalHelpers.GetScalar(regOutputs, "cfg_tgate_on"),
                ["cfg_gate_settle"] = SignalHelpers.GetScalar(regOutputs, "cfg_tgate_settle"),
            });
            gateNv1047.Step();
        }
        else if (ComboConfig.GateDriver is GateNt39565dModel gateNt39565d)
        {
            gateNt39565d.SetInputs(new SignalMap
            {
                ["row_index"] = SignalHelpers.GetScalar(rowOutputs, "row_index"),
                ["gate_on_pulse"] = SignalHelpers.GetScalar(rowOutputs, "gate_on_pulse"),
                ["scan_dir"] = SignalHelpers.GetScalar(regOutputs, "cfg_scan_dir"),
                ["chip_sel"] = 2U,
                ["mode_sel"] = 1U,
                ["cascade_stv_return"] = SignalHelpers.GetScalar(rowOutputs, "gate_on_pulse"),
            });
            gateNt39565d.Step();
        }

        var gateOutputs = ComboConfig.GateDriver.GetOutputs();

        if (ComboConfig.AfeModel is AfeAd711xxModel afeAd711xx)
        {
            afeAd711xx.SetInputs(new SignalMap
            {
                ["afe_start"] = SignalHelpers.GetScalar(fsmOutputs, "fsm_state") == 7U ? 1U : 0U,
                ["config_req"] = SignalHelpers.GetScalar(fsmOutputs, "fsm_state") == 6U ? 1U : 0U,
                ["cfg_combo"] = (uint)ComboConfig.ComboId,
                ["cfg_tline"] = SignalHelpers.GetScalar(regOutputs, "cfg_tline"),
                ["cfg_ifs"] = SignalHelpers.GetScalar(regOutputs, "cfg_afe_ifs"),
                ["cfg_lpf"] = SignalHelpers.GetScalar(regOutputs, "cfg_afe_lpf"),
                ["cfg_pmode"] = SignalHelpers.GetScalar(regOutputs, "cfg_afe_pmode"),
                ["cfg_nchip"] = ComboConfig.AfeChips,
                ["afe_type"] = ComboConfig.AfeTypeId,
            });
            afeAd711xx.Step();
        }
        else if (ComboConfig.AfeModel is AfeAfe2256Model afeAfe2256)
        {
            afeAfe2256.SetInputs(new SignalMap
            {
                ["afe_start"] = SignalHelpers.GetScalar(fsmOutputs, "fsm_state") == 7U ? 1U : 0U,
                ["config_req"] = SignalHelpers.GetScalar(fsmOutputs, "fsm_state") == 6U ? 1U : 0U,
                ["cfg_tline"] = SignalHelpers.GetScalar(regOutputs, "cfg_tline"),
                ["cfg_cic_en"] = SignalHelpers.GetScalar(regOutputs, "cfg_cic_en"),
                ["cfg_cic_profile"] = SignalHelpers.GetScalar(regOutputs, "cfg_cic_profile"),
                ["cfg_pipeline_en"] = SignalHelpers.GetScalar(regOutputs, "cfg_pipeline_en"),
                ["cfg_tp_sel"] = SignalHelpers.GetScalar(regOutputs, "cfg_tp_sel"),
                ["cfg_nchip"] = ComboConfig.AfeChips,
            });
            afeAfe2256.Step();
        }

        var afeOutputs = ComboConfig.AfeModel.GetOutputs();

        Radiog.SetInputs(new SignalMap
        {
            ["start"] = SignalHelpers.GetScalar(regOutputs, "ctrl_start"),
            ["xray_ready"] = _xrayReady,
            ["xray_on"] = _xrayOn,
            ["xray_off"] = _xrayOff,
            ["dark_frame_mode"] = SignalHelpers.GetScalar(regOutputs, "cfg_mode") == 3U ? 1U : 0U,
            ["frame_valid"] = SignalHelpers.GetScalar(afeOutputs, "dout_window_valid"),
            ["frame_pixels"] = ExtractAfePixels(afeOutputs),
        });
        Radiog.Step();
        var radiogOutputs = Radiog.GetOutputs();

        ProtMon.SetInputs(new SignalMap
        {
            ["fsm_state"] = SignalHelpers.GetScalar(fsmOutputs, "fsm_state"),
            ["xray_active"] = SignalHelpers.GetScalar(radiogOutputs, "xray_enable"),
            ["cfg_max_exposure"] = 0U,
            ["radiography_mode"] = radiographyMode,
        });
        ProtMon.Step();
        var protOutputs = ProtMon.GetOutputs();

        RegBank.SetStatus(
            SignalHelpers.GetScalar(fsmOutputs, "sts_busy") != 0U,
            SignalHelpers.GetScalar(fsmOutputs, "sts_done") != 0U,
            SignalHelpers.GetScalar(fsmOutputs, "sts_error") != 0U || SignalHelpers.GetScalar(protOutputs, "err_flag") != 0U,
            SignalHelpers.GetScalar(afeOutputs, "dout_window_valid") != 0U,
            (ushort)SignalHelpers.GetScalar(fsmOutputs, "sts_line_idx"),
            (byte)SignalHelpers.GetScalar(fsmOutputs, "sts_err_code"));

        CycleCount++;
        CurrentSnapshot = CreateSnapshot(fsmOutputs, rowOutputs, gateOutputs, afeOutputs, protOutputs, powerOutputs);

        TraceCapture.Record(CurrentSnapshot);
        RequirementTracker.Evaluate(CurrentSnapshot);

        _latchedCtrlStart = 0U;
        _latchedCtrlAbort = 0U;
        _previousFsmState = SignalHelpers.GetScalar(fsmOutputs, "fsm_state");
        return CurrentSnapshot;
    }

    public SimulationSnapshot RefreshSnapshot()
    {
        CurrentSnapshot = CreateSnapshot();
        return CurrentSnapshot;
    }

    private void ApplyComboRegisters()
    {
        RegBank.Write(FoundationConstants.kRegCombo, (ushort)ComboConfig.ComboId);
        RegBank.Write(FoundationConstants.kRegNRows, (ushort)ComboConfig.Rows);
        RegBank.Write(FoundationConstants.kRegNCols, (ushort)ComboConfig.Cols);
        RegBank.Write(FoundationConstants.kRegAfeNChip, (ushort)ComboConfig.AfeChips);
    }

    private ushort[] ReadRegisters()
    {
        var registers = new ushort[32];
        for (byte index = 0; index < registers.Length; index++)
        {
            registers[index] = RegBank.Read(index);
        }

        return registers;
    }

    private static ushort[] ExtractAfePixels(SignalMap afeOutputs)
    {
        var pixels = SignalHelpers.GetVector(afeOutputs, "line_pixels");
        if (pixels.Length != 0)
        {
            return (ushort[])pixels.Clone();
        }

        pixels = SignalHelpers.GetVector(afeOutputs, "sample_line");
        if (pixels.Length != 0)
        {
            return (ushort[])pixels.Clone();
        }

        pixels = SignalHelpers.GetVector(afeOutputs, "current_row");
        return pixels.Length != 0 ? (ushort[])pixels.Clone() : [];
    }

    private SimulationSnapshot CreateSnapshot()
    {
        return CreateSnapshot(
            PanelFsm.GetOutputs(),
            RowScan.GetOutputs(),
            ComboConfig.GateDriver.GetOutputs(),
            ComboConfig.AfeModel.GetOutputs(),
            ProtMon.GetOutputs(),
            PowerSeq.GetOutputs());
    }

    private SimulationSnapshot CreateSnapshot(
        SignalMap fsmOutputs,
        SignalMap rowOutputs,
        SignalMap gateOutputs,
        SignalMap afeOutputs,
        SignalMap protOutputs,
        SignalMap powerOutputs)
    {
        return SimulationSnapshot.Capture(
            CycleCount,
            fsmOutputs,
            rowOutputs,
            gateOutputs,
            afeOutputs,
            protOutputs,
            powerOutputs,
            ReadRegisters());
    }
}
