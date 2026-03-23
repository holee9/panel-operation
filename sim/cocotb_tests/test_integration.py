import cocotb

from .vector_utils import run_vector_test


@cocotb.test()
async def detector_core_reset_smoke(dut):
    await run_vector_test(
        dut,
        "detector_core_reset.hex",
        clock_name="clk_100mhz",
        init_values={
            "spi_sclk": 0,
            "spi_mosi": 0,
            "spi_cs_n": 1,
            "mcu_data_ack": 0,
            "afe_spi_sdo": 0,
            "afe_dout_a": 0,
            "afe_dout_b": 0,
            "afe_dclk": 0,
            "afe_fclk": 0,
            "xray_prep_req": 0,
            "xray_on": 0,
            "xray_off": 0,
            "vgh_over": 0,
            "vgh_under": 0,
            "temp_over": 0,
            "hw_emergency_n": 1,
        },
    )
