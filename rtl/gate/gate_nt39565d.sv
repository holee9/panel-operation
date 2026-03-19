// gate_nt39565d.sv — NT39565D Gate IC driver (C6-C7)
// 541 channels, dual STV, CPV clock, OE1/OE2 split control
// Supports 6-chip cascade, 2G mode, bidirectional scan
module gate_nt39565d
  import fpd_params_pkg::*;
(
    input  logic        clk,
    input  logic        rst_n,

    // Control (from panel_ctrl_fsm)
    input  logic [11:0] row_index,
    input  logic        gate_on_pulse,
    input  logic        scan_dir,        // 0=down, 1=up
    input  logic [1:0]  chip_sel,        // CHIP_SEL[1:0] mode
    input  logic [1:0]  mode_sel,        // MODE1/MODE2

    // Timing parameters (from reg_bank)
    input  logic [15:0] cfg_cpv_period,  // CPV clock period
    input  logic [15:0] cfg_stv_pulse,   // STV pulse width
    input  logic [11:0] cfg_gate_on,     // Gate ON time

    // NT39565D output pins (Left/Right for dual-STV)
    output logic        nt_stv1l,        // Start vertical pulse 1 (left)
    output logic        nt_stv2l,        // Start vertical pulse 2 (left)
    output logic        nt_stv1r,        // Start vertical pulse 1 (right)
    output logic        nt_stv2r,        // Start vertical pulse 2 (right)
    output logic        nt_cpv_l,        // Pixel clock (left)
    output logic        nt_cpv_r,        // Pixel clock (right)
    output logic        nt_lr,           // Scan direction
    output logic        nt_oe1_l,        // Output enable 1 (left, odd)
    output logic        nt_oe1_r,        // Output enable 1 (right, odd)
    output logic        nt_oe2_l,        // Output enable 2 (left, even)
    output logic        nt_oe2_r,        // Output enable 2 (right, even)

    // Cascade monitoring
    input  logic        cascade_stv_return, // STVD from last IC in chain
    output logic        cascade_complete,

    // Status
    output logic        row_done
);

  // TODO: Implement dual-STV + CPV + cascade state machine

endmodule
