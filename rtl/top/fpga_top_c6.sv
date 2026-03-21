module fpga_top_c6
  import fpd_params_pkg::*;
(
    input  logic clk_100mhz,
    input  logic rst_n,
    input  logic spi_sclk,
    input  logic spi_mosi,
    output logic spi_miso,
    input  logic spi_cs_n,
    output logic [PIXEL_WIDTH-1:0] mcu_data,
    output logic mcu_data_rdy,
    input  logic mcu_data_ack
);

  logic irq_line_ready;
  logic irq_frame_done;
  logic nv_sd1, nv_sd2, nv_clk, nv_oe, nv_ona, nv_lr, nv_rst;
  logic [1:0] nv_md;
  logic nt_stv1l, nt_stv2l, nt_stv1r, nt_stv2r, nt_cpv_l, nt_cpv_r, nt_lr, nt_oe1_l, nt_oe1_r, nt_oe2_l, nt_oe2_r;
  logic afe_aclk, afe_mclk, afe_sync, afe_tp_sel, afe_reset, afe_spi_sck, afe_spi_sdi, afe_spi_cs_n;

  detector_core #(
      .USE_AFE2256(1'b0),
      .USE_NT_GATE(1'b1)
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
      .nt_stv1l(nt_stv1l),
      .nt_stv2l(nt_stv2l),
      .nt_stv1r(nt_stv1r),
      .nt_stv2r(nt_stv2r),
      .nt_cpv_l(nt_cpv_l),
      .nt_cpv_r(nt_cpv_r),
      .nt_lr(nt_lr),
      .nt_oe1_l(nt_oe1_l),
      .nt_oe1_r(nt_oe1_r),
      .nt_oe2_l(nt_oe2_l),
      .nt_oe2_r(nt_oe2_r),
      .afe_aclk(afe_aclk),
      .afe_mclk(afe_mclk),
      .afe_sync(afe_sync),
      .afe_tp_sel(afe_tp_sel),
      .afe_reset(afe_reset),
      .afe_spi_sck(afe_spi_sck),
      .afe_spi_sdi(afe_spi_sdi),
      .afe_spi_sdo(1'b0),
      .afe_spi_cs_n(afe_spi_cs_n),
      .afe_dout_a(1'b0),
      .afe_dout_b(1'b0),
      .afe_dclk(clk_100mhz),
      .afe_fclk(1'b0),
      .xray_prep_req(1'b0),
      .xray_enable(),
      .xray_on(1'b0),
      .xray_off(1'b0),
      .vgh_over(1'b0),
      .vgh_under(1'b0),
      .temp_over(1'b0),
      .hw_emergency_n(1'b1),
      .en_vgl(),
      .en_vgh(),
      .en_avdd1(),
      .en_avdd2(),
      .en_dvdd()
  );

endmodule
