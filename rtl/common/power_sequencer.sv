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

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      current_mode <= PWR_POWER_UP;
      en_vgl <= 1'b0;
      en_vgh <= 1'b0;
      en_avdd1 <= 1'b0;
      en_avdd2 <= 1'b0;
      en_dvdd <= 1'b0;
      power_good <= 1'b0;
      seq_error <= 1'b0;
    end else begin
      seq_error <= en_vgh && !en_vgl;
      power_good <= 1'b0;

      case (target_mode)
        PWR_POWER_UP,
        PWR_ACTIVE,
        PWR_CAL_DARK: begin
          en_dvdd <= 1'b1;
          en_avdd1 <= 1'b1;
          en_avdd2 <= 1'b1;
          en_vgl <= 1'b1;
          if (vgl_stable) begin
            en_vgh <= 1'b1;
          end
          if (vgl_stable && vgh_stable) begin
            current_mode <= target_mode;
            power_good <= 1'b1;
          end
        end

        PWR_IDLE_L1: begin
          current_mode <= PWR_IDLE_L1;
          en_dvdd <= 1'b1;
          en_avdd1 <= 1'b1;
          en_avdd2 <= 1'b1;
          en_vgl <= 1'b1;
          en_vgh <= 1'b1;
          power_good <= vgl_stable && vgh_stable;
        end

        PWR_IDLE_L2: begin
          current_mode <= PWR_IDLE_L2;
          en_dvdd <= 1'b1;
          en_avdd1 <= 1'b1;
          en_avdd2 <= 1'b0;
          en_vgl <= 1'b1;
          en_vgh <= 1'b0;
        end

        default: begin
          current_mode <= PWR_IDLE_L3;
          en_vgh <= 1'b0;
          en_vgl <= 1'b0;
          en_avdd1 <= 1'b0;
          en_avdd2 <= 1'b0;
          en_dvdd <= 1'b1;
        end
      endcase
    end
  end

endmodule
