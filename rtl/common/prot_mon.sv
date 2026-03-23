// prot_mon.sv — Protection monitor: over-exposure timeout, error flags, forced gate-off
module prot_mon
  import fpd_types_pkg::*;
  import fpd_params_pkg::*;
(
    input  logic        clk,
    input  logic        rst_n,

    // From panel_ctrl_fsm
    input  fsm_state_t  fsm_state,
    input  logic        xray_active,

    // Timeout configuration (from reg_bank)
    input  logic [23:0] cfg_max_exposure,  // max integration time
    input  logic        radiography_mode,

    // Error outputs
    output logic        err_timeout,       // over-exposure timeout flag
    output logic        err_flag,          // general error flag
    output logic        force_gate_off     // force all gates OFF
);

  logic [23:0] exposure_count;
  logic [23:0] exposure_limit;
  localparam logic [23:0] XRAY_TIMEOUT_5S  = 24'd500000;
  localparam logic [23:0] XRAY_TIMEOUT_30S = 24'd3000000;

  always_comb begin
    exposure_limit = radiography_mode ? XRAY_TIMEOUT_30S : XRAY_TIMEOUT_5S;
    if ((cfg_max_exposure != 24'd0) && (cfg_max_exposure < exposure_limit)) begin
      exposure_limit = cfg_max_exposure;
    end
  end

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      exposure_count <= '0;
      err_timeout <= 1'b0;
      err_flag <= 1'b0;
      force_gate_off <= 1'b0;
    end else begin
      if (((fsm_state == ST_INTEGRATE) || (fsm_state == ST_XRAY_ENABLE)) && xray_active) begin
        exposure_count <= exposure_count + 24'd1;
        if ((exposure_count + 24'd1) >= exposure_limit) begin
          err_timeout <= 1'b1;
          err_flag <= 1'b1;
          force_gate_off <= 1'b1;
        end
      end else if (fsm_state == ST_IDLE) begin
        exposure_count <= '0;
        err_timeout <= 1'b0;
        err_flag <= 1'b0;
        force_gate_off <= 1'b0;
      end
    end
  end

endmodule
