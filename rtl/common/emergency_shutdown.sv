// emergency_shutdown.sv — Over-voltage, over-temperature, PLL failure detection
// Response time < 100 us (async combinational path for critical signals)
module emergency_shutdown (
    input  logic        clk,
    input  logic        rst_n,

    // Analog monitoring inputs
    input  logic        vgh_over,       // VGH > 38V
    input  logic        vgh_under,      // VGH < 15V
    input  logic        temp_over,      // Temperature > 45C
    input  logic        pll_unlocked,   // MMCM lock lost
    input  logic        hw_emergency_n, // Hardware emergency button (active low)

    // Shutdown outputs
    output logic        shutdown_req,   // Request power sequencer shutdown
    output logic        force_gate_off, // Immediate gate disable
    output logic [7:0]  shutdown_code   // Reason code
);

  always_comb begin
    shutdown_req = 1'b0;
    force_gate_off = 1'b0;
    shutdown_code = 8'h00;

    if (!hw_emergency_n) begin
      shutdown_req = 1'b1;
      force_gate_off = 1'b1;
      shutdown_code = 8'hEE;
    end else if (vgh_over) begin
      shutdown_req = 1'b1;
      force_gate_off = 1'b1;
      shutdown_code = 8'h01;
    end else if (temp_over) begin
      shutdown_req = 1'b1;
      force_gate_off = 1'b1;
      shutdown_code = 8'h02;
    end else if (pll_unlocked) begin
      shutdown_req = 1'b1;
      force_gate_off = 1'b1;
      shutdown_code = 8'h03;
    end else if (vgh_under) begin
      shutdown_req = 1'b1;
      force_gate_off = 1'b1;
      shutdown_code = 8'h04;
    end
  end

endmodule
