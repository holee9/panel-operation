// gate_nv1047.sv — NV1047 Gate IC driver (C1-C5)
// 256/300 channels, shift register (SD1/SD2), OE/ONA control
// Max CLK = 200 kHz, bidirectional scan (L/R pin)
module gate_nv1047
  import fpd_params_pkg::*;
(
    input  logic        clk,
    input  logic        rst_n,

    // Control (from panel_ctrl_fsm)
    input  logic [11:0] row_index,
    input  logic        gate_on_pulse,
    input  logic        scan_dir,        // 0=up, 1=down
    input  logic        reset_all,       // Force all gates ON (ONA)

    // Timing parameters (from reg_bank)
    input  logic [15:0] cfg_clk_period,  // CLK period (10ns units, min 5us = 200kHz)
    input  logic [11:0] cfg_gate_on,     // OE=0 hold time (clk units)
    input  logic [7:0]  cfg_gate_settle, // Post-CLK settle time (clk units)
    input  logic [1:0]  cfg_mode,        // MD[1:0] channel mode

    // NV1047 output pins
    output logic        nv_sd1,          // Shift data 1
    output logic        nv_sd2,          // Shift data 2
    output logic        nv_clk,          // Shift clock
    output logic        nv_oe,           // Output enable (active low)
    output logic        nv_ona,          // All-on (active low)
    output logic        nv_lr,           // Scan direction
    output logic        nv_rst,          // Reset
    output logic [1:0]  nv_md,           // Mode select

    // Status
    output logic        row_done
);

  logic gate_on_prev;

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      nv_sd1 <= 1'b0;
      nv_sd2 <= 1'b0;
      nv_clk <= 1'b0;
      nv_oe <= 1'b1;
      nv_ona <= 1'b1;
      nv_lr <= 1'b0;
      nv_rst <= 1'b0;
      nv_md <= 2'b00;
      row_done <= 1'b0;
      gate_on_prev <= 1'b0;
    end else begin
      nv_sd1 <= row_index[0];
      nv_sd2 <= row_index[1];
      nv_clk <= gate_on_pulse ? clk : 1'b0;
      nv_oe <= ~gate_on_pulse;
      nv_ona <= ~reset_all;
      nv_lr <= scan_dir;
      nv_rst <= rst_n;
      nv_md <= cfg_mode;
      row_done <= gate_on_prev && !gate_on_pulse;
      gate_on_prev <= gate_on_pulse;
    end
  end

endmodule
