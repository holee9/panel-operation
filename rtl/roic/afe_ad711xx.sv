// afe_ad711xx.sv — AD71124/AD71143 AFE controller
// Shared module with generic parameters for IFS bit-width difference
// AD71124: tLINE_min=22us, IFS 6-bit (0-63)
// AD71143: tLINE_min=60us, IFS 5-bit (0-31)
module afe_ad711xx
  import fpd_params_pkg::*;
#(
    parameter IFS_WIDTH   = 6,       // 6 for AD71124, 5 for AD71143
    parameter TLINE_MIN   = 2200,    // 22us for AD71124, 6000 for AD71143 (10ns units)
    parameter N_AFE_CHIPS = 1        // Number of AFE chips in daisy chain
)(
    input  logic        clk,
    input  logic        rst_n,

    // Control (from panel_ctrl_fsm)
    input  logic        afe_start,
    input  logic        config_req,
    input  logic [11:0] line_idx,

    // Configuration (from reg_bank)
    input  logic [15:0] cfg_tline,
    input  logic [IFS_WIDTH-1:0] cfg_ifs,  // Full-scale charge range
    input  logic [3:0]  cfg_lpf,           // LPF time constant
    input  logic [1:0]  cfg_pmode,         // Power mode
    input  logic [3:0]  cfg_nchip,         // Number of AFE chips

    // AFE output pins
    output logic        afe_aclk,          // Master clock to AFE
    output logic        afe_reset,         // Active high reset
    output logic        afe_sync,          // Synchronization pulse

    // AFE SPI configuration
    output logic        afe_spi_sck,
    output logic        afe_spi_sdi,
    input  logic        afe_spi_sdo,
    output logic        afe_spi_cs_n,

    // Data valid window
    output logic        dout_window_valid, // LVDS data expected in this window

    // Status
    output logic        config_done,
    output logic        afe_ready
);

  logic [15:0] line_count;
  logic [7:0]  cfg_count;
  logic [2:0]  clk_div;
  logic [23:0] cfg_shift;

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      afe_aclk <= 1'b0;
      afe_reset <= 1'b1;
      afe_sync <= 1'b0;
      afe_spi_sck <= 1'b0;
      afe_spi_sdi <= 1'b0;
      afe_spi_cs_n <= 1'b1;
      dout_window_valid <= 1'b0;
      config_done <= 1'b0;
      afe_ready <= 1'b0;
      line_count <= '0;
      cfg_count <= '0;
      clk_div <= '0;
      cfg_shift <= '0;
    end else begin
      config_done <= 1'b0;
      afe_sync <= 1'b0;
      clk_div <= clk_div + 3'd1;
      if (clk_div == 3'd4) begin
        afe_aclk <= ~afe_aclk;
        clk_div <= '0;
      end

      if (config_req && !afe_ready) begin
        if (cfg_count == 8'd0) begin
          cfg_shift <= {cfg_ifs, cfg_lpf, cfg_pmode, cfg_nchip, 8'hA5};
        end
        afe_spi_cs_n <= 1'b0;
        afe_spi_sck <= ~afe_spi_sck;
        if (!afe_spi_sck) begin
          afe_spi_sdi <= cfg_shift[23];
          cfg_shift <= {cfg_shift[22:0], line_idx[0]};
          cfg_count <= cfg_count + 8'd1;
        end
        if (cfg_count >= 8'd23) begin
          afe_spi_cs_n <= 1'b1;
          afe_ready <= 1'b1;
          config_done <= 1'b1;
          cfg_count <= '0;
          afe_reset <= 1'b0;
        end
      end

      if (afe_start && afe_ready) begin
        afe_sync <= 1'b1;
        dout_window_valid <= 1'b1;
        line_count <= line_count + 16'd1;
        if (line_count + 16'd1 >= ((cfg_tline < TLINE_MIN) ? TLINE_MIN : cfg_tline)) begin
          dout_window_valid <= 1'b0;
          line_count <= '0;
        end
      end else if (!afe_start) begin
        dout_window_valid <= 1'b0;
        line_count <= '0;
      end
    end
  end

endmodule
