// row_scan_eng.sv — Row index counter and Gate ON/OFF timing generator
// Shared across all Gate IC types, parameterized by MAX_ROWS
module row_scan_eng
  import fpd_params_pkg::*;
#(
    parameter ROW_WIDTH = 12  // Supports up to 4096 rows
)(
    input  logic        clk,
    input  logic        rst_n,

    // Control
    input  logic        scan_start,
    input  logic        scan_abort,
    input  logic        scan_dir,         // 0=forward (0→N), 1=reverse (N→0)
    input  logic [ROW_WIDTH-1:0] cfg_nrows, // Number of rows to scan

    // Timing (from reg_bank)
    input  logic [11:0] cfg_tgate_on,     // Gate ON pulse width
    input  logic [7:0]  cfg_tgate_settle, // Gate settle time after OFF

    // Gate IC control outputs
    output logic [ROW_WIDTH-1:0] row_index,
    output logic        gate_on_pulse,    // Gate ON active
    output logic        gate_settle,      // Post-OFF settle period

    // Status
    output logic        scan_active,
    output logic        row_done,         // Current row complete
    output logic        scan_done         // All rows complete
);

  // TODO: Implement row counter + timing FSM

endmodule
