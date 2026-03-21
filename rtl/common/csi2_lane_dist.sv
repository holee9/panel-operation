module csi2_lane_dist (
    input  logic       clk,
    input  logic       rst_n,
    input  logic [2:0] lane_count,
    input  logic [7:0] packet_byte,
    input  logic       packet_valid,
    input  logic       packet_last,
    output logic [7:0] lane0_byte,
    output logic [7:0] lane1_byte,
    output logic [7:0] lane2_byte,
    output logic [7:0] lane3_byte,
    output logic [3:0] lane_valid,
    output logic       lanes_last
);

  logic [1:0] lane_ptr;
  logic [2:0] lanes_active;

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      lane0_byte <= 8'h00;
      lane1_byte <= 8'h00;
      lane2_byte <= 8'h00;
      lane3_byte <= 8'h00;
      lane_valid <= 4'b0000;
      lanes_last <= 1'b0;
      lane_ptr <= 2'd0;
      lanes_active <= 3'd2;
    end else begin
      lane_valid <= 4'b0000;
      lanes_last <= 1'b0;
      lanes_active <= (lane_count < 3'd2) ? 3'd2 : lane_count;

      if (packet_valid) begin
        case (lane_ptr)
          2'd0: begin lane0_byte <= packet_byte; lane_valid[0] <= 1'b1; end
          2'd1: begin lane1_byte <= packet_byte; lane_valid[1] <= 1'b1; end
          2'd2: begin lane2_byte <= packet_byte; lane_valid[2] <= 1'b1; end
          default: begin lane3_byte <= packet_byte; lane_valid[3] <= 1'b1; end
        endcase

        if (packet_last) begin
          lanes_last <= 1'b1;
          lane_ptr <= 2'd0;
        end else if (lane_ptr + 2'd1 >= lanes_active[1:0]) begin
          lane_ptr <= 2'd0;
        end else begin
          lane_ptr <= lane_ptr + 2'd1;
        end
      end
    end
  end

endmodule
