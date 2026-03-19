// fpd_params_pkg.sv — Configurable parameters for X-ray FPD system
package fpd_params_pkg;

  // System clock
  parameter SYS_CLK_HZ = 100_000_000;

  // Panel geometry (runtime configurable via reg_bank, defaults here)
  parameter MAX_ROWS = 3072;
  parameter MAX_COLS = 3072;

  // AFE clock frequencies
  parameter ACLK_HZ = 10_000_000;   // AD71124/AD71143
  parameter MCLK_HZ = 32_000_000;   // AFE2256

  // AFE configuration
  parameter MAX_AFE_CHIPS = 24;
  parameter LVDS_PAIRS_PER_AFE = 3;  // DOUT x2 + DCLK = 3 diff pairs = 6 pins

  // Gate IC timing defaults (overridden by reg_bank at runtime)
  parameter T_GATE_ON_DEFAULT  = 16'd2200;  // 22us in 10ns units
  parameter T_GATE_SETTLE_DEFAULT = 8'd100; // 1us in 10ns units

  // Safety limits
  parameter MAX_EXPOSURE_MS = 5000;
  parameter TIMEOUT_5SEC_CLKS = SYS_CLK_HZ * 5;

  // Register map
  parameter REG_ADDR_WIDTH = 5;   // 32 registers (0x00-0x1F)
  parameter REG_DATA_WIDTH = 16;

  // Line buffer
  parameter PIXEL_WIDTH = 16;

endpackage
