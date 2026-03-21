module csi2_packet_builder
  import fpd_params_pkg::*;
(
    input  logic       clk,
    input  logic       rst_n,
    input  logic [15:0] pixel_data,
    input  logic       pixel_valid,
    input  logic       line_start,
    input  logic       line_end,
    input  logic       frame_start,
    input  logic       frame_end,
    output logic [7:0] packet_byte,
    output logic       packet_valid,
    output logic       packet_last,
    output logic [15:0] word_count
);

  typedef enum logic [1:0] {
    PKT_IDLE,
    PKT_HEADER,
    PKT_PAYLOAD,
    PKT_TRAILER
  } pkt_state_t;

  pkt_state_t pkt_state;
  logic [2:0] header_idx;
  logic [15:0] word_count_next;

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      packet_byte <= 8'h00;
      packet_valid <= 1'b0;
      packet_last <= 1'b0;
      word_count <= 16'd0;
      word_count_next <= 16'd0;
      header_idx <= 3'd0;
      pkt_state <= PKT_IDLE;
    end else begin
      packet_valid <= 1'b0;
      packet_last <= 1'b0;

      case (pkt_state)
        PKT_IDLE: begin
          if (frame_start || line_start) begin
            pkt_state <= PKT_HEADER;
            header_idx <= 3'd0;
            word_count_next <= 16'd0;
          end
        end

        PKT_HEADER: begin
          packet_valid <= 1'b1;
          case (header_idx)
            3'd0: packet_byte <= 8'h2E;
            3'd1: packet_byte <= word_count_next[7:0];
            3'd2: packet_byte <= word_count_next[15:8];
            default: packet_byte <= 8'h00;
          endcase

          if (header_idx == 3'd3) begin
            pkt_state <= PKT_PAYLOAD;
          end else begin
            header_idx <= header_idx + 3'd1;
          end
        end

        PKT_PAYLOAD: begin
          if (pixel_valid) begin
            packet_valid <= 1'b1;
            packet_byte <= pixel_data[15:8];
            word_count <= word_count + 16'd2;
            word_count_next <= word_count + 16'd2;
            pkt_state <= PKT_TRAILER;
          end else if (line_end || frame_end) begin
            pkt_state <= PKT_TRAILER;
          end
        end

        default: begin
          packet_valid <= pixel_valid;
          packet_byte <= pixel_data[7:0];
          if (line_end || frame_end) begin
            packet_last <= 1'b1;
            pkt_state <= PKT_IDLE;
          end else begin
            pkt_state <= PKT_PAYLOAD;
          end
        end
      endcase
    end
  end

endmodule
