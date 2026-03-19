// fpd_types_pkg.sv — Global type definitions for X-ray FPD system
package fpd_types_pkg;

  // FSM states
  typedef enum logic [2:0] {
    ST_IDLE         = 3'b000,
    ST_RESET        = 3'b001,
    ST_INTEGRATE    = 3'b010,
    ST_READOUT_INIT = 3'b011,
    ST_SCAN_LINE    = 3'b100,
    ST_READOUT_DONE = 3'b101,
    ST_DONE         = 3'b110,
    ST_ERROR        = 3'b111
  } fsm_state_t;

  // Operating modes (REG_MODE[2:0])
  typedef enum logic [2:0] {
    MODE_STATIC     = 3'b000,
    MODE_CONTINUOUS = 3'b001,
    MODE_TRIGGERED  = 3'b010,
    MODE_DARK_FRAME = 3'b011,
    MODE_RESET_ONLY = 3'b100
  } op_mode_t;

  // Gate IC type selection
  typedef enum logic {
    GATE_NV1047   = 1'b0,
    GATE_NT39565D = 1'b1
  } gate_ic_type_t;

  // AFE type selection
  typedef enum logic [1:0] {
    AFE_AD71124 = 2'b00,
    AFE_AD71143 = 2'b01,
    AFE_AFE2256 = 2'b10
  } afe_type_t;

  // Power modes (M0-M5)
  typedef enum logic [2:0] {
    PWR_POWER_UP = 3'b000,
    PWR_ACTIVE   = 3'b001,
    PWR_IDLE_L1  = 3'b010,
    PWR_IDLE_L2  = 3'b011,
    PWR_IDLE_L3  = 3'b100,
    PWR_CAL_DARK = 3'b101
  } power_mode_t;

  // Hardware combination ID (C1-C7)
  typedef enum logic [2:0] {
    COMBO_C1 = 3'd1,
    COMBO_C2 = 3'd2,
    COMBO_C3 = 3'd3,
    COMBO_C4 = 3'd4,
    COMBO_C5 = 3'd5,
    COMBO_C6 = 3'd6,
    COMBO_C7 = 3'd7
  } combo_id_t;

endpackage
