// reg_bank.sv — 32-register file (0x00-0x1F), MCU R/W access
// All timing parameters are runtime-configurable (no hardcoding)
module reg_bank
  import fpd_types_pkg::*;
  import fpd_params_pkg::*;
(
    input  logic        clk,
    input  logic        rst_n,

    // SPI slave interface
    input  logic [REG_ADDR_WIDTH-1:0] reg_addr,
    input  logic [REG_DATA_WIDTH-1:0] reg_wdata,
    output logic [REG_DATA_WIDTH-1:0] reg_rdata,
    input  logic        reg_wr_en,
    input  logic        reg_rd_en,

    // Configuration outputs (active values to other modules)
    output logic [2:0]  cfg_mode,        // REG_MODE[2:0]
    output logic [2:0]  cfg_combo,       // REG_COMBO[2:0]
    output logic [11:0] cfg_nrows,       // REG_NROWS
    output logic [11:0] cfg_ncols,       // REG_NCOLS
    output logic [15:0] cfg_tline,       // REG_TLINE
    output logic [15:0] cfg_treset,      // REG_TRESET
    output logic [23:0] cfg_tinteg,      // REG_TINTEG
    output logic [11:0] cfg_tgate_on,    // REG_TGATE_ON
    output logic [7:0]  cfg_tgate_settle,// REG_TGATE_SETTLE
    output logic [5:0]  cfg_afe_ifs,     // REG_AFE_IFS
    output logic [3:0]  cfg_afe_lpf,     // REG_AFE_LPF
    output logic [1:0]  cfg_afe_pmode,   // REG_AFE_PMODE
    output logic        cfg_cic_en,      // REG_CIC_EN
    output logic [3:0]  cfg_cic_profile, // REG_CIC_PROFILE[3:0]
    output logic        cfg_pipeline_en, // REG_CIC_PROFILE[4]
    output logic        cfg_tp_sel,      // REG_CIC_PROFILE[5]
    output logic        cfg_scan_dir,    // REG_SCAN_DIR
    output logic [1:0]  cfg_gate_sel,    // REG_GATE_SEL
    output logic [3:0]  cfg_afe_nchip,   // REG_AFE_NCHIP
    output logic [7:0]  cfg_sync_dly,    // REG_SYNC_DLY
    output logic [7:0]  cfg_nreset,      // REG_NRESET
    output logic [7:0]  cfg_irq_en,      // REG_CTRL[10:3]

    // Control inputs (active status from other modules)
    output logic        ctrl_start,      // REG_CTRL[0]
    output logic        ctrl_abort,      // REG_CTRL[1]
    output logic        ctrl_irq_global_en, // REG_CTRL[2]
    input  logic        sts_busy,        // REG_STATUS[0]
    input  logic        sts_done,        // REG_STATUS[1]
    input  logic        sts_error,       // REG_STATUS[2]
    input  logic        sts_line_rdy,    // REG_STATUS[3]
    input  logic [11:0] sts_line_idx,    // REG_LINE_IDX
    input  logic [7:0]  sts_err_code     // REG_ERR_CODE
);
  localparam logic [REG_ADDR_WIDTH-1:0] REG_CTRL_ADDR         = 5'h00;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_STATUS_ADDR       = 5'h01;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_MODE_ADDR         = 5'h02;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_COMBO_ADDR        = 5'h03;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_NROWS_ADDR        = 5'h04;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_NCOLS_ADDR        = 5'h05;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_TLINE_ADDR        = 5'h06;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_TRESET_ADDR       = 5'h07;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_TINTEG_ADDR       = 5'h08;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_TGATE_ON_ADDR     = 5'h09;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_TGATE_SETTLE_ADDR = 5'h0A;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_AFE_IFS_ADDR      = 5'h0B;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_AFE_LPF_ADDR      = 5'h0C;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_AFE_PMODE_ADDR    = 5'h0D;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_CIC_EN_ADDR       = 5'h0E;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_CIC_PROFILE_ADDR  = 5'h0F;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_SCAN_DIR_ADDR     = 5'h10;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_GATE_SEL_ADDR     = 5'h11;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_AFE_NCHIP_ADDR    = 5'h12;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_SYNC_DLY_ADDR     = 5'h13;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_LINE_IDX_ADDR     = 5'h14;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_ERR_CODE_ADDR     = 5'h15;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_NRESET_ADDR       = 5'h16;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_TINTEG_H_ADDR     = 5'h17;
  localparam logic [REG_ADDR_WIDTH-1:0] REG_VERSION_ADDR      = 5'h1F;

  logic [REG_DATA_WIDTH-1:0] regs [0:(1 << REG_ADDR_WIDTH)-1];
  integer reg_idx;

  function automatic logic is_read_only(input logic [REG_ADDR_WIDTH-1:0] addr);
    begin
      is_read_only =
        (addr == REG_STATUS_ADDR)   ||
        (addr == REG_LINE_IDX_ADDR) ||
        (addr == REG_ERR_CODE_ADDR) ||
        (addr == REG_VERSION_ADDR);
    end
  endfunction

  function automatic logic [REG_DATA_WIDTH-1:0] read_data(
    input logic [REG_ADDR_WIDTH-1:0] addr
  );
    begin
      case (addr)
        REG_STATUS_ADDR: read_data = {
          12'h000,
          sts_line_rdy,
          sts_error,
          sts_done,
          sts_busy
        };
        REG_LINE_IDX_ADDR: read_data = {4'h0, sts_line_idx};
        REG_ERR_CODE_ADDR: read_data = {8'h00, sts_err_code};
        default: read_data = regs[addr];
      endcase
    end
  endfunction

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      for (reg_idx = 0; reg_idx < (1 << REG_ADDR_WIDTH); reg_idx = reg_idx + 1) begin
        regs[reg_idx] <= '0;
      end

      regs[REG_COMBO_ADDR]        <= 16'h0001;
      regs[REG_NROWS_ADDR]        <= 16'd2048;
      regs[REG_NCOLS_ADDR]        <= 16'd2048;
      regs[REG_TLINE_ADDR]        <= 16'd2200;
      regs[REG_TRESET_ADDR]       <= 16'd100;
      regs[REG_TINTEG_ADDR]       <= 16'd1000;
      regs[REG_TGATE_ON_ADDR]     <= T_GATE_ON_DEFAULT;
      regs[REG_TGATE_SETTLE_ADDR] <= {8'h00, T_GATE_SETTLE_DEFAULT};
      regs[REG_AFE_LPF_ADDR]      <= 16'h0000;
      regs[REG_AFE_PMODE_ADDR]    <= 16'h0000;
      regs[REG_CIC_EN_ADDR]       <= 16'h0000;
      regs[REG_CIC_PROFILE_ADDR]  <= 16'h0000;
      regs[REG_GATE_SEL_ADDR]     <= 16'h0000;
      regs[REG_AFE_NCHIP_ADDR]    <= 16'h0001;
      regs[REG_SYNC_DLY_ADDR]     <= 16'h0000;
      regs[REG_NRESET_ADDR]       <= 16'h0003;
      regs[REG_TINTEG_H_ADDR]     <= 16'h0000;
      regs[REG_VERSION_ADDR]      <= 16'h0010;
    end else begin
      regs[REG_CTRL_ADDR][1:0] <= 2'b00;

      if (reg_wr_en && !is_read_only(reg_addr)) begin
        regs[reg_addr] <= reg_wdata;
      end
    end
  end

  always_comb begin
    reg_rdata         = read_data(reg_addr);
    cfg_mode          = regs[REG_MODE_ADDR][2:0];
    cfg_combo         = regs[REG_COMBO_ADDR][2:0];
    cfg_nrows         = regs[REG_NROWS_ADDR][11:0];
    cfg_ncols         = regs[REG_NCOLS_ADDR][11:0];
    cfg_tline         = regs[REG_TLINE_ADDR];
    cfg_treset        = regs[REG_TRESET_ADDR];
    cfg_tinteg        = {regs[REG_TINTEG_H_ADDR][7:0], regs[REG_TINTEG_ADDR]};
    cfg_tgate_on      = regs[REG_TGATE_ON_ADDR][11:0];
    cfg_tgate_settle  = regs[REG_TGATE_SETTLE_ADDR][7:0];
    cfg_afe_ifs       = regs[REG_AFE_IFS_ADDR][5:0];
    cfg_afe_lpf       = regs[REG_AFE_LPF_ADDR][3:0];
    cfg_afe_pmode     = regs[REG_AFE_PMODE_ADDR][1:0];
    cfg_cic_en        = regs[REG_CIC_EN_ADDR][0];
    cfg_cic_profile   = regs[REG_CIC_PROFILE_ADDR][3:0];
    cfg_pipeline_en   = regs[REG_CIC_PROFILE_ADDR][4];
    cfg_tp_sel        = regs[REG_CIC_PROFILE_ADDR][5];
    cfg_scan_dir      = regs[REG_SCAN_DIR_ADDR][0];
    cfg_gate_sel      = regs[REG_GATE_SEL_ADDR][1:0];
    cfg_afe_nchip     = regs[REG_AFE_NCHIP_ADDR][3:0];
    cfg_sync_dly      = regs[REG_SYNC_DLY_ADDR][7:0];
    cfg_nreset        = regs[REG_NRESET_ADDR][7:0];
    cfg_irq_en        = regs[REG_CTRL_ADDR][10:3];
    ctrl_start        = regs[REG_CTRL_ADDR][0];
    ctrl_abort        = regs[REG_CTRL_ADDR][1];
    ctrl_irq_global_en = regs[REG_CTRL_ADDR][2];
  end

endmodule
