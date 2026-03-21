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

  logic running;
  logic triggered_seen;

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      running <= 1'b0;
      triggered_seen <= 1'b0;
      xray_enable <= 1'b0;
      integ_active <= 1'b0;
      integ_done <= 1'b0;
      integ_count <= '0;
    end else begin
      integ_done <= 1'b0;

      if (!running) begin
        integ_active <= 1'b0;
        integ_count <= '0;
        triggered_seen <= 1'b0;
        xray_enable <= 1'b0;
        if (integ_start) begin
          running <= 1'b1;
          xray_enable <= triggered_mode;
          integ_active <= !triggered_mode;
        end
      end else begin
        if (triggered_mode && !triggered_seen) begin
          xray_enable <= 1'b1;
          if (xray_on || xray_prep_req) begin
            triggered_seen <= 1'b1;
            integ_active <= 1'b1;
          end
        end

        if (integ_active) begin
          integ_count <= integ_count + 24'd1;
          if ((integ_count + 24'd1) >= cfg_tinteg || xray_off) begin
            integ_active <= 1'b0;
            xray_enable <= 1'b0;
            integ_done <= 1'b1;
            running <= 1'b0;
          end
        end
      end
    end
  end

endmodule
