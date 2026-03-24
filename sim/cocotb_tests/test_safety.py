import cocotb
from cocotb.clock import Clock
from cocotb.triggers import RisingEdge


@cocotb.test()
async def prot_mon_trips_timeout_and_clears_in_idle(dut):
    cocotb.start_soon(Clock(dut.clk, 10, units="ns").start())

    dut.rst_n.value = 0
    dut.fsm_state.value = 0
    dut.xray_active.value = 0
    dut.cfg_max_exposure.value = 3
    dut.radiography_mode.value = 0

    for _ in range(3):
        await RisingEdge(dut.clk)

    dut.rst_n.value = 1
    dut.fsm_state.value = 4
    dut.xray_active.value = 1

    for _ in range(3):
        await RisingEdge(dut.clk)

    assert int(dut.err_timeout.value) == 1, "timeout should assert when exposure reaches cfg_max_exposure"
    assert int(dut.err_flag.value) == 1, "timeout should raise the aggregated error flag"
    assert int(dut.force_gate_off.value) == 1, "timeout should force gates off"

    dut.fsm_state.value = 0
    dut.xray_active.value = 0
    await RisingEdge(dut.clk)

    assert int(dut.err_timeout.value) == 0, "idle state should clear timeout"
    assert int(dut.err_flag.value) == 0, "idle state should clear the aggregated error flag"
    assert int(dut.force_gate_off.value) == 0, "idle state should release force_gate_off"
