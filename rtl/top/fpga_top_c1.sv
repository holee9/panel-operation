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

  logic irq_line_ready;
  logic irq_frame_done;
  logic afe_mclk_unused;
  logic afe_tp_sel_unused;
  logic nt_stv1l_unused, nt_stv2l_unused, nt_stv1r_unused, nt_stv2r_unused;
  logic nt_cpv_l_unused, nt_cpv_r_unused, nt_lr_unused;
  logic nt_oe1_l_unused, nt_oe1_r_unused, nt_oe2_l_unused, nt_oe2_r_unused;

  detector_core #(
      .USE_AFE2256(1'b0),
      .USE_NT_GATE(1'b0),
      .FORCE_DEFAULT_COMBO(1'b1),
      .DEFAULT_COMBO(COMBO_C1),
      .DEFAULT_NROWS(2048),
      .DEFAULT_NCOLS(2048),
      .DEFAULT_AFE_CHIPS(1),
      .DEFAULT_CSI_LANES(2),
      .DEFAULT_TLINE(2200),
      .DEFAULT_GATE_CLK_PERIOD(2200),
      .DEFAULT_NT_STV_PULSE(100)
  ) u_detector_core (
      .clk_100mhz(clk_100mhz),
      .rst_n(rst_n),
      .spi_sclk(spi_sclk),
      .spi_mosi(spi_mosi),
      .spi_miso(spi_miso),
      .spi_cs_n(spi_cs_n),
      .mcu_data(mcu_data),
      .mcu_data_rdy(mcu_data_rdy),
      .mcu_data_ack(mcu_data_ack),
      .irq_line_ready(irq_line_ready),
      .irq_frame_done(irq_frame_done),
      .nv_sd1(nv_sd1),
      .nv_sd2(nv_sd2),
      .nv_clk(nv_clk),
      .nv_oe(nv_oe),
      .nv_ona(nv_ona),
      .nv_lr(nv_lr),
      .nv_rst(nv_rst),
      .nv_md(nv_md),
      .nt_stv1l(nt_stv1l_unused),
      .nt_stv2l(nt_stv2l_unused),
      .nt_stv1r(nt_stv1r_unused),
      .nt_stv2r(nt_stv2r_unused),
      .nt_cpv_l(nt_cpv_l_unused),
      .nt_cpv_r(nt_cpv_r_unused),
      .nt_lr(nt_lr_unused),
      .nt_oe1_l(nt_oe1_l_unused),
      .nt_oe1_r(nt_oe1_r_unused),
      .nt_oe2_l(nt_oe2_l_unused),
      .nt_oe2_r(nt_oe2_r_unused),
      .afe_aclk(afe_aclk),
      .afe_mclk(afe_mclk_unused),
      .afe_sync(afe_sync),
      .afe_tp_sel(afe_tp_sel_unused),
      .afe_reset(afe_reset),
      .afe_spi_sck(afe_spi_sck),
      .afe_spi_sdi(afe_spi_sdi),
      .afe_spi_sdo(afe_spi_sdo),
      .afe_spi_cs_n(afe_spi_cs_n),
      .afe_dout_a(afe_dout_a_p),
      .afe_dout_b(afe_dout_b_p),
      .afe_dclk(afe_dclk_p),
      .afe_fclk(1'b0),
      .xray_prep_req(xray_prep_req),
      .xray_enable(xray_enable),
      .xray_on(xray_on),
      .xray_off(xray_off),
      .vgh_over(vgh_over),
      .vgh_under(vgh_under),
      .temp_over(temp_over),
      .hw_emergency_n(hw_emergency_n),
      .en_vgl(en_vgl),
      .en_vgh(en_vgh),
      .en_avdd1(en_avdd1),
      .en_avdd2(en_avdd2),
      .en_dvdd(en_dvdd)
  );

  assign irq_n = ~(irq_line_ready | irq_frame_done);

endmodule
