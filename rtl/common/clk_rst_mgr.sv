// clk_rst_mgr.sv — Clock generation (MMCM) and reset synchronization
// Generates ACLK (10 MHz), MCLK (32 MHz) from system clock
module clk_rst_mgr
  import fpd_params_pkg::*;
#(
    parameter AFE_CLK_HZ = ACLK_HZ  // ACLK_HZ for AD711xx, MCLK_HZ for AFE2256
)(
    input  logic clk_sys,       // System clock (100 MHz)
    input  logic rst_ext_n,     // External async reset

    output logic clk_afe,       // AFE master clock (ACLK or MCLK)
    output logic clk_sys_out,   // Buffered system clock
    output logic rst_sync_n,    // Synchronized active-low reset
    output logic pll_locked     // MMCM lock indicator
);

  // TODO: Instantiate MMCME2_ADV, async-to-sync reset

endmodule
