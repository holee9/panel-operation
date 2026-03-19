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
    output logic        cfg_scan_dir,    // REG_SCAN_DIR
    output logic [3:0]  cfg_afe_nchip,   // REG_AFE_NCHIP

    // Control inputs (active status from other modules)
    output logic        ctrl_start,      // REG_CTRL[0]
    output logic        ctrl_abort,      // REG_CTRL[1]
    input  logic        sts_busy,        // REG_STATUS[0]
    input  logic        sts_done,        // REG_STATUS[1]
    input  logic        sts_error,       // REG_STATUS[2]
    input  logic [11:0] sts_line_idx,    // REG_LINE_IDX
    input  logic [7:0]  sts_err_code     // REG_ERR_CODE
);

  // TODO: Implement register array with default values

endmodule
