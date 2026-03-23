import cocotb
from cocotb.triggers import RisingEdge

from .conftest import reset_sync, start_clock


@cocotb.test()
async def afe_ad711xx_config_request(dut):
    await start_clock(dut.clk)
    dut.config_req.value = 0
    dut.afe_start.value = 0
    dut.line_idx.value = 0
    dut.cfg_tline.value = 2200
    dut.cfg_ifs.value = 3
    dut.cfg_lpf.value = 0
    dut.cfg_pmode.value = 0
    dut.cfg_nchip.value = 1
    await reset_sync(dut)
    dut.config_req.value = 1
    for _ in range(32):
        await RisingEdge(dut.clk)
        if int(dut.config_done.value) == 1:
            break

    assert int(dut.afe_ready.value) == 1
    dut.config_req.value = 0
    dut.afe_start.value = 1
    for _ in range(8):
        await RisingEdge(dut.clk)
        if int(dut.dout_window_valid.value) == 1:
            break
    assert int(dut.dout_window_valid.value) == 1
