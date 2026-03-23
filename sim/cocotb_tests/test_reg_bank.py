import cocotb

from .vector_utils import run_vector_test

@cocotb.test()
async def reg_bank_defaults(dut):
    await run_vector_test(
        dut,
        "reg_bank_defaults.hex",
        init_values={
            "reg_addr": 0,
            "reg_wdata": 0,
            "reg_wr_en": 0,
            "reg_rd_en": 0,
            "sts_busy": 0,
            "sts_done": 0,
            "sts_error": 0,
            "sts_line_rdy": 0,
            "sts_line_idx": 0,
            "sts_err_code": 0,
        },
    )
