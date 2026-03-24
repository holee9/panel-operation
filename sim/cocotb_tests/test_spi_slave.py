import cocotb
from cocotb.clock import Clock
from cocotb.triggers import RisingEdge


@cocotb.test()
async def spi_write_transaction_pulses_reg_write(dut):
    async def wait_cycles(count):
        saw_wr_en = False
        for _ in range(count):
            await RisingEdge(dut.clk)
            saw_wr_en |= int(dut.reg_wr_en.value) == 1
        return saw_wr_en

    async def transfer_bit(bit_value):
        dut.spi_mosi.value = bit_value
        dut.spi_sclk.value = 0
        saw_wr_en = await wait_cycles(3)
        dut.spi_sclk.value = 1
        saw_wr_en |= await wait_cycles(3)
        dut.spi_sclk.value = 0
        saw_wr_en |= await wait_cycles(3)
        return saw_wr_en

    cocotb.start_soon(Clock(dut.clk, 10, units="ns").start())

    dut.rst_n.value = 0
    dut.spi_sclk.value = 0
    dut.spi_mosi.value = 0
    dut.spi_cs_n.value = 1
    dut.reg_rdata.value = 0

    await wait_cycles(3)
    dut.rst_n.value = 1
    await wait_cycles(3)

    dut.spi_cs_n.value = 0
    await wait_cycles(3)

    saw_wr_en = False
    frame = [int(bit) for bit in f"{0x82:08b}{0xA5C3:016b}"]
    for bit in frame:
        saw_wr_en |= await transfer_bit(bit)

    dut.spi_cs_n.value = 1
    await wait_cycles(3)

    assert saw_wr_en, "write transaction should pulse reg_wr_en"
    assert int(dut.reg_rd_en.value) == 0, "write transaction should not pulse reg_rd_en"
    assert int(dut.reg_addr.value) == 0x02, "address phase should capture register address 0x02"
    assert int(dut.reg_wdata.value) == 0xA5C3, "write data should match the shifted MOSI payload"
