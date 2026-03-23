import cocotb
from cocotb.clock import Clock
from cocotb.triggers import RisingEdge


@cocotb.test()
async def gate_nv1047_reset_state(dut):
    """After reset, NV1047 OE should be inactive (high) and row_done deasserted."""
    cocotb.start_soon(Clock(dut.clk, 10, units="ns").start())
    dut.rst_n.value = 0
    dut.gate_on_pulse.value = 0
    dut.cfg_gate_settle.value = 4
    for _ in range(3):
        await RisingEdge(dut.clk)
    dut.rst_n.value = 1
    for _ in range(3):
        await RisingEdge(dut.clk)
    assert int(dut.nv_oe.value) == 1, "OE should be inactive (high) after reset"
    assert int(dut.row_done.value) == 0, "row_done should be deasserted after reset"


@cocotb.test()
async def gate_nv1047_bbm_gap(dut):
    """OE should deactivate during break-before-make gap after gate_on_pulse falls."""
    cocotb.start_soon(Clock(dut.clk, 10, units="ns").start())
    dut.rst_n.value = 0
    dut.gate_on_pulse.value = 0
    dut.cfg_gate_settle.value = 4
    for _ in range(3):
        await RisingEdge(dut.clk)
    dut.rst_n.value = 1
    await RisingEdge(dut.clk)

    # Assert gate_on_pulse for 2 cycles
    dut.gate_on_pulse.value = 1
    for _ in range(2):
        await RisingEdge(dut.clk)

    # Release gate_on_pulse — BBM gap starts
    dut.gate_on_pulse.value = 0
    await RisingEdge(dut.clk)
    assert int(dut.row_done.value) == 0, "row_done should not assert immediately after gate_on falls"
