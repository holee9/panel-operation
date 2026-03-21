module fpga_top_c3
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

  always_comb begin
    spi_miso = 1'b0;
    mcu_data = '0;
    mcu_data_rdy = 1'b0;
  end

endmodule
