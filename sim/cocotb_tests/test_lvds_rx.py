import cocotb
from cocotb.triggers import Timer


@cocotb.test()
async def lvds_rx_disabled_holds_idle(dut):
    dut.rst_n.value = 0
    dut.rx_enable.value = 0
    await Timer(1, units="ns")
    dut.rst_n.value = 1
    await Timer(1, units="ns")
    assert int(dut.pixel_valid.value) == 0
