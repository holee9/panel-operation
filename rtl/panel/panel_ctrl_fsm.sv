// panel_ctrl_fsm.sv — Main panel driving FSM
// States: IDLE → RESET → INTEGRATE → READOUT_INIT → SCAN_LINE → READOUT_DONE → DONE
// Modes: STATIC, CONTINUOUS, TRIGGERED, DARK_FRAME, RESET_ONLY
// All timing from reg_bank (no hardcoded values)
module panel_ctrl_fsm
  import fpd_types_pkg::*;
  import fpd_params_pkg::*;
(
    input  logic        clk,
    input  logic        rst_n,

    // Control (from reg_bank)
    input  logic        ctrl_start,
    input  logic        ctrl_abort,
    input  op_mode_t    cfg_mode,
    input  logic [15:0] cfg_treset,
    input  logic [23:0] cfg_tinteg,
    input  logic [11:0] cfg_nrows,

    // X-ray generator interface
    input  logic        xray_prep_req,   // Generator ready
    output logic        xray_enable,     // Detector ready for exposure
    input  logic        xray_on,         // Exposure active
    input  logic        xray_off,        // Exposure complete

    // Gate IC driver control
    output logic        gate_start_scan,
    output logic [11:0] gate_row_index,
    output logic        gate_reset_all,
    input  logic        gate_row_done,

    // AFE control
    output logic        afe_start,
    output logic        afe_config_req,
    input  logic        afe_config_done,
    input  logic        afe_line_valid,

    // Protection monitor
    input  logic        prot_error,
    input  logic        prot_force_stop,

    // Status outputs
    output fsm_state_t  fsm_state,
    output logic        sts_busy,
    output logic        sts_done,
    output logic        sts_error,
    output logic [11:0] sts_line_idx,
    output logic [7:0]  sts_err_code
);

  // TODO: Implement main FSM with mode-dependent behavior

endmodule
