// panel_integ_ctrl.sv — Integration window timing control
// Manages X-ray exposure timing in TRIGGERED mode
// Handles PREP_REQUEST / X_RAY_ENABLE handshake with generator
module panel_integ_ctrl
  import fpd_params_pkg::*;
(
    input  logic        clk,
    input  logic        rst_n,

    // Control
    input  logic        integ_start,
    input  logic [23:0] cfg_tinteg,      // Integration time (10ns units)
    input  logic        triggered_mode,  // 1 = wait for X-ray trigger

    // X-ray generator signals
    input  logic        xray_prep_req,
    output logic        xray_enable,
    input  logic        xray_on,
    input  logic        xray_off,

    // Status
    output logic        integ_active,    // Integration window open
    output logic        integ_done,      // Integration complete
    output logic [23:0] integ_count      // Elapsed integration time
);

  // TODO: Implement integration timing with generator handshake

endmodule
