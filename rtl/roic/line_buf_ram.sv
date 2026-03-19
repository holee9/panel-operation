// line_buf_ram.sv — BRAM-based ping-pong line buffer
// Stores 1 complete row of pixel data, double-buffered for continuous readout
// Write from LVDS receivers, read by data_out_mux → MCU
module line_buf_ram
  import fpd_params_pkg::*;
#(
    parameter N_COLS  = 2048,           // Pixels per row (configurable per combination)
    parameter N_AFES  = 1              // Number of AFE sources writing simultaneously
)(
    input  logic        wr_clk,        // Write clock (may be DCLK domain)
    input  logic        rd_clk,        // Read clock (system clock)
    input  logic        rst_n,

    // Write port (from line_data_rx instances)
    input  logic [PIXEL_WIDTH-1:0] wr_data,
    input  logic [11:0] wr_addr,       // Column address (AFE offset + col index)
    input  logic        wr_en,
    input  logic        wr_bank_sel,   // Ping-pong bank select

    // Read port (to data_out_mux)
    output logic [PIXEL_WIDTH-1:0] rd_data,
    input  logic [11:0] rd_addr,
    input  logic        rd_en,
    input  logic        rd_bank_sel,

    // Status
    output logic        wr_line_done,  // Write bank complete (all cols written)
    output logic        bank_swap      // Bank swap event
);

  // TODO: Implement dual-port BRAM with ping-pong logic

endmodule
