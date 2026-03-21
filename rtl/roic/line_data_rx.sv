// line_data_rx.sv — LVDS data receiver and deserializer
// Per-AFE instance: IBUFDS → ISERDESE2 → bit alignment → pixel output
// Supports ADI mode (DOUT A/B, self-clocked DCLK) and TI mode (4-lane, FCLK)
// 3 LVDS diff pairs per AFE = 6 pins (DOUT x2 + DCLK)
module line_data_rx
  import fpd_params_pkg::*;
#(
    parameter ADI_MODE  = 1,        // 1=AD71124/AD71143, 0=AFE2256
    parameter N_CHANNELS = 256      // Channels per AFE
)(
    input  logic        clk,
    input  logic        rst_n,

    // LVDS inputs (from AFE chip, post-IBUFDS)
    input  logic        lvds_dclk,       // Deserialized data clock
    input  logic        lvds_dout_a,     // Data output A (or lane 0)
    input  logic        lvds_dout_b,     // Data output B (or lane 1)
    input  logic        lvds_fclk,       // TI AFE2256 frame clock lane

    // Control
    input  logic        rx_enable,       // Enable reception window
    input  logic        bitslip_req,     // Bitslip alignment request

    // Pixel output (to line_buf_ram)
    output logic [PIXEL_WIDTH-1:0] pixel_data,
    output logic        pixel_valid,
    output logic [11:0] pixel_col_idx,   // Column index within this AFE
    output logic        line_complete    // All channels received for this row
);

  logic [15:0] shift_reg;
  logic [3:0]  bit_count;

  always_ff @(posedge lvds_dclk or negedge rst_n) begin
    if (!rst_n) begin
      shift_reg <= '0;
      bit_count <= '0;
      pixel_data <= '0;
      pixel_valid <= 1'b0;
      pixel_col_idx <= '0;
      line_complete <= 1'b0;
    end else begin
      pixel_valid <= 1'b0;
      line_complete <= 1'b0;
      if (rx_enable) begin
        if (!ADI_MODE && lvds_fclk) begin
          bit_count <= '0;
          pixel_col_idx <= '0;
        end
        shift_reg <= {shift_reg[13:0], lvds_dout_a, lvds_dout_b};
        if (bitslip_req) begin
          shift_reg <= {shift_reg[14:0], shift_reg[15]};
        end
        if (bit_count == 4'd7) begin
          pixel_data <= {shift_reg[13:0], lvds_dout_a, lvds_dout_b};
          pixel_valid <= 1'b1;
          if (pixel_col_idx + 12'd1 >= N_CHANNELS) begin
            line_complete <= 1'b1;
            pixel_col_idx <= '0;
          end else begin
            pixel_col_idx <= pixel_col_idx + 12'd1;
          end
          bit_count <= '0;
        end else begin
          bit_count <= bit_count + 4'd1;
        end
      end else begin
        bit_count <= '0;
        pixel_col_idx <= '0;
      end
    end
  end

endmodule
