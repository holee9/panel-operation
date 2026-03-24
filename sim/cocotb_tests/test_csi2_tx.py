import cocotb
from cocotb.clock import Clock
from cocotb.triggers import RisingEdge


@cocotb.test()
async def csi2_packet_builder_emits_header_payload_and_last(dut):
    cocotb.start_soon(Clock(dut.clk, 10, units="ns").start())

    dut.rst_n.value = 0
    dut.pixel_data.value = 0
    dut.pixel_valid.value = 0
    dut.line_start.value = 0
    dut.line_end.value = 0
    dut.frame_start.value = 0
    dut.frame_end.value = 0

    for _ in range(3):
        await RisingEdge(dut.clk)

    dut.rst_n.value = 1
    for _ in range(2):
        await RisingEdge(dut.clk)

    dut.line_start.value = 1
    await RisingEdge(dut.clk)
    dut.line_start.value = 0

    observed_bytes = []
    observed_last = []

    for _ in range(4):
        await RisingEdge(dut.clk)
        assert int(dut.packet_valid.value) == 1, "CSI-2 header bytes should be emitted on consecutive cycles"
        observed_bytes.append(int(dut.packet_byte.value))
        observed_last.append(int(dut.packet_last.value))

    dut.pixel_data.value = 0xABCD
    dut.pixel_valid.value = 1
    await RisingEdge(dut.clk)
    observed_bytes.append(int(dut.packet_byte.value))
    observed_last.append(int(dut.packet_last.value))

    dut.line_end.value = 1
    await RisingEdge(dut.clk)
    observed_bytes.append(int(dut.packet_byte.value))
    observed_last.append(int(dut.packet_last.value))

    dut.pixel_valid.value = 0
    dut.line_end.value = 0
    await RisingEdge(dut.clk)

    assert observed_bytes[:4] == [0x2E, 0x00, 0x00, 0x1D], "header should encode data type, zero word count, and ECC"
    assert observed_bytes[4:] == [0xAB, 0xCD], "payload should emit high byte then low byte"
    assert observed_last == [0, 0, 0, 0, 0, 1], "packet_last should only assert on the trailer byte"
    assert int(dut.word_count.value) == 2, "one 16-bit pixel should advance word_count by two bytes"
