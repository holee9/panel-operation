// clk_rst_mgr.sv - Clock generation bootstrap and reset synchronization
// This behavioral version is suitable for simulation-first bring-up.
module clk_rst_mgr
  import fpd_params_pkg::*;
#(
    parameter AFE_CLK_HZ = ACLK_HZ
)(
    input  logic clk_sys,
    input  logic rst_ext_n,
    input  logic [1:0] afe_type_sel,

    output logic clk_afe,
    output logic clk_aclk,
    output logic clk_mclk,
    output logic clk_sys_out,
    output logic rst_sync_n,
    output logic pll_locked
);

  localparam longint unsigned NCO_SCALE  = 64'd4294967296;
  localparam longint unsigned PHASE_STEP_ACLK = (AFE_CLK_HZ * NCO_SCALE) / SYS_CLK_HZ;
  localparam longint unsigned PHASE_STEP_MCLK = (MCLK_HZ * NCO_SCALE) / SYS_CLK_HZ;

  logic [31:0] phase_acc_aclk;
  logic [31:0] phase_acc_mclk;
  logic [4:0]  lock_count;
  logic        rst_pipe1;
  logic        rst_pipe2;

  assign clk_sys_out = clk_sys;

  always_ff @(posedge clk_sys or negedge rst_ext_n) begin
    if (!rst_ext_n) begin
      phase_acc_aclk <= '0;
      phase_acc_mclk <= '0;
      clk_afe    <= 1'b0;
      clk_aclk   <= 1'b0;
      clk_mclk   <= 1'b0;
      pll_locked <= 1'b0;
      lock_count <= '0;
      rst_pipe1  <= 1'b0;
      rst_pipe2  <= 1'b0;
      rst_sync_n <= 1'b0;
    end else begin
      phase_acc_aclk <= phase_acc_aclk + PHASE_STEP_ACLK[31:0];
      phase_acc_mclk <= phase_acc_mclk + PHASE_STEP_MCLK[31:0];
      clk_aclk <= phase_acc_aclk[31];
      clk_mclk <= phase_acc_mclk[31];
      clk_afe <= (afe_type_sel == 2'b10) ? clk_mclk : clk_aclk;

      if (!pll_locked) begin
        lock_count <= lock_count + 5'd1;
        if (lock_count == 5'd15) begin
          pll_locked <= 1'b1;
        end
      end

      rst_pipe1  <= pll_locked;
      rst_pipe2  <= rst_pipe1;
      rst_sync_n <= rst_pipe2;
    end
  end

endmodule
