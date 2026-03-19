# Technology Stack: panel-operation

## Primary Language

SystemVerilog (IEEE 1800-2017)

## Target FPGA

**Device**: xc7a35tfgg484-1

| Spec | Value |
|------|-------|
| Family | Xilinx Artix-7 |
| Part | xc7a35tfgg484-1 |
| Package | FGG484 |
| Speed Grade | -1 |
| Logic Cells | 33,280 |
| CLB Slices | 5,200 |
| DSP48E1 | 90 |
| BRAM36K | 50 (1,800 Kb total) |
| BRAM18K | 100 |
| MMCM | 5 |
| I/O Pins | 250 (FGG484) |
| HR I/O Banks | 10 |
| ISERDESE2 | Available (Artix-7 series) |
| IDELAYE2 | Available |

### Resource Feasibility per Combination

| Combination | Feasibility | Readout Mode | Notes |
|-------------|------------|--------------|-------|
| C1-C5 (1 AFE) | ✅ Direct | 1 AFE, 3 LVDS pairs | ~12 I/O pins |
| C6-C7 (12 AFE) | ✅ Direct | 12 AFE, 36 LVDS pairs | ~78 I/O pins |
| 24-AFE (확장) | ✅ Direct | 24 AFE, 72 LVDS pairs (실제 운용 중) | ~150 I/O pins |

### LVDS Pin Budget (per AFE = 3 differential pairs = 6 pins)

| Signal | Pairs | Pins | Description |
|--------|-------|------|-------------|
| DOUT_P/M × 2 | 2 | 4 | Data output (2 LVDS pairs) |
| DCLK_P/M | 1 | 2 | Data clock |
| **Per AFE** | **3** | **6** | |

| Configuration | LVDS Pairs | LVDS Pins | Other I/O | Total | Artix-7 250 I/O |
|---------------|-----------|-----------|-----------|-------|------------------|
| 1 AFE (C1-C5) | 3 | 6 | ~20 | ~26 | ✅ 여유 |
| 12 AFE (C6-C7) | 36 | 72 | ~20 | ~92 | ✅ 여유 |
| 24 AFE | 72 | 144 | ~20 | ~164 | ✅ 가능 |

### 24-AFE Direct LVDS Architecture (Working System)

Artix-7 35T에서 24 AFE를 지원. **모든 AFE LVDS 출력이 FPGA에 직접 연결** (외부 MUX 없음).

- Per AFE: 3 LVDS pairs (6 pins) — DOUT ×2 + DCLK
- 24 AFE: 72 LVDS pairs = 144 pins (of 250 available)
- 브로드캐스트 SYNC/ACLK/MCLK (fan-out 버퍼 사용)
- SPI 데이지체인: 24 × 24-bit = 576 bits per write (~58µs @ 10MHz)
- 각 AFE마다 전용 LVDS 수신기 인스턴스 (line_data_rx)

### BRAM Budget (C1-C5, Artix-7 35T)

| Use | BRAM36K | Notes |
|-----|---------|-------|
| line_buf_ram (1 line) | 2 | 2048 × 16bit |
| Defect pixel map | 1 | Sparse list (~50 entries) |
| SPI/Register | 0 | Uses CLB distributed RAM |
| Offset map cache (1 line) | 2 | 2048 × 16bit |
| Gain map cache (1 line) | 2 | 2048 × 16bit |
| LVDS deserialize FIFO | 1-24 | Async FIFO for CDC (per AFE) |
| **Total Used** | **~8** | **of 50 available** |
| **Available for calibration** | **42** | ~151 KB for partial calibration storage |

> Full offset/gain maps (2048×2048×16bit = 8MB each) require external memory (SRAM or DDR).

## Simulation & Verification Tools

- **HDL Simulator**: Questa/ModelSim (Siemens EDA)
  - License: Windows Terminal Services 환경에서는 GUI 모드 필수
  - Command: `vsim -version` (라이선스 확인)
- **Synthesis & Implementation**: Vivado 2025.2 (Xilinx/AMD)
- **Timing Analysis**: Vivado STA
- **Linting**: Verilator (optional), Vivado lint
- **Simulation (alternative)**: Vivado Simulator (xsim) — 라이선스 불필요

## Key Dependencies (IP Cores)

| IP | Purpose | Vendor |
|----|---------|--------|
| IBUFDS | LVDS 차동 입력 → 싱글엔드 변환 | Xilinx Primitives |
| ISERDESE2 | LVDS 역직렬화 (8:1) | Xilinx Primitives |
| IDELAYE2 | LVDS 위상 보정 (tap 0-31) | Xilinx Primitives |
| MMCM/PLL | 클럭 생성 (ACLK 10MHz, MCLK 32MHz, DCLK) | Xilinx Clocking Wizard |
| Block RAM | 라인 버퍼 (BRAM36, 2-port) | Xilinx Memory |
| DDR3/DDR4 MIG | 프레임 버퍼 (C6/C7 대형 패널) | Xilinx MIG IP |

## Interface Protocols

| Interface | Protocol | Speed | Use |
|-----------|----------|-------|-----|
| MCU ↔ FPGA | SPI Mode 0/3 | 1-10 MHz | 레지스터 R/W, 설정 |
| AFE SPI | SPI (daisy-chain) | 10-40 MHz | AFE 초기화, CIC 로딩 |
| AFE → FPGA | LVDS | 80-200 MHz | 16-bit 픽셀 데이터 |
| X-ray Gen ↔ FPGA | TTL GPIO | Async | PREP_REQUEST, X_RAY_ENABLE |
| FPGA → MCU | Parallel/DMA | 16-32 bit | 프레임 데이터 전송 |

## Critical Timing Constraints

| Path | Constraint | Source |
|------|-----------|--------|
| System Clock | 100 MHz | FPGA oscillator |
| ACLK (AD711xx) | 10-40 MHz | MMCM generated |
| MCLK (AFE2256) | 32 MHz ±1% | MMCM generated |
| LVDS DCLK | 80-200 MHz | AFE self-clocked |
| Gate CLK (NV1047) | ≤ 200 kHz | FPGA divider |
| CPV (NT39565D) | ~100 kHz | FPGA divider |
| SPI MCU | ≤ 10 MHz | MCU master |
| SYNC jitter | < ±5 ns | CDC requirement |

## Physical Constraints

| Parameter | C1-C5 | C6-C7 |
|-----------|-------|-------|
| Line Time Min | 22 µs (AD71124) / 60 µs (AD71143) | 10.85 µs (30fps) / 21.7 µs (15fps) |
| Gate Pulse Min | 15-45 µs (5×τ_TFT) | 40 µs (NT39565D) |
| Pixel Clock | 61+ MHz | 100+ MHz |
| Frame Buffer | 2048x2048x16bit = 8 MB | 3072x3072x16bit = 18.9 MB |
| Lag State Buffer | N/A (simple) | 150 MB DDR3 (4-component) |
| LVDS Receivers | 5 (1 AFE) | 60 (12 AFEs) |

## Development Environment

- **OS**: Windows 11 Pro
- **Build**: Vivado 2025.2 (synthesis + implementation)
- **Simulation**: Questa/ModelSim GUI mode
- **Version Control**: Git + GitHub (holee9/panel-operation)
- **Documentation**: Markdown (Korean)
- **MSBuild**: D:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe

## Power Sequencing Rules (Safety-Critical)

1. VGL must stabilize BEFORE VGH (violation → Gate IC latch-up)
2. AFE AVDD1 before AVDD2 (reverse bias protection)
3. DVDD before Gate IC outputs
4. Soft-start slope ≤ 5 V/ms (inrush limiting)
5. Emergency shutdown: < 100 µs response time

## Patent Considerations

| Patent | Status | Impact |
|--------|--------|--------|
| US5452338A (GE, recursive offset) | Active | Offset 업데이트에 재귀 필터 사용 시 라이선스 필요 |
| US7792251B2 (direct lag measurement) | Expired 2028 | 자유 사용 가능 |
| US7404673B2 (multi-angle gain) | Expired 2026 | 자유 사용 가능 |
