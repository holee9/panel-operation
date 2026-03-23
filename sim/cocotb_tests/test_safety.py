import cocotb
from cocotb.triggers import Timer


@cocotb.test()
async def prot_mon_reset_state(dut):
    dut.clk.value = 0
    dut.rst_n.value = 0
    await Timer(1, units="ns")
    dut.rst_n.value = 1
    await Timer(1, units="ns")
    assert int(dut.err_flag.value) == 0
