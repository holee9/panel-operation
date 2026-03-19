// line_data_rx.sv — LVDS data receiver and deserializer
// Per-AFE instance: IBUFDS → ISERDESE2 → bit alignment → pixel output
// Supports ADI mode (DOUT A/B, self-clocked DCLK) and TI mode (4-lane, FCLK)
// 3 LVDS diff pairs per AFE = 6 pins (DOUT x2 + DCLK)
module line_data_rx
  import fpd_params_pkg::*;
#(
    parameter ADI_MODE  = 1,        // 1=AD71124/AD71143, 0=AFE2256
    parameter N_CHANNELS = 256      // Channels per AFE
)(
    input  logic        clk,
    input  logic        rst_n,

    // LVDS inputs (from AFE chip, post-IBUFDS)
    input  logic        lvds_dclk,       // Deserialized data clock
    input  logic        lvds_dout_a,     // Data output A (or lane 0)
    input  logic        lvds_dout_b,     // Data output B (or lane 1)

    // Control
    input  logic        rx_enable,       // Enable reception window
    input  logic        bitslip_req,     // Bitslip alignment request

    // Pixel output (to line_buf_ram)
    output logic [PIXEL_WIDTH-1:0] pixel_data,
    output logic        pixel_valid,
    output logic [11:0] pixel_col_idx,   // Column index within this AFE
    output logic        line_complete    // All channels received for this row
);

  // TODO: Implement ISERDESE2 + shift register + frame alignment

endmodule
