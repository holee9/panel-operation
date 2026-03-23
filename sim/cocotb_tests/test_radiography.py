import cocotb

from .vector_utils import run_vector_test


@cocotb.test()
async def radiography_mode_reset_smoke(dut):
    await run_vector_test(
        dut,
        "panel_fsm_radiography.hex",
        init_values={
            "ctrl_start": 0,
            "ctrl_abort": 0,
            "cfg_mode": 2,
            "cfg_combo": 1,
            "cfg_treset": 1,
            "cfg_tinteg": 2,
            "cfg_nrows": 2,
            "cfg_nreset": 1,
            "cfg_sync_dly": 1,
            "cfg_tgate_settle": 1,
            "radiography_mode": 1,
            "xray_prep_req": 0,
            "xray_on": 0,
            "xray_off": 0,
            "gate_row_done": 0,
            "afe_config_done": 0,
            "afe_line_valid": 0,
            "prot_error": 0,
            "prot_force_stop": 0,
        },
    )
