import cocotb
from cocotb.clock import Clock
from cocotb.triggers import RisingEdge


@cocotb.test()
async def line_buf_ram_writes_reads_and_swaps_bank_on_last_column(dut):
    cocotb.start_soon(Clock(dut.wr_clk, 10, units="ns").start())
    cocotb.start_soon(Clock(dut.rd_clk, 14, units="ns").start())

    dut.rst_n.value = 0
    dut.wr_data.value = 0
    dut.wr_addr.value = 0
    dut.wr_en.value = 0
    dut.wr_bank_sel.value = 0
    dut.rd_addr.value = 0
    dut.rd_en.value = 0
    dut.rd_bank_sel.value = 0

    for _ in range(3):
        await RisingEdge(dut.wr_clk)
    dut.rst_n.value = 1
    await RisingEdge(dut.wr_clk)

    dut.wr_en.value = 1
    dut.wr_addr.value = 0
    dut.wr_data.value = 0x1234
    await RisingEdge(dut.wr_clk)

    dut.wr_addr.value = 2047
    dut.wr_data.value = 0xBEEF
    await RisingEdge(dut.wr_clk)

    assert int(dut.wr_line_done.value) == 1, "last-column write should assert wr_line_done"
    assert int(dut.bank_swap.value) == 1, "last-column write should request a bank swap"

    dut.wr_en.value = 0
    dut.rd_en.value = 1
    dut.rd_bank_sel.value = 0

    dut.rd_addr.value = 0
    await RisingEdge(dut.rd_clk)
    assert int(dut.rd_data.value) == 0x1234, "read port should return the first written pixel"

    dut.rd_addr.value = 2047
    await RisingEdge(dut.rd_clk)
    assert int(dut.rd_data.value) == 0xBEEF, "read port should return the final column value from the active bank"
