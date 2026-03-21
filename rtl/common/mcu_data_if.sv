// mcu_data_if.sv — MCU data transfer interface (parallel or SPI)
module mcu_data_if
  import fpd_params_pkg::*;
(
    input  logic        clk,
    input  logic        rst_n,

    // From data_out_mux
    input  logic [PIXEL_WIDTH-1:0] pixel_data,
    input  logic        data_valid,
    input  logic        line_start,
    input  logic        line_end,
    input  logic        frame_done,

    // MCU parallel data bus
    output logic [PIXEL_WIDTH-1:0] mcu_data,
    output logic        mcu_data_rdy,
    input  logic        mcu_data_ack,

    // Interrupt to MCU
    output logic        irq_line_ready,
    output logic        irq_frame_done
);

  logic pending_valid;

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      mcu_data <= '0;
      mcu_data_rdy <= 1'b0;
      irq_line_ready <= 1'b0;
      irq_frame_done <= 1'b0;
      pending_valid <= 1'b0;
    end else begin
      irq_line_ready <= 1'b0;
      irq_frame_done <= 1'b0;

      if (data_valid && !pending_valid) begin
        mcu_data <= pixel_data;
        mcu_data_rdy <= 1'b1;
        pending_valid <= 1'b1;
      end else if (pending_valid && mcu_data_ack) begin
        mcu_data_rdy <= 1'b0;
        pending_valid <= 1'b0;
      end

      if (line_end) begin
        irq_line_ready <= 1'b1;
      end

      if (frame_done) begin
        irq_frame_done <= 1'b1;
      end
    end
  end

endmodule
