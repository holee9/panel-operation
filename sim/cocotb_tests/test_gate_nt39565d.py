import cocotb
from cocotb.triggers import Timer


@cocotb.test()
async def gate_nt39565d_reset_outputs(dut):
    dut.clk.value = 0
    dut.rst_n.value = 0
    dut.gate_on_pulse.value = 0
    await Timer(1, units="ns")
    assert int(dut.nt_oe1_l.value) == 0
