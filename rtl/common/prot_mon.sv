// prot_mon.sv — Protection monitor: over-exposure timeout, error flags, forced gate-off
module prot_mon
  import fpd_types_pkg::*;
  import fpd_params_pkg::*;
(
    input  logic        clk,
    input  logic        rst_n,

    // From panel_ctrl_fsm
    input  fsm_state_t  fsm_state,
    input  logic        xray_active,

    // Timeout configuration (from reg_bank)
    input  logic [23:0] cfg_max_exposure,  // max integration time

    // Error outputs
    output logic        err_timeout,       // over-exposure timeout flag
    output logic        err_flag,          // general error flag
    output logic        force_gate_off     // force all gates OFF
);

  // TODO: Implement timeout counter and error detection

endmodule
