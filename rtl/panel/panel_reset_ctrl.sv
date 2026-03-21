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

  logic [15:0] hold_count;
  logic [7:0]  scan_target;
  logic [7:0]  scan_count;

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      hold_count <= '0;
      scan_target <= 8'd0;
      scan_count <= 8'd0;
      dummy_scan_req <= 1'b0;
      reset_busy <= 1'b0;
      reset_done <= 1'b0;
    end else begin
      reset_done <= 1'b0;

      if (!reset_busy) begin
        dummy_scan_req <= 1'b0;
        hold_count <= '0;
        scan_count <= '0;
        if (reset_start) begin
          reset_busy <= 1'b1;
          scan_target <= (cfg_dummy_scans == 4'd0) ? 8'd1 : {4'h0, cfg_dummy_scans};
        end
      end else if (hold_count < cfg_treset) begin
        hold_count <= hold_count + 16'd1;
      end else begin
        dummy_scan_req <= 1'b1;
        if (dummy_scan_done) begin
          scan_count <= scan_count + 8'd1;
          if ((scan_count + 8'd1) >= scan_target) begin
            dummy_scan_req <= 1'b0;
            reset_busy <= 1'b0;
            reset_done <= 1'b1;
          end
        end
      end
    end
  end

endmodule
