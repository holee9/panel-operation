// data_out_mux.sv — Line data to MCU transfer bus alignment
module data_out_mux
  import fpd_params_pkg::*;
(
    input  logic        clk,
    input  logic        rst_n,
    input  logic [11:0] cfg_ncols,

    // From line_buf_ram
    input  logic [PIXEL_WIDTH-1:0] line_pixel_data,
    input  logic        line_data_valid,
    input  logic [11:0] line_pixel_idx,

    // To mcu_data_if
    output logic [PIXEL_WIDTH-1:0] mcu_pixel_data,
    output logic        mcu_data_valid,
    output logic        mcu_line_start,
    output logic        mcu_line_end
);

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      mcu_pixel_data <= '0;
      mcu_data_valid <= 1'b0;
      mcu_line_start <= 1'b0;
      mcu_line_end <= 1'b0;
    end else begin
      mcu_pixel_data <= line_pixel_data;
      mcu_data_valid <= line_data_valid;
      mcu_line_start <= line_data_valid && (line_pixel_idx == 12'd0);
      mcu_line_end <= line_data_valid && (line_pixel_idx + 12'd1 >= cfg_ncols);
    end
  end

endmodule
