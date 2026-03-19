// panel_reset_ctrl.sv — Panel reset and initialization sequence
// Executes dummy scans (3-8 cycles) for TFT charge equilibration
// Used during RESET state and pre-exposure preparation
module panel_reset_ctrl
  import fpd_params_pkg::*;
(
    input  logic        clk,
    input  logic        rst_n,

    // Control
    input  logic        reset_start,
    input  logic [15:0] cfg_treset,      // Reset duration
    input  logic [3:0]  cfg_dummy_scans, // Number of dummy scan cycles

    // Gate IC interface (request dummy scan)
    output logic        dummy_scan_req,
    input  logic        dummy_scan_done,

    // Status
    output logic        reset_busy,
    output logic        reset_done
);

  // TODO: Implement reset sequence with configurable dummy scans

endmodule
