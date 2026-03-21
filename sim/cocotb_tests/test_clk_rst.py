import cocotb
from cocotb.triggers import RisingEdge, Timer


@cocotb.test()
async def clk_rst_locks_after_reset(dut):
    dut.clk_sys.value = 0
    dut.rst_ext_n.value = 0
    dut.afe_type_sel.value = 0

    async def drive_clock():
        while True:
            dut.clk_sys.value = 0
            await Timer(5, units="ns")
            dut.clk_sys.value = 1
            await Timer(5, units="ns")

    cocotb.start_soon(drive_clock())
    await RisingEdge(dut.clk_sys)
    dut.rst_ext_n.value = 1

    for _ in range(20):
        await RisingEdge(dut.clk_sys)

    assert int(dut.pll_locked.value) == 1
    assert int(dut.rst_sync_n.value) == 1
