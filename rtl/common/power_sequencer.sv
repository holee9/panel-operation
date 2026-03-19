// power_sequencer.sv — Power mode management (M0-M5)
// VGL must stabilize BEFORE VGH (violation = Gate IC latch-up)
// Soft-start slew rate <= 5 V/ms
module power_sequencer
  import fpd_types_pkg::*;
(
    input  logic        clk,
    input  logic        rst_n,

    // Mode control (from reg_bank / FSM)
    input  power_mode_t target_mode,
    output power_mode_t current_mode,

    // Power rail enables
    output logic        en_vgl,      // Gate low voltage (enable first)
    output logic        en_vgh,      // Gate high voltage (enable after VGL stable)
    output logic        en_avdd1,    // AFE analog supply 1
    output logic        en_avdd2,    // AFE analog supply 2
    output logic        en_dvdd,     // Digital supply

    // Status
    input  logic        vgl_stable,
    input  logic        vgh_stable,
    output logic        power_good,
    output logic        seq_error
);

  // TODO: Implement power sequencing FSM with VGL-before-VGH rule

endmodule
