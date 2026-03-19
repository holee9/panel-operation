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

    // MCU parallel data bus
    output logic [PIXEL_WIDTH-1:0] mcu_data,
    output logic        mcu_data_rdy,
    input  logic        mcu_data_ack,

    // Interrupt to MCU
    output logic        irq_line_ready,
    output logic        irq_frame_done
);

  // TODO: Implement MCU data handshake / DMA interface

endmodule
