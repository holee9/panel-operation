// afe_afe2256.sv — AFE2256 (TI) controller
// Internal TG (timing generator), MCLK-based, CIC compensation
// 4-lane LVDS output, TP_SEL profile selection, pipeline mode
module afe_afe2256
  import fpd_params_pkg::*;
#(
    parameter N_AFE_CHIPS = 1  // Number of AFE chips in daisy chain
)(
    input  logic        clk,
    input  logic        rst_n,

    // Control (from panel_ctrl_fsm)
    input  logic        afe_start,
    input  logic        config_req,

    // Configuration (from reg_bank)
    input  logic [3:0]  cfg_ifs,          // Full-scale range select
    input  logic        cfg_cic_en,       // CIC enable
    input  logic [3:0]  cfg_cic_profile,  // CIC profile select
    input  logic        cfg_pipeline_en,  // Integrate-and-read pipeline
    input  logic        cfg_tp_sel,       // Timing profile (up/down)
    input  logic [3:0]  cfg_nchip,        // Number of AFE chips
    input  logic [15:0] cfg_tline,

    // AFE output pins
    output logic        afe_mclk,         // Master clock to AFE (32 MHz)
    output logic        afe_sync,         // Synchronization / TG start
    output logic        afe_tp_sel,       // Timing profile select
    output logic        afe_reset,        // Active high reset

    // AFE SPI configuration
    output logic        afe_spi_sck,
    output logic        afe_spi_sdi,
    input  logic        afe_spi_sdo,
    output logic        afe_spi_cs_n,

    // Data valid window
    output logic        dout_window_valid,
    output logic        fclk_expected,    // Frame clock expected

    // Status
    output logic        config_done,
    output logic        afe_ready
);

  logic [15:0] line_count;
  logic [7:0]  cfg_count;

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      afe_mclk <= 1'b0;
      afe_sync <= 1'b0;
      afe_tp_sel <= 1'b0;
      afe_reset <= 1'b1;
      afe_spi_sck <= 1'b0;
      afe_spi_sdi <= 1'b0;
      afe_spi_cs_n <= 1'b1;
      dout_window_valid <= 1'b0;
      fclk_expected <= 1'b0;
      config_done <= 1'b0;
      afe_ready <= 1'b0;
      line_count <= '0;
      cfg_count <= '0;
    end else begin
      config_done <= 1'b0;
      afe_sync <= 1'b0;
      afe_mclk <= ~afe_mclk;
      afe_tp_sel <= cfg_tp_sel;

      if (config_req && !afe_ready) begin
        afe_spi_cs_n <= 1'b0;
        afe_spi_sck <= ~afe_spi_sck;
        afe_spi_sdi <= cfg_ifs[0] ^ cfg_cic_en ^ cfg_cic_profile[0] ^ cfg_pipeline_en ^ cfg_nchip[0];
        cfg_count <= cfg_count + 8'd1;
        if (cfg_count >= 8'd15) begin
          afe_spi_cs_n <= 1'b1;
          afe_ready <= 1'b1;
          config_done <= 1'b1;
          afe_reset <= 1'b0;
          cfg_count <= '0;
        end
      end

      if (afe_start && afe_ready) begin
        afe_sync <= 1'b1;
        dout_window_valid <= 1'b1;
        fclk_expected <= 1'b1;
        line_count <= line_count + 16'd1;
        if (line_count + 16'd1 >= cfg_tline) begin
          dout_window_valid <= 1'b0;
          fclk_expected <= 1'b0;
          line_count <= '0;
        end
      end else if (!afe_start) begin
        dout_window_valid <= 1'b0;
        fclk_expected <= 1'b0;
        line_count <= '0;
      end
    end
  end

endmodule
