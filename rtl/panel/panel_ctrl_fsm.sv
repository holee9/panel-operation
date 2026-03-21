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
    input  logic [7:0]  cfg_nreset,
    input  logic [7:0]  cfg_sync_dly,
    input  logic [7:0]  cfg_tgate_settle,
    input  logic        radiography_mode,

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

  localparam logic [7:0] ERR_ABORT        = 8'h01;
  localparam logic [7:0] ERR_XRAY_TIMEOUT = 8'h02;
  localparam logic [7:0] ERR_PROTECTION   = 8'h03;
  localparam logic [31:0] XRAY_TIMEOUT_5S  = SYS_CLK_HZ * 5;
  localparam logic [31:0] XRAY_TIMEOUT_30S = SYS_CLK_HZ * 30;

  logic [31:0] timer_count;
  logic [31:0] xray_wait_count;
  logic [11:0] row_index;

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      fsm_state <= ST_IDLE;
      sts_busy <= 1'b0;
      sts_done <= 1'b0;
      sts_error <= 1'b0;
      sts_line_idx <= '0;
      sts_err_code <= '0;
      xray_enable <= 1'b0;
      gate_start_scan <= 1'b0;
      gate_row_index <= '0;
      gate_reset_all <= 1'b0;
      afe_start <= 1'b0;
      afe_config_req <= 1'b0;
      timer_count <= '0;
      xray_wait_count <= '0;
      row_index <= '0;
    end else begin
      sts_done <= 1'b0;
      gate_start_scan <= 1'b0;
      afe_start <= 1'b0;
      afe_config_req <= 1'b0;
      gate_reset_all <= 1'b0;
      xray_enable <= 1'b0;

      if (prot_error || prot_force_stop) begin
        fsm_state <= ST_ERROR;
        sts_busy <= 1'b0;
        sts_error <= 1'b1;
        sts_err_code <= ERR_PROTECTION;
      end else if (ctrl_abort) begin
        fsm_state <= ST_IDLE;
        sts_busy <= 1'b0;
        sts_error <= 1'b1;
        sts_err_code <= ERR_ABORT;
      end else begin
        case (fsm_state)
          ST_IDLE: begin
            sts_busy <= 1'b0;
            sts_error <= 1'b0;
            sts_err_code <= 8'h00;
            timer_count <= '0;
            xray_wait_count <= '0;
            row_index <= '0;
            sts_line_idx <= '0;
            if (ctrl_start) begin
              fsm_state <= ST_RESET;
              sts_busy <= 1'b1;
            end
          end

          ST_RESET: begin
            gate_reset_all <= 1'b1;
            timer_count <= timer_count + 32'd1;
            if (timer_count >= (cfg_treset + cfg_nreset)) begin
              timer_count <= '0;
              if (cfg_mode == MODE_RESET_ONLY) begin
                fsm_state <= ST_DONE;
              end else begin
                fsm_state <= ST_INTEGRATE;
              end
            end
          end

          ST_INTEGRATE: begin
            if (cfg_mode == MODE_TRIGGERED) begin
              xray_enable <= 1'b1;
              if (xray_on || xray_prep_req) begin
                timer_count <= timer_count + 32'd1;
                if (xray_off || timer_count >= cfg_tinteg) begin
                  timer_count <= '0;
                  fsm_state <= ST_READOUT_INIT;
                end
              end else begin
                xray_wait_count <= xray_wait_count + 32'd1;
                if (xray_wait_count >= (radiography_mode ? XRAY_TIMEOUT_30S : XRAY_TIMEOUT_5S)) begin
                  fsm_state <= ST_ERROR;
                  sts_error <= 1'b1;
                  sts_err_code <= ERR_XRAY_TIMEOUT;
                end
              end
            end else begin
              if (cfg_mode != MODE_DARK_FRAME) begin
                xray_enable <= 1'b1;
              end
              timer_count <= timer_count + 32'd1;
              if (timer_count >= cfg_tinteg) begin
                timer_count <= '0;
                fsm_state <= ST_READOUT_INIT;
              end
            end
          end

          ST_READOUT_INIT: begin
            afe_config_req <= 1'b1;
            timer_count <= timer_count + 32'd1;
            if (afe_config_done || timer_count >= cfg_sync_dly) begin
              timer_count <= '0;
              row_index <= 12'd0;
              gate_row_index <= 12'd0;
              sts_line_idx <= 12'd0;
              fsm_state <= ST_SCAN_LINE;
            end
          end

          ST_SCAN_LINE: begin
            gate_start_scan <= (cfg_mode != MODE_DARK_FRAME);
            afe_start <= 1'b1;
            gate_row_index <= row_index;
            sts_line_idx <= row_index;
            if ((cfg_mode != MODE_DARK_FRAME && gate_row_done && afe_line_valid) ||
                (cfg_mode == MODE_DARK_FRAME && afe_line_valid)) begin
              if (row_index + 12'd1 >= cfg_nrows) begin
                fsm_state <= ST_READOUT_DONE;
              end else begin
                row_index <= row_index + 12'd1;
              end
            end
          end

          ST_READOUT_DONE: begin
            timer_count <= timer_count + 32'd1;
            if (timer_count >= cfg_tgate_settle) begin
              timer_count <= '0;
              fsm_state <= ST_DONE;
            end
          end

          ST_DONE: begin
            sts_busy <= 1'b0;
            sts_done <= 1'b1;
            if (cfg_mode == MODE_CONTINUOUS) begin
              fsm_state <= ST_RESET;
              sts_busy <= 1'b1;
            end else begin
              fsm_state <= ST_IDLE;
            end
          end

          default: begin
            sts_busy <= 1'b0;
            if (!ctrl_start) begin
              fsm_state <= ST_IDLE;
            end
          end
        endcase
      end
    end
  end

endmodule
