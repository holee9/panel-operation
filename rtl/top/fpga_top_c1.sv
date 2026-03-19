// fpga_top_c1.sv — Top-level for combination C1: NV1047 + AD71124
// Pin mapping and module instantiation for R1717 panel (17x17")
// Target: xc7a35tfgg484-1 (Artix-7 35T)
module fpga_top_c1
  import fpd_types_pkg::*;
  import fpd_params_pkg::*;
(
    // System
    input  logic        clk_100mhz,
    input  logic        rst_n,

    // MCU SPI
    input  logic        spi_sclk,
    input  logic        spi_mosi,
    output logic        spi_miso,
    input  logic        spi_cs_n,

    // MCU data output
    output logic [PIXEL_WIDTH-1:0] mcu_data,
    output logic        mcu_data_rdy,
    input  logic        mcu_data_ack,
    output logic        irq_n,

    // NV1047 Gate IC pins
    output logic        nv_sd1,
    output logic        nv_sd2,
    output logic        nv_clk,
    output logic        nv_oe,
    output logic        nv_ona,
    output logic        nv_lr,
    output logic        nv_rst,
    output logic [1:0]  nv_md,

    // AD71124 AFE pins (per AFE chip — active LVDS)
    input  logic        afe_dout_a_p, afe_dout_a_n,  // LVDS data A
    input  logic        afe_dout_b_p, afe_dout_b_n,  // LVDS data B
    input  logic        afe_dclk_p,   afe_dclk_n,    // LVDS data clock
    output logic        afe_aclk,                     // AFE master clock
    output logic        afe_sync,                     // Synchronization
    output logic        afe_reset,                    // AFE reset
    output logic        afe_spi_sck,                  // AFE SPI clock
    output logic        afe_spi_sdi,                  // AFE SPI data in
    input  logic        afe_spi_sdo,                  // AFE SPI data out
    output logic        afe_spi_cs_n,                 // AFE SPI chip select

    // X-ray generator
    input  logic        xray_prep_req,
    output logic        xray_enable,
    input  logic        xray_on,
    input  logic        xray_off,

    // Power monitoring
    input  logic        vgh_over,
    input  logic        vgh_under,
    input  logic        temp_over,
    input  logic        hw_emergency_n,

    // Power rail enables
    output logic        en_vgl,
    output logic        en_vgh,
    output logic        en_avdd1,
    output logic        en_avdd2,
    output logic        en_dvdd
);

  // TODO: Instantiate and connect all v1 modules

endmodule
