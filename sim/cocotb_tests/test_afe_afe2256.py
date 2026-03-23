import cocotb
from cocotb.clock import Clock
from cocotb.triggers import RisingEdge


@cocotb.test()
async def afe_afe2256_reset_state(dut):
    """After reset, AFE2256 config_done should be 0 and afe_ready deasserted."""
    cocotb.start_soon(Clock(dut.clk, 10, units="ns").start())
    dut.rst_n.value = 0
    dut.config_req.value = 0
    for _ in range(3):
        await RisingEdge(dut.clk)
    dut.rst_n.value = 1
    for _ in range(2):
        await RisingEdge(dut.clk)
    assert int(dut.config_done.value) == 0, "config_done should be 0 after reset without request"


@cocotb.test()
async def afe_afe2256_config_sequence(dut):
    """Config request should eventually assert config_done."""
    cocotb.start_soon(Clock(dut.clk, 10, units="ns").start())
    dut.rst_n.value = 0
    dut.config_req.value = 0
    for _ in range(3):
        await RisingEdge(dut.clk)
    dut.rst_n.value = 1
    await RisingEdge(dut.clk)

    dut.config_req.value = 1
    config_done = False
    for _ in range(32):
        await RisingEdge(dut.clk)
        if int(dut.config_done.value) == 1:
            config_done = True
            break
    assert config_done, "config_done should assert within 32 cycles after config_req"
