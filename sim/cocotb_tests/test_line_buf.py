import cocotb
from cocotb.triggers import Timer


@cocotb.test()
async def line_buf_idle_read_zero(dut):
    dut.rst_n.value = 0
    dut.rd_en.value = 0
    await Timer(1, units="ns")
    dut.rst_n.value = 1
    await Timer(1, units="ns")
    assert int(dut.wr_line_done.value) == 0
