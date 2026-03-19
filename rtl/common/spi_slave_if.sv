// spi_slave_if.sv — MCU SPI slave interface for register R/W
// Mode 0/3, 1-10 MHz, 8-bit address + 16-bit data
module spi_slave_if
  import fpd_params_pkg::*;
(
    input  logic        clk,
    input  logic        rst_n,

    // SPI bus (from MCU master)
    input  logic        spi_sclk,
    input  logic        spi_mosi,
    output logic        spi_miso,
    input  logic        spi_cs_n,

    // Register interface (to reg_bank)
    output logic [REG_ADDR_WIDTH-1:0] reg_addr,
    output logic [REG_DATA_WIDTH-1:0] reg_wdata,
    input  logic [REG_DATA_WIDTH-1:0] reg_rdata,
    output logic        reg_wr_en,
    output logic        reg_rd_en
);

  // TODO: Implement SPI slave state machine

endmodule
