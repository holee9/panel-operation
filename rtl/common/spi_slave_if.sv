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
  logic spi_sclk_meta, spi_sclk_sync, spi_sclk_prev;
  logic spi_mosi_meta, spi_mosi_sync;
  logic spi_cs_n_meta, spi_cs_n_sync;

  logic [7:0]  addr_shift;
  logic [15:0] data_shift;
  logic [15:0] tx_shift;
  logic [4:0]  bit_count;
  logic        is_read;

  wire sclk_rise = (~spi_sclk_prev) & spi_sclk_sync;
  wire sclk_fall = spi_sclk_prev & (~spi_sclk_sync);

  always_ff @(posedge clk or negedge rst_n) begin
    logic [7:0] addr_byte;

    if (!rst_n) begin
      spi_sclk_meta <= 1'b0;
      spi_sclk_sync <= 1'b0;
      spi_sclk_prev <= 1'b0;
      spi_mosi_meta <= 1'b0;
      spi_mosi_sync <= 1'b0;
      spi_cs_n_meta <= 1'b1;
      spi_cs_n_sync <= 1'b1;
      spi_miso      <= 1'b0;
      reg_addr      <= '0;
      reg_wdata     <= '0;
      reg_wr_en     <= 1'b0;
      reg_rd_en     <= 1'b0;
      addr_shift    <= '0;
      data_shift    <= '0;
      tx_shift      <= '0;
      bit_count     <= '0;
      is_read       <= 1'b0;
    end else begin
      spi_sclk_meta <= spi_sclk;
      spi_sclk_sync <= spi_sclk_meta;
      spi_mosi_meta <= spi_mosi;
      spi_mosi_sync <= spi_mosi_meta;
      spi_cs_n_meta <= spi_cs_n;
      spi_cs_n_sync <= spi_cs_n_meta;

      reg_wr_en <= 1'b0;
      reg_rd_en <= 1'b0;

      if (spi_cs_n_sync) begin
        spi_miso   <= 1'b0;
        addr_shift <= '0;
        data_shift <= '0;
        tx_shift   <= '0;
        bit_count  <= '0;
        is_read    <= 1'b0;
      end else begin
        if (sclk_rise) begin
          if (bit_count < 5'd8) begin
            addr_byte  = {addr_shift[6:0], spi_mosi_sync};
            addr_shift <= addr_byte;
            bit_count  <= bit_count + 5'd1;

            if (bit_count == 5'd7) begin
              reg_addr <= addr_byte[REG_ADDR_WIDTH-1:0];
              is_read  <= ~addr_byte[7];
              if (!addr_byte[7]) begin
                reg_rd_en <= 1'b1;
                tx_shift  <= reg_rdata;
              end
            end
          end else if (!is_read && (bit_count < 5'd24)) begin
            reg_wdata  <= {data_shift[14:0], spi_mosi_sync};
            data_shift <= {data_shift[14:0], spi_mosi_sync};
            bit_count  <= bit_count + 5'd1;

            if (bit_count == 5'd23) begin
              reg_wr_en <= 1'b1;
            end
          end
        end

        if (sclk_fall && is_read && (bit_count >= 5'd8) && (bit_count < 5'd24)) begin
          spi_miso <= tx_shift[15];
          tx_shift <= {tx_shift[14:0], 1'b0};
          bit_count <= bit_count + 5'd1;
        end
      end

      spi_sclk_prev <= spi_sclk_sync;
    end
  end

endmodule
