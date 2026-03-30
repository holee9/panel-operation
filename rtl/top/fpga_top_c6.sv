// fpga_top_c6.sv - Top-level for combination C6: NT39565D + AD71124
module fpga_top_c6
  import fpd_types_pkg::*;
  import fpd_params_pkg::*;
(
    input  logic        clk_100mhz,
    input  logic        rst_n,
    input  logic        spi_sclk,
    input  logic        spi_mosi,
    output logic        spi_miso,
    input  logic        spi_cs_n,
    output logic [PIXEL_WIDTH-1:0] mcu_data,
    output logic        mcu_data_rdy,
    input  logic        mcu_data_ack,
    output logic        irq_n,
    output logic        nt_stv1l,
    output logic        nt_stv2l,
    output logic        nt_stv1r,
    output logic        nt_stv2r,
    output logic        nt_cpv_l,
    output logic        nt_cpv_r,
    output logic        nt_lr,
    output logic        nt_oe1_l,
    output logic        nt_oe1_r,
    output logic        nt_oe2_l,
    output logic        nt_oe2_r,
    input  logic [11:0] afe_dout_a_p,
    input  logic [11:0] afe_dout_a_n,
    input  logic [11:0] afe_dout_b_p,
    input  logic [11:0] afe_dout_b_n,
    input  logic [11:0] afe_dclk_p,
    input  logic [11:0] afe_dclk_n,
    output logic        afe_aclk,
    output logic        afe_sync,
    output logic        afe_reset,
    output logic        afe_spi_sck,
    output logic        afe_spi_sdi,
    input  logic        afe_spi_sdo,
    output logic        afe_spi_cs_n,
    input  logic        xray_prep_req,
    output logic        xray_enable,
    input  logic        xray_on,
    input  logic        xray_off,
    input  logic        vgh_over,
    input  logic        vgh_under,
    input  logic        temp_over,
    input  logic        hw_emergency_n,
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
  logic [MAX_AFE_CHIPS-1:0] afe_dout_a_bus;
  logic [MAX_AFE_CHIPS-1:0] afe_dout_b_bus;
  logic [MAX_AFE_CHIPS-1:0] afe_dclk_bus;
  logic [MAX_AFE_CHIPS-1:0] afe_fclk_bus;
  logic nv_sd1_unused, nv_sd2_unused, nv_clk_unused, nv_oe_unused;
  logic nv_ona_unused, nv_lr_unused, nv_rst_unused;
  logic [1:0] nv_md_unused;

  assign afe_dout_a_bus = {{(MAX_AFE_CHIPS-12){1'b0}}, afe_dout_a_p};
  assign afe_dout_b_bus = {{(MAX_AFE_CHIPS-12){1'b0}}, afe_dout_b_p};
  assign afe_dclk_bus = {{(MAX_AFE_CHIPS-12){1'b0}}, afe_dclk_p};
  assign afe_fclk_bus = '0;

  detector_core #(
      .USE_AFE2256(1'b0),
      .USE_NT_GATE(1'b1),
      .FORCE_DEFAULT_COMBO(1'b1),
      .DEFAULT_COMBO(COMBO_C6),
      .DEFAULT_NROWS(3072),
      .DEFAULT_NCOLS(3072),
      .DEFAULT_AFE_CHIPS(12),
      .DEFAULT_CSI_LANES(4),
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
      .nv_sd1(nv_sd1_unused),
      .nv_sd2(nv_sd2_unused),
      .nv_clk(nv_clk_unused),
      .nv_oe(nv_oe_unused),
      .nv_ona(nv_ona_unused),
      .nv_lr(nv_lr_unused),
      .nv_rst(nv_rst_unused),
      .nv_md(nv_md_unused),
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
      .afe_mclk(afe_mclk_unused),
      .afe_sync(afe_sync),
      .afe_tp_sel(afe_tp_sel_unused),
      .afe_reset(afe_reset),
      .afe_spi_sck(afe_spi_sck),
      .afe_spi_sdi(afe_spi_sdi),
      .afe_spi_sdo(afe_spi_sdo),
      .afe_spi_cs_n(afe_spi_cs_n),
      .afe_dout_a(afe_dout_a_bus),
      .afe_dout_b(afe_dout_b_bus),
      .afe_dclk(afe_dclk_bus),
      .afe_fclk(afe_fclk_bus),
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
