# Codex Session Memory

## Active Role

- Codex role: `codex-coding`

## Active Issue

- Issue: `#2`
- Title: `docs cleanup and content organization`
- Status: open

## Current Code Focus

- Review follow-up implementation for `detector_core`, top variants, and panel/gate control
- Keep SW-first golden models aligned with the updated RTL state contracts
- SW simulation review fixes applied for `PanelFsmModel`, `RadiogModel`, `AfeAd711xxModel`, and `LineBufModel`
- Review-driven verification hardening applied for `sim/verilator`, cocotb smoke tests, `reg_bank`, gate drivers, CSI-2 ECC, and radiography dark-frame handling
- cocotb vector-loader path and checked-in reference vectors added for `panel_ctrl_fsm`, `reg_bank`, and `detector_core`
- `TestVectorIO` binary readback and `CR-002` timeout split tests added

## Next Resume Step

- Read `AGENT.md`
- Read this file
- Re-open the latest review docs and issue `#2` comments
- Run real RTL/sim toolchain checks for updated `sim/` models, `sim/verilator`, and `detector_core + panel_ctrl_fsm`
- Continue with RTL-side multi-AFE data-path work in `rtl/top/detector_core.sv`
- Convert remaining cocotb tests from clocked smoke checks to vector-driven compare runs once toolchain is available
