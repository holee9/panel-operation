import cocotb
from cocotb.triggers import Timer


@cocotb.test()
async def reg_bank_defaults(dut):
    dut.clk.value = 0
    dut.rst_n.value = 0
    dut.reg_addr.value = 0
    dut.reg_wdata.value = 0
    dut.reg_wr_en.value = 0
    dut.reg_rd_en.value = 0
    dut.sts_busy.value = 0
    dut.sts_done.value = 0
    dut.sts_error.value = 0
    dut.sts_line_rdy.value = 0
    dut.sts_line_idx.value = 0
    dut.sts_err_code.value = 0

    await Timer(1, units="ns")
    dut.rst_n.value = 1
    await Timer(1, units="ns")

    assert int(dut.cfg_mode.value) == 0
    assert int(dut.cfg_combo.value) == 1
    assert int(dut.cfg_nreset.value) == 3
