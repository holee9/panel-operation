import cocotb
from cocotb.triggers import Timer


@cocotb.test()
async def csi2_packet_builder_reset(dut):
    dut.rst_n.value = 0
    dut.pixel_valid.value = 0
    await Timer(1, units="ns")
    dut.rst_n.value = 1
    await Timer(1, units="ns")
    assert int(dut.packet_valid.value) == 0
