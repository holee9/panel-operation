import cocotb
from cocotb.clock import Clock
from cocotb.triggers import RisingEdge


@cocotb.test()
async def gate_nt39565d_drives_stv_oe_and_row_done(dut):
    cocotb.start_soon(Clock(dut.clk, 10, units="ns").start())

    dut.rst_n.value = 0
    dut.row_index.value = 0
    dut.gate_on_pulse.value = 0
    dut.scan_dir.value = 0
    dut.chip_sel.value = 0b10
    dut.mode_sel.value = 0
    dut.cfg_cpv_period.value = 10
    dut.cfg_stv_pulse.value = 3
    dut.cfg_gate_on.value = 8
    dut.cascade_stv_return.value = 0

    for _ in range(3):
        await RisingEdge(dut.clk)

    dut.rst_n.value = 1
    await RisingEdge(dut.clk)

    dut.gate_on_pulse.value = 1
    dut.cascade_stv_return.value = 1
    await RisingEdge(dut.clk)

    assert int(dut.nt_stv1l.value) == 1 and int(dut.nt_stv1r.value) == 1, "row 0 should drive STV1 on both banks"
    assert int(dut.nt_stv2l.value) == 0 and int(dut.nt_stv2r.value) == 0, "row 0 should not drive STV2"
    assert int(dut.nt_oe1_l.value) == 1 and int(dut.nt_oe1_r.value) == 1, "even row should enable OE1 on both banks"
    assert int(dut.cascade_complete.value) == 1, "cascade return should complete when the dual-bank chip select is active"

    dut.row_index.value = 1
    await RisingEdge(dut.clk)

    assert int(dut.nt_stv2l.value) == 1 and int(dut.nt_stv2r.value) == 1, "odd row should switch the STV phase"
    assert int(dut.nt_oe2_l.value) == 1 and int(dut.nt_oe2_r.value) == 1, "odd row should enable OE2 on both banks"

    dut.gate_on_pulse.value = 0
    dut.cascade_stv_return.value = 0
    await RisingEdge(dut.clk)

    assert int(dut.row_done.value) == 1, "row_done should pulse when gate_on_pulse falls"
