import cocotb
from cocotb.clock import Clock
from cocotb.triggers import RisingEdge


@cocotb.test()
async def lvds_rx_deserializes_one_pixel_word(dut):
    cocotb.start_soon(Clock(dut.lvds_dclk, 10, units="ns").start())

    dut.clk.value = 0
    dut.rst_n.value = 0
    dut.lvds_dout_a.value = 0
    dut.lvds_dout_b.value = 0
    dut.lvds_fclk.value = 0
    dut.rx_enable.value = 0
    dut.bitslip_req.value = 0

    for _ in range(3):
        await RisingEdge(dut.lvds_dclk)

    dut.rst_n.value = 1
    dut.rx_enable.value = 1

    bit_pairs = [(1, 0), (1, 0), (0, 1), (0, 1), (1, 1), (0, 0), (0, 0), (1, 1)]
    for bit_a, bit_b in bit_pairs:
        dut.lvds_dout_a.value = bit_a
        dut.lvds_dout_b.value = bit_b
        await RisingEdge(dut.lvds_dclk)

    assert int(dut.pixel_valid.value) == 1, "eight LVDS bit pairs should emit one pixel"
    assert int(dut.pixel_data.value) == 0xA5C3, "deserialized pixel word should match the input bit stream"
    assert int(dut.pixel_col_idx.value) == 1, "column index should advance after one pixel"
