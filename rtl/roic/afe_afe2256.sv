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

  // TODO: Implement SPI init + CIC profile load + MCLK/SYNC/TP_SEL generation

endmodule
