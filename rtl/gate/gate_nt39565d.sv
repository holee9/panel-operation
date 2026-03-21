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

  logic gate_on_prev;
  logic [15:0] cpv_count;
  logic [15:0] stv_count;
  logic        cpv_toggle;
  logic        left_bank_active;
  logic        right_bank_active;
  logic        stv_phase_sel;

  function automatic logic [15:0] safe_period(input logic [15:0] value);
    begin
      safe_period = (value < 16'd500) ? 16'd500 : value;
    end
  endfunction

  always_comb begin
    stv_phase_sel = scan_dir ? ~row_index[0] : row_index[0];
    left_bank_active = (chip_sel == 2'b00) || (chip_sel == 2'b10) || (scan_dir == 1'b0);
    right_bank_active = (chip_sel == 2'b01) || (chip_sel == 2'b10) || (scan_dir == 1'b1);
  end

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      nt_stv1l <= 1'b0;
      nt_stv2l <= 1'b0;
      nt_stv1r <= 1'b0;
      nt_stv2r <= 1'b0;
      nt_cpv_l <= 1'b0;
      nt_cpv_r <= 1'b0;
      nt_lr <= 1'b0;
      nt_oe1_l <= 1'b0;
      nt_oe1_r <= 1'b0;
      nt_oe2_l <= 1'b0;
      nt_oe2_r <= 1'b0;
      cascade_complete <= 1'b0;
      row_done <= 1'b0;
      gate_on_prev <= 1'b0;
      cpv_count <= '0;
      stv_count <= '0;
      cpv_toggle <= 1'b0;
    end else begin
      nt_lr <= scan_dir;
      if (gate_on_pulse) begin
        if (cpv_count + 16'd1 >= safe_period(cfg_cpv_period)) begin
          cpv_count <= '0;
          cpv_toggle <= ~cpv_toggle;
        end else begin
          cpv_count <= cpv_count + 16'd1;
        end

        if (stv_count < cfg_stv_pulse) begin
          stv_count <= stv_count + 16'd1;
          nt_stv1l <= left_bank_active && !stv_phase_sel;
          nt_stv2l <= left_bank_active && stv_phase_sel;
          nt_stv1r <= right_bank_active && !stv_phase_sel;
          nt_stv2r <= right_bank_active && stv_phase_sel;
        end else begin
          nt_stv1l <= 1'b0;
          nt_stv2l <= 1'b0;
          nt_stv1r <= 1'b0;
          nt_stv2r <= 1'b0;
        end
      end else begin
        cpv_count <= '0;
        stv_count <= '0;
        cpv_toggle <= 1'b0;
        nt_stv1l <= 1'b0;
        nt_stv2l <= 1'b0;
        nt_stv1r <= 1'b0;
        nt_stv2r <= 1'b0;
      end

      nt_cpv_l <= gate_on_pulse ? cpv_toggle : 1'b0;
      nt_cpv_r <= gate_on_pulse ? cpv_toggle : 1'b0;
      nt_oe1_l <= gate_on_pulse && left_bank_active && !row_index[0];
      nt_oe1_r <= gate_on_pulse && right_bank_active && !row_index[0];
      nt_oe2_l <= gate_on_pulse && left_bank_active && row_index[0];
      nt_oe2_r <= gate_on_pulse && right_bank_active && row_index[0];
      cascade_complete <= gate_on_pulse && cascade_stv_return &&
                          ((chip_sel == 2'b10) || (mode_sel != 2'b00));
      row_done <= gate_on_prev && !gate_on_pulse;
      gate_on_prev <= gate_on_pulse;
    end
  end

endmodule
