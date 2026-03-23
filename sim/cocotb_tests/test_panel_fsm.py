import cocotb

from .vector_utils import run_vector_test


@cocotb.test()
async def panel_fsm_reset_to_idle(dut):
    await run_vector_test(
        dut,
        "panel_fsm_static.hex",
        init_values={
            "ctrl_start": 0,
            "ctrl_abort": 0,
            "cfg_mode": 0,
            "cfg_combo": 1,
            "cfg_treset": 1,
            "cfg_tinteg": 1,
            "cfg_nrows": 2,
            "cfg_nreset": 1,
            "cfg_sync_dly": 1,
            "cfg_tgate_settle": 1,
            "radiography_mode": 0,
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
