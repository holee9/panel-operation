from pathlib import Path

import cocotb
from cocotb.clock import Clock
from cocotb.triggers import RisingEdge


REPO_ROOT = Path(__file__).resolve().parents[2]
RTL_ROOT = REPO_ROOT / "rtl"
VECTOR_ROOT = REPO_ROOT / "sim" / "testvectors"


async def start_clock(signal, period_ns=10):
    cocotb.start_soon(Clock(signal, period_ns, units="ns").start())


async def reset_sync(dut, clk_name="clk", rst_name="rst_n", cycles=2):
    clk = getattr(dut, clk_name)
    rst = getattr(dut, rst_name)
    rst.value = 0
    for _ in range(cycles):
        await RisingEdge(clk)
    rst.value = 1
    for _ in range(cycles):
        await RisingEdge(clk)
