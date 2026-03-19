// afe_spi_master.sv — SPI master for AFE configuration (daisy-chain capable)
// Shared by afe_ad711xx and afe_afe2256 controllers
// Supports daisy-chain: 24 AFE × 24-bit = 576 bits per write
module afe_spi_master #(
    parameter MAX_CHAIN_LEN = 24   // Maximum number of AFEs in daisy chain
)(
    input  logic        clk,
    input  logic        rst_n,

    // Command interface (from AFE controller)
    input  logic [7:0]  cmd_addr,       // Register address
    input  logic [15:0] cmd_wdata,      // Write data
    input  logic        cmd_start,      // Start transaction
    input  logic [4:0]  cmd_chain_len,  // Number of chips in chain (1-24)
    output logic        cmd_done,

    // SPI bus (to AFE chips)
    output logic        spi_sck,
    output logic        spi_sdi,
    input  logic        spi_sdo,
    output logic        spi_cs_n
);

  // TODO: Implement SPI master with daisy-chain shift

endmodule
