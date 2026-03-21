# Architecture Overview: X-ray FPD FPGA Control System

## Design Pattern

Parameterized Hardware Abstraction Architecture — 공통 코어 + 조합별 드라이버 선택 + 런타임 파라미터.

## System Boundaries

```
┌──────────────────────────────────────────────────────────────────────┐
│                          FPGA (Main Controller)                       │
│                                                                       │
│  MCU SPI ──→ reg_bank ──→ panel_ctrl_fsm ──→ gate_ic_driver          │
│                │                 │                 │                   │
│                │                 ├──→ afe_ctrl_if ──→ LVDS → line_buf │
│                │                 │                                     │
│                │                 └──→ prot_mon                        │
│                │                                                       │
│  clk_rst_mgr ─┤                      calibration_pipeline             │
│                │                      (offset→gain→defect→lag)        │
│                │                                                       │
│  power_sequencer ──→ emergency_shutdown                               │
│                                                                       │
│  data_out_mux ──→ mcu_data_if ──→ MCU/Host                          │
└──────────────────────────────────────────────────────────────────────┘
     ↕ SPI           ↕ GPIO/LVDS        ↕ LVDS            ↕ TTL
   MCU              Gate IC            AFE/ROIC         X-ray Gen
```

## Module Statistics

- Total modules: 15
- Shared (all combinations): 13
- Combination-specific: 2 (gate_ic_driver, afe_ctrl_if)
- Parameterized: 4 (clk_rst_mgr, row_scan_eng, line_buf_ram, line_data_rx)

## Data Flow

1. MCU → SPI → reg_bank (configuration)
2. panel_ctrl_fsm → gate_ic_driver → Panel (row scan)
3. panel_ctrl_fsm → afe_ctrl_if → AFE (trigger ADC)
4. AFE → LVDS → line_data_rx → line_buf_ram (pixel data)
5. line_buf_ram → calibration_pipeline → data_out_mux → MCU (corrected image)

## Clock Domains

| Domain | Frequency | Source | Modules |
|--------|-----------|--------|---------|
| sys_clk | 100 MHz | FPGA oscillator | FSM, reg_bank, SPI |
| aclk | 10-40 MHz | MMCM | afe_ad711xx |
| mclk | 32 MHz | MMCM | afe_afe2256 |
| dclk | 80-200 MHz | AFE self-clocked | line_data_rx (per-AFE) |
| spi_clk | 1-10 MHz | MCU master | spi_slave_if |
