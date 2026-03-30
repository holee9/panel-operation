module detector_core
  import fpd_types_pkg::*;
  import fpd_params_pkg::*;
#(
    parameter bit USE_AFE2256 = 1'b0,
    parameter bit USE_NT_GATE = 1'b0,
    parameter bit FORCE_DEFAULT_COMBO = 1'b1,
    parameter logic [2:0] DEFAULT_COMBO = COMBO_C1,
    parameter int DEFAULT_NROWS = 2048,
    parameter int DEFAULT_NCOLS = 2048,
    parameter int DEFAULT_AFE_CHIPS = 1,
    parameter int DEFAULT_CSI_LANES = 2,
    parameter int DEFAULT_TLINE = 2200,
    parameter int DEFAULT_GATE_CLK_PERIOD = 2200,
    parameter int DEFAULT_NT_STV_PULSE = 100
)(
    input  logic        clk_100mhz,
    input  logic        rst_n,
    input  logic        spi_sclk,
    input  logic        spi_mosi,
    output logic        spi_miso,
    input  logic        spi_cs_n,
    output logic [PIXEL_WIDTH-1:0] mcu_data,
    output logic        mcu_data_rdy,
    input  logic        mcu_data_ack,
    output logic        irq_line_ready,
    output logic        irq_frame_done,
    output logic        nv_sd1,
    output logic        nv_sd2,
    output logic        nv_clk,
    output logic        nv_oe,
    output logic        nv_ona,
    output logic        nv_lr,
    output logic        nv_rst,
    output logic [1:0]  nv_md,
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
    output logic        afe_aclk,
    output logic        afe_mclk,
    output logic        afe_sync,
    output logic        afe_tp_sel,
    output logic        afe_reset,
    output logic        afe_spi_sck,
    output logic        afe_spi_sdi,
    input  logic        afe_spi_sdo,
    output logic        afe_spi_cs_n,
    input  logic [MAX_AFE_CHIPS-1:0] afe_dout_a,
    input  logic [MAX_AFE_CHIPS-1:0] afe_dout_b,
    input  logic [MAX_AFE_CHIPS-1:0] afe_dclk,
    input  logic [MAX_AFE_CHIPS-1:0] afe_fclk,
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

  logic [REG_ADDR_WIDTH-1:0] reg_addr;
  logic [REG_DATA_WIDTH-1:0] reg_wdata;
  logic [REG_DATA_WIDTH-1:0] reg_rdata;
  logic reg_wr_en, reg_rd_en;

  logic [2:0] cfg_mode;
  logic [2:0] cfg_combo;
  logic [11:0] cfg_nrows, cfg_ncols;
  logic [15:0] cfg_tline, cfg_treset;
  logic [23:0] cfg_tinteg;
  logic [11:0] cfg_tgate_on;
  logic [7:0] cfg_tgate_settle;
  logic [5:0] cfg_afe_ifs;
  logic [3:0] cfg_afe_lpf;
  logic [1:0] cfg_afe_pmode;
  logic cfg_cic_en;
  logic [3:0] cfg_cic_profile;
  logic cfg_pipeline_en, cfg_tp_sel, cfg_scan_dir;
  logic [1:0] cfg_gate_sel;
  logic [3:0] cfg_afe_nchip;
  logic [7:0] cfg_sync_dly, cfg_nreset, cfg_irq_en;
  logic ctrl_start, ctrl_abort, ctrl_irq_global_en;
  logic sts_busy, sts_done, sts_error, sts_line_rdy;
  logic [11:0] sts_line_idx;
  logic [7:0] sts_err_code;
  fsm_state_t fsm_state_sig;

  logic clk_afe_sel, clk_aclk_sel, clk_mclk_sel, clk_sys_out, rst_sync_n, pll_locked;
  logic gate_start_scan, gate_reset_all, gate_row_done;
  logic [11:0] gate_row_index;
  logic afe_start, afe_config_req, afe_config_done, afe_line_valid;
  logic prot_error, prot_force_stop, err_timeout;
  logic [11:0] row_index;
  logic gate_on_pulse, gate_settle, scan_active, scan_done;
  logic dout_window_valid, afe_ready;
  logic fclk_expected;
  logic [PIXEL_WIDTH-1:0] rd_pixel_data;
  logic wr_bank_sel, rd_bank_sel, bank_swap, wr_line_done;
  logic [11:0] rd_addr;
  logic rd_en, frame_done_int;
  logic mcu_line_start, mcu_line_end, packet_valid, packet_last, lanes_last;
  logic [7:0] packet_byte;
  logic [7:0] lane0_byte, lane1_byte, lane2_byte, lane3_byte;
  logic [3:0] lane_valid;
  power_mode_t target_power_mode, current_power_mode;
  logic shutdown_req, shutdown_force_gate;
  logic [7:0] shutdown_code;
  logic [2:0] eff_combo;
  logic [11:0] eff_nrows, eff_ncols;
  logic [15:0] eff_tline;
  logic [3:0] eff_afe_nchip;
  logic [2:0] eff_lane_count;

  function automatic int afe_count(
    input logic [2:0] combo_id,
    input int fallback_count
  );
    begin
      case (combo_id)
        COMBO_C6,
        COMBO_C7: afe_count = 12;
        default: afe_count = fallback_count;
      endcase
    end
  endfunction

  localparam int ACTIVE_AFE_COUNT = FORCE_DEFAULT_COMBO ?
      afe_count(DEFAULT_COMBO, DEFAULT_AFE_CHIPS) : DEFAULT_AFE_CHIPS;

  localparam int LINE_RX_CHANNELS =
      ((DEFAULT_NCOLS + ACTIVE_AFE_COUNT - 1) / ACTIVE_AFE_COUNT);

  logic [ACTIVE_AFE_COUNT-1:0][PIXEL_WIDTH-1:0] rx_pixel_data;
  logic [ACTIVE_AFE_COUNT-1:0]                  rx_pixel_valid;
  logic [ACTIVE_AFE_COUNT-1:0][11:0]           rx_pixel_col_idx;
  logic [ACTIVE_AFE_COUNT-1:0][11:0]           linebuf_wr_addr;

  function automatic logic [11:0] combo_default_ncols(input logic [2:0] combo_id);
    begin
      case (combo_id)
        COMBO_C4,
        COMBO_C5: combo_default_ncols = 12'd1664;
        COMBO_C6,
        COMBO_C7: combo_default_ncols = 12'd3072;
        default: combo_default_ncols = 12'd2048;
      endcase
    end
  endfunction

  function automatic logic [15:0] combo_min_tline(input logic [2:0] combo_id);
    begin
      case (combo_id)
        COMBO_C2: combo_min_tline = 16'd6000;
        COMBO_C3: combo_min_tline = 16'd5120;
        default: combo_min_tline = 16'd2200;
      endcase
    end
  endfunction

  function automatic logic [2:0] lane_count_for_cfg(
    input logic [3:0] afe_chips,
    input logic [2:0] default_lanes
  );
    begin
      if (afe_chips >= 4) begin
        lane_count_for_cfg = 3'd4;
      end else if (afe_chips >= 2) begin
        lane_count_for_cfg = 3'd2;
      end else begin
        lane_count_for_cfg = default_lanes;
      end
    end
  endfunction

  always_comb begin
    eff_combo = FORCE_DEFAULT_COMBO ? DEFAULT_COMBO : cfg_combo;
    eff_nrows = FORCE_DEFAULT_COMBO ? DEFAULT_NROWS : cfg_nrows;
    eff_ncols = FORCE_DEFAULT_COMBO ? DEFAULT_NCOLS :
                ((cfg_ncols < combo_default_ncols(eff_combo)) ? combo_default_ncols(eff_combo) : cfg_ncols);
    eff_tline = FORCE_DEFAULT_COMBO ? DEFAULT_TLINE :
                ((cfg_tline < combo_min_tline(eff_combo)) ? combo_min_tline(eff_combo) : cfg_tline);
    eff_afe_nchip = FORCE_DEFAULT_COMBO ? DEFAULT_AFE_CHIPS :
                    ((cfg_afe_nchip == 4'd0) ? 4'd1 : cfg_afe_nchip);
    eff_lane_count = lane_count_for_cfg(eff_afe_nchip, DEFAULT_CSI_LANES);
  end

  spi_slave_if u_spi (
      .clk(clk_100mhz),
      .rst_n(rst_sync_n),
      .spi_sclk(spi_sclk),
      .spi_mosi(spi_mosi),
      .spi_miso(spi_miso),
      .spi_cs_n(spi_cs_n),
      .reg_addr(reg_addr),
      .reg_wdata(reg_wdata),
      .reg_rdata(reg_rdata),
      .reg_wr_en(reg_wr_en),
      .reg_rd_en(reg_rd_en)
  );

  reg_bank u_reg_bank (
      .clk(clk_100mhz),
      .rst_n(rst_sync_n),
      .reg_addr(reg_addr),
      .reg_wdata(reg_wdata),
      .reg_rdata(reg_rdata),
      .reg_wr_en(reg_wr_en),
      .reg_rd_en(reg_rd_en),
      .cfg_mode(cfg_mode),
      .cfg_combo(cfg_combo),
      .cfg_nrows(cfg_nrows),
      .cfg_ncols(cfg_ncols),
      .cfg_tline(cfg_tline),
      .cfg_treset(cfg_treset),
      .cfg_tinteg(cfg_tinteg),
      .cfg_tgate_on(cfg_tgate_on),
      .cfg_tgate_settle(cfg_tgate_settle),
      .cfg_afe_ifs(cfg_afe_ifs),
      .cfg_afe_lpf(cfg_afe_lpf),
      .cfg_afe_pmode(cfg_afe_pmode),
      .cfg_cic_en(cfg_cic_en),
      .cfg_cic_profile(cfg_cic_profile),
      .cfg_pipeline_en(cfg_pipeline_en),
      .cfg_tp_sel(cfg_tp_sel),
      .cfg_scan_dir(cfg_scan_dir),
      .cfg_gate_sel(cfg_gate_sel),
      .cfg_afe_nchip(cfg_afe_nchip),
      .cfg_sync_dly(cfg_sync_dly),
      .cfg_nreset(cfg_nreset),
      .cfg_irq_en(cfg_irq_en),
      .ctrl_start(ctrl_start),
      .ctrl_abort(ctrl_abort),
      .ctrl_irq_global_en(ctrl_irq_global_en),
      .sts_busy(sts_busy),
      .sts_done(sts_done),
      .sts_error(sts_error),
      .sts_line_rdy(sts_line_rdy),
      .sts_line_idx(sts_line_idx),
      .sts_err_code(sts_err_code)
  );

  clk_rst_mgr u_clk_rst (
      .clk_sys(clk_100mhz),
      .rst_ext_n(rst_n),
      .afe_type_sel(USE_AFE2256 ? AFE_AFE2256 : AFE_AD71124),
      .clk_afe(clk_afe_sel),
      .clk_aclk(clk_aclk_sel),
      .clk_mclk(clk_mclk_sel),
      .clk_sys_out(clk_sys_out),
      .rst_sync_n(rst_sync_n),
      .pll_locked(pll_locked)
  );

  panel_ctrl_fsm u_fsm (
      .clk(clk_sys_out),
      .rst_n(rst_sync_n),
      .ctrl_start(ctrl_start),
      .ctrl_abort(ctrl_abort),
      .cfg_mode(op_mode_t'(cfg_mode)),
      .cfg_treset(cfg_treset),
      .cfg_tinteg(cfg_tinteg),
      .cfg_nrows(eff_nrows),
      .cfg_nreset(cfg_nreset),
      .cfg_sync_dly(cfg_sync_dly),
      .cfg_tgate_settle(cfg_tgate_settle),
      .radiography_mode(eff_combo == COMBO_C6 || eff_combo == COMBO_C7),
      .xray_prep_req(xray_prep_req),
      .xray_enable(xray_enable),
      .xray_on(xray_on),
      .xray_off(xray_off),
      .gate_start_scan(gate_start_scan),
      .gate_row_index(gate_row_index),
      .gate_reset_all(gate_reset_all),
      .gate_row_done(gate_row_done),
      .afe_start(afe_start),
      .afe_config_req(afe_config_req),
      .afe_config_done(afe_config_done),
      .afe_line_valid(afe_line_valid),
      .prot_error(prot_error),
      .prot_force_stop(prot_force_stop),
      .fsm_state(fsm_state_sig),
      .sts_busy(sts_busy),
      .sts_done(sts_done),
      .sts_error(sts_error),
      .sts_line_idx(sts_line_idx),
      .sts_err_code(sts_err_code)
  );

  row_scan_eng u_row_scan (
      .clk(clk_sys_out),
      .rst_n(rst_sync_n),
      .scan_start(gate_start_scan),
      .scan_abort(ctrl_abort || shutdown_force_gate),
      .scan_dir(cfg_scan_dir),
      .cfg_nrows(eff_nrows),
      .cfg_tgate_on(cfg_tgate_on),
      .cfg_tgate_settle(cfg_tgate_settle),
      .row_index(row_index),
      .gate_on_pulse(gate_on_pulse),
      .gate_settle(gate_settle),
      .scan_active(scan_active),
      .row_done(gate_row_done),
      .scan_done(scan_done)
  );

  generate
    if (!USE_NT_GATE) begin : gen_nv_gate
      gate_nv1047 u_gate_nv (
          .clk(clk_sys_out),
          .rst_n(rst_sync_n),
          .row_index(row_index),
          .gate_on_pulse(gate_on_pulse),
          .scan_dir(cfg_scan_dir),
          .reset_all(gate_reset_all || shutdown_force_gate),
          .cfg_clk_period(DEFAULT_GATE_CLK_PERIOD),
          .cfg_gate_on(cfg_tgate_on),
          .cfg_gate_settle(cfg_tgate_settle),
          .cfg_mode(cfg_gate_sel),
          .nv_sd1(nv_sd1),
          .nv_sd2(nv_sd2),
          .nv_clk(nv_clk),
          .nv_oe(nv_oe),
          .nv_ona(nv_ona),
          .nv_lr(nv_lr),
          .nv_rst(nv_rst),
          .nv_md(nv_md),
          .row_done()
      );
      assign nt_stv1l = 1'b0;
      assign nt_stv2l = 1'b0;
      assign nt_stv1r = 1'b0;
      assign nt_stv2r = 1'b0;
      assign nt_cpv_l = 1'b0;
      assign nt_cpv_r = 1'b0;
      assign nt_lr = 1'b0;
      assign nt_oe1_l = 1'b0;
      assign nt_oe1_r = 1'b0;
      assign nt_oe2_l = 1'b0;
      assign nt_oe2_r = 1'b0;
    end else begin : gen_nt_gate
      gate_nt39565d u_gate_nt (
          .clk(clk_sys_out),
          .rst_n(rst_sync_n),
          .row_index(row_index),
          .gate_on_pulse(gate_on_pulse),
          .scan_dir(cfg_scan_dir),
          .chip_sel(cfg_gate_sel),
          .mode_sel(cfg_gate_sel),
          .cfg_cpv_period(DEFAULT_GATE_CLK_PERIOD),
          .cfg_stv_pulse(DEFAULT_NT_STV_PULSE),
          .cfg_gate_on(cfg_tgate_on),
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
          .cascade_stv_return(1'b1),
          .cascade_complete(),
          .row_done()
      );
      assign nv_sd1 = 1'b0;
      assign nv_sd2 = 1'b0;
      assign nv_clk = 1'b0;
      assign nv_oe = 1'b1;
      assign nv_ona = 1'b1;
      assign nv_lr = 1'b0;
      assign nv_rst = rst_sync_n;
      assign nv_md = 2'b00;
    end
  endgenerate

  generate
    if (!USE_AFE2256) begin : gen_adi_afe
      afe_ad711xx u_afe_adi (
          .clk(clk_sys_out),
          .rst_n(rst_sync_n),
          .afe_start(afe_start),
          .config_req(afe_config_req),
          .line_idx(sts_line_idx),
          .cfg_tline(eff_tline),
          .cfg_ifs(cfg_afe_ifs),
          .cfg_lpf(cfg_afe_lpf),
          .cfg_pmode(cfg_afe_pmode),
          .cfg_nchip(eff_afe_nchip),
          .afe_aclk(afe_aclk),
          .afe_reset(afe_reset),
          .afe_sync(afe_sync),
          .afe_spi_sck(afe_spi_sck),
          .afe_spi_sdi(afe_spi_sdi),
          .afe_spi_sdo(afe_spi_sdo),
          .afe_spi_cs_n(afe_spi_cs_n),
          .dout_window_valid(dout_window_valid),
          .config_done(afe_config_done),
          .afe_ready(afe_ready)
      );
      assign afe_mclk = 1'b0;
      assign afe_tp_sel = 1'b0;
      assign fclk_expected = 1'b0;
    end else begin : gen_ti_afe
      afe_afe2256 u_afe_ti (
          .clk(clk_sys_out),
          .rst_n(rst_sync_n),
          .afe_start(afe_start),
          .config_req(afe_config_req),
          .cfg_ifs(cfg_afe_ifs[3:0]),
          .cfg_cic_en(cfg_cic_en),
          .cfg_cic_profile(cfg_cic_profile),
          .cfg_pipeline_en(cfg_pipeline_en),
          .cfg_tp_sel(cfg_tp_sel),
          .cfg_nchip(eff_afe_nchip),
          .cfg_tline(eff_tline),
          .afe_mclk(afe_mclk),
          .afe_sync(afe_sync),
          .afe_tp_sel(afe_tp_sel),
          .afe_reset(afe_reset),
          .afe_spi_sck(afe_spi_sck),
          .afe_spi_sdi(afe_spi_sdi),
          .afe_spi_sdo(afe_spi_sdo),
          .afe_spi_cs_n(afe_spi_cs_n),
          .dout_window_valid(dout_window_valid),
          .fclk_expected(fclk_expected),
          .config_done(afe_config_done),
          .afe_ready(afe_ready)
      );
      assign afe_aclk = 1'b0;
    end
  endgenerate

  genvar afe_idx;
  generate
    for (afe_idx = 0; afe_idx < ACTIVE_AFE_COUNT; afe_idx++) begin : gen_afe_rx
      localparam logic [11:0] COL_OFFSET = afe_idx * LINE_RX_CHANNELS;

      line_data_rx #(
          .ADI_MODE(!USE_AFE2256),
          .N_CHANNELS(LINE_RX_CHANNELS)
      ) u_line_rx (
          .clk(clk_sys_out),
          .rst_n(rst_sync_n),
          .lvds_dclk(afe_dclk[afe_idx]),
          .lvds_dout_a(afe_dout_a[afe_idx]),
          .lvds_dout_b(afe_dout_b[afe_idx]),
          .lvds_fclk(afe_fclk[afe_idx]),
          .rx_enable(dout_window_valid),
          .bitslip_req(1'b0),
          .pixel_data(rx_pixel_data[afe_idx]),
          .pixel_valid(rx_pixel_valid[afe_idx]),
          .pixel_col_idx(rx_pixel_col_idx[afe_idx]),
          .line_complete()
      );

      assign linebuf_wr_addr[afe_idx] = rx_pixel_col_idx[afe_idx] + COL_OFFSET;
    end
  endgenerate

  always_ff @(posedge clk_sys_out or negedge rst_sync_n) begin
    if (!rst_sync_n) begin
      wr_bank_sel <= 1'b0;
      rd_bank_sel <= 1'b1;
      rd_addr <= '0;
      rd_en <= 1'b0;
      afe_line_valid <= 1'b0;
      sts_line_rdy <= 1'b0;
      frame_done_int <= 1'b0;
    end else begin
      afe_line_valid <= 1'b0;
      sts_line_rdy <= 1'b0;
      frame_done_int <= 1'b0;

      if (wr_line_done) begin
        wr_bank_sel <= ~wr_bank_sel;
        rd_bank_sel <= wr_bank_sel;
        rd_addr <= '0;
        rd_en <= 1'b1;
        afe_line_valid <= 1'b1;
        sts_line_rdy <= 1'b1;
        if (sts_line_idx + 12'd1 >= eff_nrows) begin
          frame_done_int <= 1'b1;
        end
      end else if (rd_en) begin
        if (rd_addr + 12'd1 >= eff_ncols) begin
          rd_en <= 1'b0;
          rd_addr <= '0;
        end else begin
          rd_addr <= rd_addr + 12'd1;
        end
      end
    end
  end

  line_buf_ram #(
      .N_COLS(MAX_COLS),
      .N_AFES(ACTIVE_AFE_COUNT)
  ) u_line_buf (
      .wr_clk(afe_dclk[0]),
      .rd_clk(clk_sys_out),
      .rst_n(rst_sync_n),
      .wr_data(rx_pixel_data),
      .wr_addr(linebuf_wr_addr),
      .wr_en(rx_pixel_valid),
      .wr_bank_sel(wr_bank_sel),
      .rd_data(rd_pixel_data),
      .rd_addr(rd_addr),
      .rd_en(rd_en),
      .rd_bank_sel(rd_bank_sel),
      .wr_line_done(wr_line_done),
      .bank_swap(bank_swap)
  );

  data_out_mux u_data_out_mux (
      .clk(clk_sys_out),
      .rst_n(rst_sync_n),
      .cfg_ncols(eff_ncols),
      .line_pixel_data(rd_pixel_data),
      .line_data_valid(rd_en),
      .line_pixel_idx(rd_addr),
      .mcu_pixel_data(),
      .mcu_data_valid(),
      .mcu_line_start(mcu_line_start),
      .mcu_line_end(mcu_line_end)
  );

  logic [PIXEL_WIDTH-1:0] mux_pixel_data;
  logic mux_data_valid;

  assign mux_pixel_data = rd_pixel_data;
  assign mux_data_valid = rd_en;

  mcu_data_if u_mcu_if (
      .clk(clk_sys_out),
      .rst_n(rst_sync_n),
      .pixel_data(mux_pixel_data),
      .data_valid(mux_data_valid),
      .line_start(mcu_line_start),
      .line_end(mcu_line_end),
      .frame_done(frame_done_int),
      .mcu_data(mcu_data),
      .mcu_data_rdy(mcu_data_rdy),
      .mcu_data_ack(mcu_data_ack),
      .irq_line_ready(irq_line_ready),
      .irq_frame_done(irq_frame_done)
  );

  csi2_packet_builder u_csi_pkt (
      .clk(clk_sys_out),
      .rst_n(rst_sync_n),
      .pixel_data(mux_pixel_data),
      .pixel_valid(mux_data_valid),
      .line_start(mcu_line_start),
      .line_end(mcu_line_end),
      .frame_start(sts_line_idx == 12'd0 && mcu_line_start),
      .frame_end(frame_done_int),
      .packet_byte(packet_byte),
      .packet_valid(packet_valid),
      .packet_last(packet_last),
      .word_count()
  );

  csi2_lane_dist u_csi_lane (
      .clk(clk_sys_out),
      .rst_n(rst_sync_n),
      .lane_count(eff_lane_count),
      .packet_byte(packet_byte),
      .packet_valid(packet_valid),
      .packet_last(packet_last),
      .lane0_byte(lane0_byte),
      .lane1_byte(lane1_byte),
      .lane2_byte(lane2_byte),
      .lane3_byte(lane3_byte),
      .lane_valid(lane_valid),
      .lanes_last(lanes_last)
  );

  prot_mon u_prot (
      .clk(clk_sys_out),
      .rst_n(rst_sync_n),
      .fsm_state(fsm_state_sig),
      .xray_active(xray_on),
      .cfg_max_exposure(cfg_tinteg),
      .radiography_mode(eff_combo == COMBO_C6 || eff_combo == COMBO_C7),
      .err_timeout(err_timeout),
      .err_flag(prot_error),
      .force_gate_off(prot_force_stop)
  );

  emergency_shutdown u_shutdown (
      .clk(clk_sys_out),
      .rst_n(rst_sync_n),
      .vgh_over(vgh_over),
      .vgh_under(vgh_under),
      .temp_over(temp_over),
      .pll_unlocked(~pll_locked),
      .hw_emergency_n(hw_emergency_n),
      .shutdown_req(shutdown_req),
      .force_gate_off(shutdown_force_gate),
      .shutdown_code(shutdown_code)
  );

  always_comb begin
    if (shutdown_req) begin
      target_power_mode = PWR_IDLE_L3;
    end else if (cfg_mode == MODE_DARK_FRAME) begin
      target_power_mode = PWR_CAL_DARK;
    end else begin
      target_power_mode = PWR_ACTIVE;
    end
  end

  power_sequencer u_power (
      .clk(clk_sys_out),
      .rst_n(rst_sync_n),
      .target_mode(target_power_mode),
      .current_mode(current_power_mode),
      .en_vgl(en_vgl),
      .en_vgh(en_vgh),
      .en_avdd1(en_avdd1),
      .en_avdd2(en_avdd2),
      .en_dvdd(en_dvdd),
      .vgl_stable(1'b1),
      .vgh_stable(~vgh_under),
      .power_good(),
      .seq_error()
  );

endmodule
