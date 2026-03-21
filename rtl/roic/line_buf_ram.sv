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

  logic [PIXEL_WIDTH-1:0] mem [0:1][0:N_COLS-1];

  always_ff @(posedge wr_clk or negedge rst_n) begin
    if (!rst_n) begin
      wr_line_done <= 1'b0;
      bank_swap <= 1'b0;
    end else begin
      wr_line_done <= 1'b0;
      bank_swap <= 1'b0;
      if (wr_en) begin
        mem[wr_bank_sel][wr_addr] <= wr_data;
        if (wr_addr + 12'd1 >= N_COLS) begin
          wr_line_done <= 1'b1;
          bank_swap <= 1'b1;
        end
      end
    end
  end

  always_ff @(posedge rd_clk or negedge rst_n) begin
    if (!rst_n) begin
      rd_data <= '0;
    end else if (rd_en) begin
      rd_data <= mem[rd_bank_sel][rd_addr];
    end
  end

endmodule
