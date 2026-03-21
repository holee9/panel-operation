import cocotb
from cocotb.triggers import Timer


@cocotb.test()
async def spi_idle_defaults(dut):
    dut.clk.value = 0
    dut.rst_n.value = 0
    dut.spi_sclk.value = 0
    dut.spi_mosi.value = 0
    dut.spi_cs_n.value = 1
    dut.reg_rdata.value = 0

    await Timer(1, units="ns")
    dut.rst_n.value = 1
    await Timer(1, units="ns")

    assert int(dut.reg_wr_en.value) == 0
    assert int(dut.reg_rd_en.value) == 0
