# Multi-AFE Synchronization, Large Panel Control, and Advanced FPGA Design for X-ray FPD
## Comprehensive Research Report

**Target System:** X239AW1-102 — 3072×3072 pixel large flat panel detector  
**Configuration:** 12× AFE2256 (or 12× AD71124) + 6× NT39565D gate drivers + FPGA readout controller  
**Date:** March 2026

---

## Table of Contents

1. [System Overview & Throughput Calculations](#1-system-overview--throughput-calculations)
2. [Multi-Chip ROIC Synchronization Architecture](#2-multi-chip-roic-synchronization-architecture)
3. [Large Panel Readout Architecture](#3-large-panel-readout-architecture)
4. [NT39565D Dual-STV Gate Driving](#4-nt39565d-dual-stv-gate-driving)
5. [FPGA High-Speed LVDS — IDELAY2/ISERDES2](#5-fpga-high-speed-lvds--idelay2iserdes2)
6. [Frame Buffer Architecture — DDR3/DDR4](#6-frame-buffer-architecture--ddr3ddr4)
7. [High Frame Rate Fluoroscopy — 30fps Budget](#7-high-frame-rate-fluoroscopy--30fps-budget)
8. [Power Sequencing for Large Panels](#8-power-sequencing-for-large-panels)
9. [Thermal Management](#9-thermal-management)
10. [Error Detection and Recovery](#10-error-detection-and-recovery)
11. [State-of-the-Art FPGA Designs for FPD](#11-state-of-the-art-fpga-designs-for-fpd)
12. [FPGA Resource Estimates](#12-fpga-resource-estimates)
13. [Best-Practice Design Patterns Summary](#13-best-practice-design-patterns-summary)
14. [References](#14-references)

---

## 1. System Overview & Throughput Calculations

### 1.1 Panel Parameters

| Parameter | Value |
|-----------|-------|
| Pixel array | 3072 × 3072 |
| Pixel pitch | 140 µm (CareRay 1800RF-II class) |
| Active area | ~430 × 430 mm (43 cm × 43 cm) |
| Data depth | 16 bits/pixel |
| Frame size | 3072 × 3072 × 2 bytes = **18,874,368 bytes ≈ 18 MB** |
| AFE chips | 12× AFE2256 (256 channels each = 3072 columns total) |
| Gate drivers | 6× NT39565D (each driving ~512 rows) |

### 1.2 Throughput Calculations

#### Raw Data Rate at 30 fps (Fluoroscopy Mode)

```
Frame size  = 3072 × 3072 × 16 bit = 150,994,944 bits = ~18.874 MB
Frame rate  = 30 fps
Raw throughput = 18.874 MB × 30 = 566.2 MB/s ≈ 540 MB/s (effective with overhead)
```

> **Critical design constraint:** 540 MB/s sustained data rate from panel to FPGA memory.

#### Per-AFE2256 Data Rate

```
AFE2256 has 256 channels → 4 internal 16-bit SAR ADCs (256:4 MUX)
Each ADC serializes 4 differential bits in parallel → 4 LVDS pairs per chip
Scan time range: <20 µs to 204.8 µs per row

At minimum 20 µs scan time:
  - 3072 rows × 20 µs = 61.44 ms/frame → 16.3 fps max
At 30 fps, maximum allowed line time:
  - 1/30 / 3072 rows = 10.85 µs/row (with NO blanking time)
  - Practical target: ≤8 µs line time + 2.85 µs blanking = 10.85 µs total
```

> **Key finding:** Achieving 30fps at full 3072×3072 requires ≤8 µs scan time per row — this is near the AFE2256 minimum specification limit.

#### LVDS Serial Data Rate per AFE2256 Output

```
AFE2256 outputs 4 LVDS channels per chip
Each channel serializes 16-bit word
At 8 µs scan time per row:
  LVDS bit rate per channel = 16 bits / 8 µs = 2 Mbps (very modest)

However, the 4 ADCs process 256 channels in parallel:
  Total data output = 256 × 16 bit = 4096 bits per row scan
  Over 4 LVDS pairs: 1024 bits per LVDS pair per row
  At 8 µs: LVDS clock = 1024 / 8 µs = 128 MHz per LVDS pair
```

#### Aggregate FPGA Input Bandwidth (12 AFEs)

```
12 AFE2256 × 4 LVDS pairs × 128 MHz × 2 (DDR) = 12.3 Gbps aggregate
Per FPGA pin budget: 48 LVDS input pairs at ~128 MHz effective clock
```

### 1.3 MCLK Timing Derivation (AFE2256)

From the AFE2256 architecture: integration occurs over 256 × MCLK cycles when SYNC is asserted.

```
Target scan time = 8 µs
MCLK = 256 cycles / 8 µs = 32 MHz

At MCLK = 32 MHz:
  - Integration window = 256/32 MHz = 8 µs ✓
  - ADC conversion overlaps with integration (pipelined)
  - LVDS serial output clock derived from MCLK

At MCLK = 50 MHz (if supported):
  - Integration = 256/50 = 5.12 µs → 195 fps theoretical max
```

---

## 2. Multi-Chip ROIC Synchronization Architecture

### 2.1 Synchronization Requirements

For a 12× AFE2256 system, all chips must:
1. **Start integration simultaneously** — all 12 SYNC signals must arrive within ≤1 MCLK period of each other
2. **Produce aligned LVDS output clocks** — DCLK from all 12 chips must be phase-coherent for FPGA capture
3. **Maintain frame-to-frame timing** — deterministic latency from SYNC assert to first valid DOUT

### 2.2 SYNC Signal Distribution Topologies

#### Option A: Broadcast (Star) SYNC Distribution

```
                    ┌─────────────────────────────────────┐
                    │              FPGA                   │
                    │  ┌────────────────────────────────┐ │
                    │  │  SYNC Generator (GPIO or MMCM) │ │
                    │  └──────────────┬─────────────────┘ │
                    └─────────────────│───────────────────┘
                                      │
                    ┌─────────────────▼─────────────────┐
                    │       Clock Buffer / Fan-out       │
                    │    (e.g., SN74LVC1G17 × N or      │
                    │     LVDS fan-out buffer IC)        │
                    └─┬────┬────┬────┬────┬────┬────┬──┘
                      │    │    │    │    │    │    │
                   AFE1  AFE2  AFE3  AFE4  AFE5  AFE6  ...AFE12
                   
✓ Pros: Simultaneous assertion, best phase alignment
✗ Cons: PCB trace length matching required (~2.5 ps/mm delay)
  Tolerance: Traces must match within ±1 MCLK/4 = ±7.8 ns at 32 MHz
```

#### Option B: Daisy-Chain SYNC

```
FPGA → AFE1 → AFE2 → AFE3 → ... → AFE12

Each stage adds 1 SYNC propagation delay (typically ≥5 ns at 3.3V CMOS)
Total skew for 12 chips: ≥55 ns → at 32 MHz MCLK (31.25 ns period), 
this exceeds 1.5 MCLK cycles → UNACCEPTABLE for synchronous readout

⚠ Daisy-chain SYNC is NOT recommended for parallel readout of 12 AFEs
  Use ONLY for sequential readout mode where staggered start is acceptable
```

#### Option C: Reference Distribution + SYNC (Best Practice)

Based on [Analog Devices AN165 Multi-Part Clock Synchronization](https://www.analog.com/media/en/technical-documentation/application-notes/an165fa.pdf):

```
Reference Oscillator (e.g., 32 MHz TCXO)
         │
         ├──► FPGA MMCM/PLL (generates internal clocks)
         │
         └──► Clock Fan-out Buffer (e.g., CDCLVP1208, LTC6957)
                    │
              ┌─────┴─────┐
              │ 12 equal- │
              │  length   │
              │  traces   │
              └──┬────┬───┘
                 │    │ ... (matched to ±200 ps)
              AFE1   AFE2  ...  AFE12
              
MCLK distribution: LVDS fan-out, all outputs phase-locked
SYNC distribution: Separate LVCMOS broadcast, all synchronized to MCLK rising edge
```

### 2.3 Phase Alignment Methods

#### FPGA-Side Phase Calibration (AutoSync Equivalent)

Inspired by [TI SLAA643 — Synchronizing Giga-Sample ADCs with Multiple FPGAs](https://www.ti.com/lit/pdf/slaa643):

```verilog
// Phase alignment FSM for multi-AFE synchronization
// After power-up, FPGA injects a known "timestamp" pattern via SYNC
// Then reads back DOUT[0] from all AFEs simultaneously
// Phase difference detected by comparing timestamp edge position

module afe_phase_aligner #(
    parameter N_AFE = 12
)(
    input  wire              clk,
    input  wire [N_AFE-1:0]  dout_lane0,   // LSB of each AFE's output
    output reg  [N_AFE-1:0]  sync_out,
    output reg  [7:0]        phase_tap [N_AFE]  // IDELAY tap values
);
    // 1. Assert SYNC to all AFEs simultaneously
    // 2. Capture DOUT[0] (timestamp bit) from all AFEs
    // 3. Compare captured positions against AFE[0] reference
    // 4. Adjust per-AFE IDELAY to align DCLK phase
    // 5. Verify alignment with training pattern
endmodule
```

#### MCLK Synchronization via Common Source

```
Key rule: All AFE2256 chips must share EXACTLY the same MCLK source
- Use a single oscillator → LVDS/LVCMOS fan-out buffer
- Maximum allowable skew between MCLK at any two AFE inputs: < Tsetup/2

At MCLK = 32 MHz (period = 31.25 ns):
  Maximum MCLK skew: ±7 ns (including PCB trace mismatch, buffer skew)
  Required trace matching: ≤7 ns / 170 ps/mm = 41 mm length mismatch tolerance
```

### 2.4 JESD204B-Inspired Deterministic Latency

For stricter applications requiring frame-exact synchronization:

```
Apply JESD204B Subclass 1 approach to AFE synchronization:
1. SYSREF signal: Common low-frequency pulse (once per frame)
2. All AFEs reset their internal timing generators on SYSREF rising edge
3. Local MCLK divided to frame timing with SYSREF alignment
4. Achieves deterministic, repeatable latency across all chips

Reference: EZParallelSync method (AN165) — all dividers reset via common SYNC pulse
```

---

## 3. Large Panel Readout Architecture

### 3.1 Parallel Column Readout Architecture

For 3072 columns with 12× AFE2256:

```
Panel Column Mapping:
  AFE01: columns    0 –  255  (West edge)
  AFE02: columns  256 –  511
  AFE03: columns  512 –  767
  AFE04: columns  768 – 1023
  AFE05: columns 1024 – 1279
  AFE06: columns 1280 – 1535  (Center-left)
  AFE07: columns 1536 – 1791  (Center-right)
  AFE08: columns 1792 – 2047
  AFE09: columns 2048 – 2303
  AFE10: columns 2304 – 2559
  AFE11: columns 2560 – 2815
  AFE12: columns 2816 – 3071  (East edge)

All 12 AFEs read simultaneously → entire 3072-column row captured in ONE scan cycle
FPGA receives 12 × 4 = 48 LVDS data streams in parallel
```

### 3.2 Data Aggregation Architecture

```
                ┌──────────────────────────────────────────────────────┐
                │                     FPGA                             │
                │                                                      │
  AFE01-04      │  ┌─────────────┐   ┌──────────────┐                │
  (16 LVDS)────►│  │ LVDS RX     │──►│ Deserializer │──┐             │
                │  │ Bank A      │   │ ISERDES ×16  │  │             │
  AFE05-08      │  ├─────────────┤   ├──────────────┤  ▼             │
  (16 LVDS)────►│  │ LVDS RX     │──►│ Deserializer │──►┌──────────┐ │
                │  │ Bank B      │   │ ISERDES ×16  │  │Row Buffer│ │
  AFE09-12      │  ├─────────────┤   ├──────────────┤  │(3072×16b)│ │
  (16 LVDS)────►│  │ LVDS RX     │──►│ Deserializer │──►│  BRAMs   │ │
                │  │ Bank C      │   │ ISERDES ×16  │  └────┬─────┘ │
                │  └─────────────┘   └──────────────┘       │       │
                │                                            ▼       │
                │                              ┌─────────────────┐   │
                │                              │  DDR4 Frame     │   │
                │                              │  Buffer (18 MB) │   │
                │                              │  MIG Controller │   │
                │                              └─────────────────┘   │
                └──────────────────────────────────────────────────────┘
```

### 3.3 FPGA Internal Line Buffer Design

```
Each row = 3072 pixels × 16 bits = 49,152 bits = 6,144 bytes

Xilinx BRAM: 36Kbit = 4,608 bytes (18-bit width mode)
Required BRAMs per row buffer: ⌈6144/4096⌉ = 2 BRAMs (using 18Kbit BRAMs)
  or 1 BRAM per bank section (8× 36Kbit BRAMs for double-buffered row)

Row buffer design: Ping-pong FIFO
  - While BRAM[0] is being written from LVDS receivers (current row)
  - BRAM[1] is being DMA'd to DDR4 (previous row)
  - Swap after row scan complete
```

### 3.4 Throughput Budget Calculation

```
Line time budget (30 fps, 3072 rows):
  Frame period = 1/30 = 33.33 ms
  Vertical blanking allowance = 10% = 3.33 ms
  Active readout time = 30 ms
  Line time = 30 ms / 3072 = 9.77 µs (with blanking included)
  
  Safe target: 8 µs integration + 1.77 µs row transfer = 9.77 µs ✓

DDR4 write bandwidth required:
  3072 × 16 bit per row = 6144 bytes
  Every 9.77 µs: 6144 B / 9.77 µs = 628 MB/s required DDR4 write bandwidth
  
Xilinx Kintex UltraScale DDR4 @ 2400 MHz:
  Peak bandwidth = 2400 MT/s × 8 bytes = 19.2 GB/s >> 628 MB/s ✓
```

---

## 4. NT39565D Dual-STV Gate Driving

### 4.1 Overview

The NT39565D is a TFT gate driver IC supporting large panel applications. For a 3072-row panel:

```
6× NT39565D configuration:
  Each NT39565D drives up to 512 gate lines
  NT39565D[0]: Gate lines    0 –  511 (Top)
  NT39565D[1]: Gate lines  512 – 1023
  NT39565D[2]: Gate lines 1024 – 1535
  NT39565D[3]: Gate lines 1536 – 2047
  NT39565D[4]: Gate lines 2048 – 2559
  NT39565D[5]: Gate lines 2560 – 3071 (Bottom)
```

### 4.2 STV1/STV2 Dual-STV Driving

For a large 3072-row panel, a single STV pulse initiating from one side would require each gate driver to propagate through all 512 stages sequentially. Dual-STV driving splits the panel into two halves:

```
Dual-STV Architecture for 3072-Row Panel:

STV1 → NT39565D[0] → drives rows 0–511 TOP-DOWN
         │
         └─► NT39565D[1] → rows 512–1023 (cascaded, same direction)
         └─► NT39565D[2] → rows 1024–1535

STV2 → NT39565D[5] → drives rows 3071–2560 BOTTOM-UP (reverse direction)
         │
         └─► NT39565D[4] → rows 2559–2048
         └─► NT39565D[3] → rows 2047–1536

Simultaneous STV1+STV2 assertion: Both halves scan inward simultaneously
→ Maximum row activation time halved
→ Reduces ghost image artifacts in fluoroscopy applications
```

#### STV Timing Parameters

```
FPGA-generated STV timing:
  STV pulse width = 1 CPV period (1 gate line time = 9.77 µs at 30fps)
  STV setup before first CPV: ≥0.5 µs (verify with NT39565D datasheet)
  STV to first gate output: ~2 CPV delays (NT39565D internal pipeline)

CPV frequency at 30 fps:
  CPV = 1 / 9.77 µs = 102.4 kHz

FPGA clock for gate timing generation:
  Use dedicated counter with 100 MHz reference: 100 MHz / 977 = CPV ≈ 102.4 kHz
  Error: 100,000,000 / 977 = 102,354 Hz (actual) vs 102,356 Hz (ideal) → <0.002% error
```

### 4.3 Gate Line Capacitance Compensation

For a 430 mm panel with 3072 columns at 140 µm pitch:

```
Gate line capacitance estimation:
  Line length = 430 mm (full column width)
  Column capacitance = ~20 fF/pixel × 3072 pixels = 61.4 pF per gate line
  
  CPV drive capability requirements:
    At 102.4 kHz, Cload = 61.4 pF:
    Peak current = C × dV/dt = 61.4 pF × 40V / 9.77 µs = 252 µA (minimal)
    
    However, transient charging at VGH rise:
    At tr = 0.5 µs (10%–90%): I_peak = C × ΔV / tr = 61.4 pF × 40V / 0.5 µs = 4.9 mA per line
    
  NT39565D output drive: Typically 1–10 mA per output → sufficient ✓
  
  For slower edges (reduce EMI): tr = 2 µs → I_peak = 1.2 mA ✓
```

### 4.4 CPV Clock Distribution (FPGA to Gate ICs)

```
FPGA outputs:
  - CPV (clock for gate shift register): ±3.3V CMOS → matches NT39565D logic
  - STV1, STV2: Separate start-of-frame pulses
  - OE (output enable): Active low, controls VGH/VGL window
  - VGH/VGL: Not generated by FPGA; use dedicated DC-DC converters

Recommended signal levels:
  VGH = +20V to +30V (gate-ON voltage)
  VGL = −5V to −15V (gate-OFF voltage)
  CPV amplitude = 3.3V logic (FPGA → gate driver control input)

FPGA GPIO requirement for gate control:
  6 × (CPV + STV + OE) = 18 GPIO pins minimum
  Plus VGH_EN, VGL_EN, VCOM_ADJ: 3 more = 21 GPIO total
```

---

## 5. FPGA High-Speed LVDS — IDELAY2/ISERDES2

### 5.1 Xilinx 7-Series / UltraScale LVDS Input Architecture

For capturing AFE2256 LVDS outputs, Xilinx provides a hardened receive chain:

```
AFE2256 DOUT_P/DOUT_M
        │
   IBUFDS (differential input buffer)
        │
   IDELAYE2 (programmable delay, 0–31 taps, ~78 ps/tap in 7-series)
        │            │
        │      IDELAYCTRL (calibrates tap values to silicon process/temp/voltage)
        │
   ISERDESE2 (hardened deserializer)
        │  Mode: MASTER+SLAVE cascade for >8:1 ratio
        │  DATA_RATE: "DDR" or "SDR"
        │  DATA_WIDTH: 4, 6, 8, 10, 14 (ISERDESE2 in cascade)
        ▼
   Parallel data bus (8–14 bits wide at FPGA clock rate)
```

From the [Xilinx ISERDESE2 documentation](https://adaptivesupport.amd.com/s/question/0D52E00007G0GEqSAN/regarding-iserdese2-for-high-speed-data-capture):

> **In DDR mode, ISERDESE2 can capture data at rates up to 1600 Mb/s. At 14:1 deserialization, this means data will be clocked into ISERDESE2 at up to 1.6 Gbps.**

### 5.2 LVDS Bit Alignment Procedure (Multi-Channel)

Based on [FPGA Related discussion on IDELAY/ISERDES alignment](https://www.fpgarelated.com/showthread/comp.arch.fpga/124039-1.php) and [IDELAYE2/IOSERDESE2 TDC paper (InspireHEP)](https://inspirehep.net/files/8b87c259088d9f9b9b4883522c3b1466):

#### Stage 1: Bit Alignment (Center the Eye)

```
Dual-IDELAY technique for automatic center-of-eye detection:
  M_DELAY: Master delay for data capture
  S_DELAY: Slave delay = M_DELAY ± 2 taps (oscillating)
  
  Algorithm:
    1. Compare M_sample vs S_sample
    2. If equal: M_DELAY is near center of eye ✓
    3. If not equal: Shift M_DELAY in direction that restores equality
    4. Repeat per channel until all 48 LVDS channels are locked
    5. Store tap values in configuration register for power-on restore

IDELAY2 tap resolution (UltraScale): ~10 ps/tap (vs ~78 ps in 7-series)
UltraScale taps = 512 → 0–5.11 ns range (much finer than 7-series)
```

#### Stage 2: Word/Frame Alignment (Bitslip)

```
ISERDESE2 BITSLIP operation:
  1. AFE outputs known training pattern (e.g., 0xAAAA at startup)
  2. FPGA ISERDES captures pattern
  3. Compare received word against expected
  4. If mismatch: Assert BITSLIP for 1 clock cycle → shifts by 1 serial bit
  5. Repeat up to (deserialization_factor - 1) times until match
  6. Lock and store bitslip count per channel

VHDL/Verilog bitslip controller pseudocode:
  - Training pattern: 0xF0F0 (16-bit alternating)
  - After alignment: Switch to normal data capture mode
  - Re-align if SYNC or lock-loss detected
```

### 5.3 FPGA I/O Bank Planning for 48 LVDS Channels

```
AFE2256 LVDS outputs per chip:
  DCLK_P/M  (1 pair)
  FCLK_P/M  (1 pair = frame clock)
  DOUT_P/M  (4 pairs for 4 ADC outputs)
  Total: 6 differential pairs per AFE

12 AFEs × 6 pairs = 72 differential pairs = 144 single-ended I/O pins

Xilinx XCKU040-1FFVA1156 (Kintex UltraScale):
  HP I/O banks: 4 banks × 52 pairs = 208 differential pair capacity
  72 pairs well within 208 pairs ✓
  
I/O bank allocation:
  Bank 64 (HP, VCCO=1.8V): AFE01–04 LVDS (24 pairs)
  Bank 65 (HP, VCCO=1.8V): AFE05–08 LVDS (24 pairs)
  Bank 66 (HP, VCCO=1.8V): AFE09–12 LVDS (24 pairs)
  Bank 67 (HD): Gate control GPIOs, SPI, UART
  
Note: LVDS receivers use HP banks only (VCCO ≤ 1.8V for LVDS Vcm compliance)
```

### 5.4 Clock Domain Crossing Strategy

```
Clock domains in the system:
  CLK_MCLK   : 32 MHz — AFE master clock (from external TCXO)
  CLK_DCLK[] : 12 × 128 MHz — per-AFE LVDS data clocks (from DCLK_P/M)
  CLK_FPGA   : 200 MHz — FPGA system clock (from MMCM)
  CLK_DDR    : 1200 MHz — DDR4 interface clock
  CLK_AXI    : 100 MHz — AXI interconnect clock

CDC (Clock Domain Crossing) approach:
  1. ISERDES output → async FIFO → CLK_FPGA domain
  2. Row buffer accumulation in CLK_FPGA
  3. DMA write to DDR4 via AXI4 Stream → CLK_AXI → CLK_DDR
  4. MCU interface via AXI4-Lite (CLK_AXI) or PCIe
```

---

## 6. Frame Buffer Architecture — DDR3/DDR4

### 6.1 Frame Buffer Requirements

```
Single frame size = 3072 × 3072 × 16 bit = 18,874,368 bytes ≈ 18 MB
Multi-frame buffer (triple buffering recommended):
  Write buffer: Current frame being captured
  Read buffer:  Frame being transferred to MCU/host
  Spare buffer: Previous frame (artifact comparison, lag correction)
  Total DDR requirement: 3 × 18 MB = 54 MB minimum
  
With dark/offset correction frames: add 2 × 18 MB = 90 MB total
Recommended DDR4 capacity: ≥ 512 MB (1× 512 Mb × 8 = 512 MB component, or DIMM)
```

### 6.2 DDR4 MIG Controller Configuration (Kintex UltraScale)

```
Recommended DDR4 configuration:
  Memory: Micron MT40A512M16LY-062E (8GB) or similar
  Speed grade: DDR4-2400 (1200 MHz)
  Data width: 16-bit (×2 chips for 32-bit bus → 4.8 GB/s)
  or 32-bit bus (×4 chips → 9.6 GB/s)

Required write bandwidth: 628 MB/s (from Section 1.4)
DDR4-2400 × 16-bit: 2400 MT/s × 2 bytes = 4.8 GB/s >> 628 MB/s ✓
Efficiency target: 40–60% bus utilization (safety margin for refresh/ACT cycles)

MIG IP settings:
  Reference clock: 300 MHz (for 1200 MHz DDR operation)
  AXI interface: 256-bit @ 300 MHz = 9.6 GB/s native
  Enable ECC: Optional (adds SECDED capability for medical reliability)
```

### 6.3 DMA Transfer to MCU/Host

Based on [DMA implementations for FPGA-based data acquisition (GSI)](https://indico.gsi.de/event/6233/contributions/28592/attachments/20760/26216/WZab_DMA.pdf):

```
DMA Architecture Options:
┌──────────────────────────────────────────────────────┐
│ Option A: AXI CDMA (Central DMA)                     │
│   - FPGA generates frame-complete interrupt          │
│   - PS ARM initiates AXI CDMA transfer               │
│   - DDR4 → PS DRAM via AXI HP port                   │
│   - Throughput: ~4 GB/s (HP port) → sufficient ✓    │
│   - Latency: ~10 µs per 18 MB frame                  │
└──────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────┐
│ Option B: PCIe DMA (for external CPU/server)         │
│   - Xilinx XDMA IP (AXI-to-PCIe bridge)             │
│   - PCIe Gen2 × 4: 16 Gb/s theoretical              │
│   - Achieved: 14.2 Gb/s (89% efficiency) per GSI    │
│   - CPU DMA buffer: mmap'ed scatter-gather lists     │
│   - CPU overhead: <1% at 60fps/4MB frames (proven)  │
│   - For 18MB/30fps = 540 MB/s: PCIe Gen2×4 ✓        │
└──────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────┐
│ Option C: 10G Ethernet (RDMA / RASHPA)               │
│   - RDMA-based data push (no CPU in critical path)  │
│   - Used in Medipix3RX FPGA systems at ESRF         │
│   - 10 GbE: 1.25 GB/s theoretical                   │
│   - 540 MB/s fits within 10G with margin ✓           │
│   - Higher latency than PCIe (~ms vs µs)             │
└──────────────────────────────────────────────────────┘

Recommended for FPD: Option A (Zynq SoC, integrated ARM) or Option B (dedicated PCIe card)
```

### 6.4 Frame Buffer State Machine

```verilog
// Simplified frame buffer FSM (conceptual)
// Triple-buffer scheme with frame-complete handshake

typedef enum {WAIT_ROW, CAPTURE_ROW, ROW_DONE, FRAME_DONE} state_t;

// Buffer assignment:
//   buf_wr_ptr: FPGA is writing here (current frame)
//   buf_rd_ptr: MCU is reading here (last complete frame)
//   buf_spare:  Spare / lag correction frame

// On FRAME_DONE:
//   1. Assert frame_rdy interrupt to MCU
//   2. Swap buf_wr_ptr ← spare
//   3. MCU reads buf_rd_ptr via DMA
//   4. On MCU DMA complete: swap buf_rd_ptr ← previous buf_wr_ptr
```

---

## 7. High Frame Rate Fluoroscopy — 30fps Budget

### 7.1 Frame Timing Architecture

```
Frame period = 33.33 ms (30 fps)

┌───────────────────────────────────────────────────────────────────────┐
│                         FRAME N (33.33 ms)                            │
├──────────────────────────────────┬───────────────────┬────────────────┤
│   Gate Scan + AFE Readout        │  Vertical Blank   │  X-ray Pulse   │
│   3072 rows × 9.77 µs = 30 ms   │     2.33 ms       │  (overlap w/   │
│                                  │                   │   integration) │
└──────────────────────────────────┴───────────────────┴────────────────┘

Line-level timing (9.77 µs total):
┌────────────────────────────────────────────────────────────────────┐
│ Gate ON (VGH)   │ Integration (8 µs) │ Gate OFF│ AFE Scan (7.69µs)│
│    ~1 µs        │   pixel charging   │  ~1 µs  │  LVDS output     │
└────────────────────────────────────────────────────────────────────┘
Note: AFE2256 pipelined: scan overlaps with NEXT row's integration
```

### 7.2 Pipelined Integration-and-Read

The AFE2256 supports **pipelined Integrate-and-Read** — data from row N is being serialized while row N+1 is integrating:

```
Timeline (pipelined mode):
  Row N:   [──INTEGRATE──][──SCAN OUT──]
  Row N+1:              [──INTEGRATE──][──SCAN OUT──]
  Row N+2:                            [──INTEGRATE──][──SCAN OUT──]
  
This means:
  - Gate line N+1 is activated BEFORE AFE has finished outputting row N
  - AFE's dual-banking CDS (correlated double sampler) enables this
  - FPGA must manage line timing precisely: gate pulse, CDS timing, SYNC
```

### 7.3 CareRay 1800RF-II Frame Rate Validation

From CareRay product specifications (fluoroscopy mode):

| Resolution | Binning | Frame Rate |
|------------|---------|-----------|
| 3072×3072 | 1×1 | 3 fps |
| 1536×1536 | 2×2 | 12 fps |
| 1024×1024 | 3×3 | 30 fps |
| 768×768 | 4×4 | 32 fps |
| 3072×3072 ROI | 2×2 | 25–30 fps |

> **Note:** 30 fps at full 3072×3072 requires hardware binning or ROI mode in current commercial implementations. Achieving 30 fps at native resolution requires AFE scan time <8 µs.

### 7.4 Throughput Summary Table

| Metric | Value |
|--------|-------|
| Frame size | 18,874,368 bytes (18 MB) |
| Frame rate | 30 fps |
| Raw data throughput | 566 MB/s |
| Line time budget | 9.77 µs per row |
| AFE MCLK required | ≥32 MHz |
| LVDS aggregate rate | ~12 Gbps (48 pairs) |
| DDR4 write bandwidth | 628 MB/s required |
| PCIe/MCU transfer | 540 MB/s sustained |

---

## 8. Power Sequencing for Large Panels

### 8.1 AFE2256 Power-Up Sequence

The AFE2256 uses two supply rails:

```
Required power-up order (AFE2256):
  1. AVDD1 = 1.85V (core analog supply) — ramp first
  2. AVDD2 = 3.3V (output/interface supply) — ramp after AVDD1 stable
  3. Wait: ≥ 1 ms for internal reference to stabilize
  4. Assert SPI configuration: PGA gain, full-scale range, power mode
  5. Apply MCLK (32 MHz) — must be stable before SYNC assertion
  6. Begin SYNC cycling

Power-down order: Reverse sequence
  1. De-assert SYNC
  2. Remove MCLK
  3. Set nap mode via SPI (reduces supply current to near zero)
  4. Ramp down AVDD2
  5. Ramp down AVDD1
```

### 8.2 Gate IC (NT39565D) Power-Up Sequence

Based on general TFT gate driver power-up requirements ([Patent US8599182B2](https://patents.google.com/patent/US8599182B2/en), [PCB Artists VGH/VGL guide](https://pcbartists.com/design/power-supply/vgh-vgl-vcom-avdd-voltage-generation-schematic-tft-lcd/)):

```
CRITICAL RULE: VGL must be present BEFORE VGH is applied
(If VGH arrives before VGL, latch-up damage to gate driver TFTs can occur)

Recommended gate IC power-up sequence:
  T=0 ms:    Apply GND, DVDD (3.3V logic supply to NT39565D)
  T=5 ms:    Ramp VGL (−10V to −15V) — ensure stable before VGH
  T=10 ms:   Ramp VGH (+20V to +30V) — AFTER VGL is fully established
  T=15 ms:   Assert CPV clock at low frequency (test mode)
  T=20 ms:   Assert STV1 to begin gate scan
  
  Power-down sequence (reverse):
  1. De-assert STV (hold all gates at VGL)
  2. Stop CPV clock
  3. Ramp down VGH slowly (< 5 V/ms to prevent inductive spikes)
  4. Ramp down VGL
  5. Remove DVDD

VGL-before-VGH timing margin: ≥5 ms (silicon process-dependent)
```

### 8.3 Inrush Current Management

```
Inrush current sources:
  1. VGH charge into gate line capacitance:
     C_gate = 3072 columns × 30 fF/crossing × 12 = 1.1 nF (TFT gate-to-source)
     Additional decoupling capacitors on gate IC supply: 10–100 µF typical
     I_inrush = C × dV/dt = 100 µF × 25V / 1 ms = 2.5 A peak → need soft-start

  2. AFE2256 AVDD1 inrush:
     12 chips × estimated 100 µF per chip = 1.2 mF total
     At 1.85V / 1 ms: I_inrush = 1.2 mF × 1.85V / 1 ms = 2.2 A

  3. FPGA core VCC inrush:
     Kintex UltraScale: Ccore ≈ 2–10 mF depending on fabric utilization
     
Mitigation:
  - Use TPS54620/TPS65150 or similar multi-rail PMIC with programmable slew rate
  - Soft-start time: ≥ 5 ms for VGH rail
  - Inrush limiting: Use NTC thermistor (10Ω, 3A) in series with VGH before steady-state bypass relay
  - Sequencing controller: TI TPS3706 or MachXO CPLD-based sequencer
```

### 8.4 Power Domain Isolation

```
Power domain diagram:
  ┌─────────────────────────────────────────────────────────┐
  │              System Power Architecture                  │
  │                                                         │
  │  24V/12V Input DC                                       │
  │       │                                                 │
  │  ┌────▼──────┐  ┌──────────────┐  ┌────────────────┐  │
  │  │ Gate IC   │  │ AFE Supply   │  │ FPGA Supply    │  │
  │  │ Supplies  │  │ Module       │  │ Module         │  │
  │  │ +VGH +28V │  │ AVDD1 1.85V │  │ VCCINT 0.95V  │  │
  │  │ -VGL -12V │  │ AVDD2 3.3V  │  │ VCCO_HP 1.8V  │  │
  │  │ DVDD 3.3V │  │ 12 × AFE   │  │ VCCO_HD 3.3V  │  │
  │  └───────────┘  └──────────────┘  │ VCCO_MGT 1.8V│  │
  │                                   └────────────────┘  │
  │  Sequence controller: CPLD or TI TPS3706               │
  └─────────────────────────────────────────────────────────┘
```

---

## 9. Thermal Management

### 9.1 AFE2256 Power Dissipation Estimation

The AFE2256 datasheet lists low power with nap/power-down modes (exact mW values are proprietary/NDA). Based on similar 256-channel AFE ICs in the industry:

```
Estimated AFE2256 power consumption (active mode):
  Analog core (256 integrators + CDS): ~50 mW typical
  4× 16-bit SAR ADC at 32 MHz: ~20 mW (5 mW each)
  LVDS output drivers: ~12 mW (4 pairs × 3 mW each)
  Total per chip: ~80 mW estimated active power

12 AFE2256 chips × 80 mW = ~960 mW = ~1 W total for all AFEs

Note: In pipelined mode at high frame rate, analog power scales with scan rate
At 30 fps (active duty ~90%): ~900 mW for 12 AFEs
```

### 9.2 FPGA Power Budget (Kintex UltraScale XCKU040)

```
FPGA power breakdown (estimated using Xilinx Power Estimator):
  
  Static power (VCCINT + VCCAUX + VCCO): ~500 mW
  
  Dynamic power:
  ├─ LVDS input logic (48 channels × ISERDES × IDELAY):
  │    48 channels × ~3 mW/LVDS = 144 mW
  ├─ Data path logic (deserializers, FIFOs, alignment):
  │    ~200 mW at 200 MHz
  ├─ DDR4 MIG controller:
  │    ~300 mW (memory interface + PHY)
  ├─ Gate driver timing controllers:
  │    ~50 mW
  └─ AXI DMA + SPI + control logic:
       ~100 mW
  
  Total dynamic: ~794 mW
  Total FPGA power: ~1.3 W (static + dynamic)
  
  Junction temperature estimate:
  Tj = Ta + P × θJA
  For XCKU040-1FFVA1156 (35×35 mm BGA):
    θJA (still air) ≈ 7.3°C/W
    Tj = 25°C + 1.3W × 7.3 = 34.5°C (well within 100°C limit) ✓
```

### 9.3 System Thermal Budget

```
Total system power dissipation:
  AFEs (12×): ~1 W
  Gate ICs (6×): ~500 mW (VGH/VGL switching loss)
  FPGA: ~1.3 W
  DDR4 memory: ~500 mW (2×chips at 1.2V/2400MT/s)
  Gate IC power supplies (VGH/VGL efficiency ~80%): +750 mW losses
  AFE linear regulators: +200 mW losses
  
  Total: ~4.25 W system power dissipation
  
Thermal management requirements:
  PCB: 4-layer minimum with dedicated power planes
  Heatsink: 50×50 mm aluminum (θHS ≈ 5°C/W) for FPGA
  Case temperature target: ≤ 45°C
  Optional: Thermoelectric cooler (Peltier) for scintillator temperature stability
  
From [Control of temperature of FPD patent (US20060076500A1)](https://patents.google.com/patent/US20060076500A1/en):
  FPD scintillator temperature stability: ±2°C recommended
  Operation range: 15°C – 35°C ambient
  Temperature monitoring: NTC thermistor on scintillator layer + FPGA ADC readback
```

---

## 10. Error Detection and Recovery

### 10.1 LVDS Bit Error Detection

#### Built-in Self-Test (BIST) Pattern Method

```
Based on [Parallelized FPGA Architecture PMC Sensors 2025](https://pmc.ncbi.nlm.nih.gov/articles/PMC11723343/):

Implementation:
  1. At startup: Send known PRBS-7 or alternating 0xAAAA/0x5555 training pattern
  2. FPGA expects known sequence → compare received vs expected
  3. Error counter increments on mismatch
  4. Bit Error Rate (BER) threshold: >1e-12 triggers IDELAY re-alignment
  
For high-speed channels (Timepix4 at 5.12 Gbps benchmark):
  PRBS-31 tests showed BER < 1e-14 in well-designed systems
  For AFE LVDS at 128 MHz: BER typically <1e-15 (standard LVDS operating range)
```

#### Runtime Error Detection Strategy

```
runtime_lvds_monitor module:
  - Monitor each LVDS lane for:
    a) Symbol errors: Unexpected bit transitions in fixed patterns
    b) Clock loss: DCLK missing for >2 line periods → assert SYNC_LOSS flag
    c) Data valid window: Check DOUT transitions only within expected timing window
    
  - On error detected:
    a) Assert ERROR_FLAG to MCU via interrupt
    b) Tag affected frame with ERROR_HEADER in metadata
    c) Do NOT corrupt frame buffer — preserve last valid frame
    d) Attempt re-alignment: reassert SYNC after 1 frame period
    e) Log error count to SPI register map for diagnostics
```

### 10.2 AFE Timeout / Fault Detection

```
AFE fault conditions:
  1. DCLK missing: FPGA watchdog timer per AFE — 2× expected DCLK period timeout
  2. DOUT stuck high/low: >16 consecutive identical 16-bit words → suspect fault
  3. ADC overflow: MSB stuck at '1' or '0' for all pixels in region → scintillator fault
  4. SPI read-back failure: AFE register values don't match programmed values
  
Recovery actions:
  Priority 1 (soft reset): Re-send SPI configuration, re-assert SYNC
  Priority 2 (hard reset): Power-cycle individual AFE AVDD rails (requires per-chip control)
  Priority 3 (fault isolation): Disable affected AFE, flag corresponding pixel columns as invalid
  Priority 4 (system halt): If >2 AFEs fail → stop imaging, alert operator

AFE health register (FPGA):
  reg [11:0] afe_status;  // bit per AFE: 1=OK, 0=fault
  reg [11:0] afe_timeout_counter [12];
  // Exported to MCU via SPI status register
```

### 10.3 Gate IC Fault Detection

```
NT39565D fault indicators:
  1. Gate output stuck: All pixels in a row unresponsive → gate line open/short
  2. VGH voltage collapse: Monitor VGH with ADC → <18V threshold → fault
  3. Thermal shutdown: NT39565D internal thermal sense → mirror to FPGA
  
Detection method:
  - Dark frame analysis: After X-ray acquisition, scan frame for entire row = 0 or row = 65535
  - Gate IC self-test mode (if available): Send test CPV sequence, verify output via test probe
  - Power monitor ADC: Sample VGH rail every 100 ms (1 sample per ~3000 frames)
```

### 10.4 System-Level Error Hierarchy

```
Error Level Classification:
  ┌────────────────────────────────────────────────────────────┐
  │ Level 0 (Warning): BER > 1e-12 but < 1e-9                 │
  │   Action: Log, continue acquisition, flag frame           │
  ├────────────────────────────────────────────────────────────┤
  │ Level 1 (Recoverable): LVDS lock loss on 1 channel        │
  │   Action: Pause frame, re-align IDELAY, resume            │
  ├────────────────────────────────────────────────────────────┤
  │ Level 2 (Partial Fault): 1 AFE fails, >1 gate row fails   │
  │   Action: Disable AFE, mask pixels, continue degraded     │
  ├────────────────────────────────────────────────────────────┤
  │ Level 3 (Critical): >2 AFEs fail, VGH collapse, FPGA PLL  │
  │   Action: Emergency stop, preserve last valid frame,      │
  │            alert operator, require manual restart         │
  └────────────────────────────────────────────────────────────┘
```

---

## 11. State-of-the-Art FPGA Designs for FPD

### 11.1 FPGA Selection Comparison for Medical X-ray FPD

| Feature | Xilinx Kintex-7 | Xilinx Kintex UltraScale | Xilinx UltraScale+ | Intel Arria 10 | Intel Stratix 10 |
|---------|-----------------|--------------------------|--------------------|--------------|--------------| 
| Process | 28 nm | 20 nm | 16 nm FinFET | 20 nm | 14 nm FinFET |
| Logic Cells | 326K | 725K | 1,143K | 1,150K | 5,510K |
| BRAM | 27 Mb | 67 Mb | 75 Mb | 53 Mb | 229 Mb |
| LVDS I/O pairs | 300+ | 600+ | 600+ | 600+ | 1,100+ |
| DDR4 support | ✗ (DDR3 only) | ✓ (DDR4-2400) | ✓ (DDR4-2666) | ✓ | ✓ |
| FPGA power | ~3W typ | ~5W typ | ~7W typ | ~8W | ~20W |
| Medical imaging | ✓ | ✓✓ (recommended) | ✓✓✓ | ✓ | ✓✓ |
| Price tier | $$ | $$$ | $$$$ | $$$ | $$$$$ |

**Recommended for this design: Xilinx Kintex UltraScale XCKU040 or XCKU060**

Sources: [AMD Kintex UltraScale product page](https://www.amd.com/en/products/adaptive-socs-and-fpgas/fpga/kintex-ultrascale.html), [NASA Kintex UltraScale characterization](https://nepp.nasa.gov/docs/tasks/041-FPGA/NEPP-TR-2019-Berg-TR-15-061-Xilinx-XCKU040-2FFVA1156E-KintexUltraScale-LBNL-2019Nov18-20205007765.pdf)

### 11.2 State-of-the-Art Academic Benchmarks

| System | FPGA | Detector | Frame Rate | Data Rate |
|--------|------|----------|------------|-----------|
| CSNS Timepix4 readout (2026) | Zynq XCZU15EG | 6.94 cm² hybrid pixel | Variable | 80 Gbps (16 channels) |
| ESRF Medipix3RX (2020) | Kintex/Ultrascale | Medipix3RX (8-chip) | High-rate | DDR4 frame buffer |
| HiZ-GUNDAM X-ray CMOS (2024) | Kintex-7 XC7K325T | CMOS imager | Satellite | 128 MB DDR3L |
| Berkeley Lab multichannel (2023) | Custom | Radiation detector | Variable | LVDS → Ethernet |
| SPIE ATRC X-ray readout (2026) | Multi-FPGA | CCD (multi-channel) | High speed | Parallelized CCD |

Sources: [Timepix4 CSNS readout (arXiv 2026)](https://arxiv.org/html/2603.09499v1), [HiZ-GUNDAM CMOS readout (arXiv 2024)](https://arxiv.org/html/2403.10409v1), [ESRF RASHPA FPGA (arXiv 2020)](https://arxiv.org/pdf/2010.15450), [SPIE X-ray FPGA (SPIE 2026)](https://spie.org/astronomical-telescopes-instrumentation/presentation/The-high-speed-FPGA-readout-system-for-the-Advanced-X/14146-244)

### 11.3 Zynq UltraScale+ MPSoC Option

For an integrated FPGA+ARM+MCU solution:

```
Zynq UltraScale+ XCZU7EV or XCZU9EG:
  - PL (FPGA fabric): Equivalent to Kintex UltraScale+
  - PS (ARM): Quad Cortex-A53 @1.3 GHz + Dual Cortex-R5
  - DDR4: Native PS-side DDR4 controller
  - PCIe Gen2×4 hardened
  - Video codec units: H.265 hardware encoder (useful for fluoroscopy preview)

Advantage: Eliminates separate MCU
Disadvantage: Higher cost, more complex software stack (Linux or RTOS required)
```

---

## 12. FPGA Resource Estimates

### 12.1 Resource Utilization Estimate (XCKU040)

| Function | LUTs | FFs | BRAM (36K) | DSP | Notes |
|----------|------|-----|------------|-----|-------|
| 48× LVDS ISERDES | 2,400 | 3,840 | 0 | 0 | 50 LUT/ch |
| 48× IDELAY controllers | 1,200 | 960 | 0 | 0 | 25 LUT/ch |
| 48× bit-alignment FSMs | 4,800 | 4,800 | 0 | 0 | 100 LUT/ch |
| Row buffer (3×3072×16b) | 0 | 0 | 18 | 0 | 3×(2×36Kb BRAMs) |
| SYNC generator + gate timing | 500 | 400 | 2 | 0 | CPV, STV1/2, OE |
| DDR4 MIG controller | 8,000 | 12,000 | 12 | 0 | Xilinx MIG IP |
| AXI DMA + interconnect | 5,000 | 6,000 | 4 | 0 | AXI CDMA IP |
| Error detection logic | 1,000 | 2,000 | 2 | 0 | Watchdogs, counters |
| SPI master (AFE config) | 500 | 300 | 1 | 0 | 12 CS lines |
| Data path (alignment, packing) | 3,000 | 4,000 | 4 | 4 | Bit/word alignment |
| **Total estimate** | **~26,400** | **~34,300** | **~43** | **4** | |
| XCKU040 capacity | 243,000 | 487,200 | 600 | 1,920 | |
| **Utilization %** | **~11%** | **~7%** | **~7%** | **<1%** | **Well within limits** |

> The design fits comfortably in XCKU040. XCKU025 (lower cost, smaller) might also suffice.

### 12.2 Timing Closure Estimates

```
Critical timing paths:
  1. ISERDES DCLK → output parallel data: <1.5 ns (FPGA internal, well within 200 MHz CLK_FPGA)
  2. IDELAY tap update → data valid: ~1.5 cycles at 200 MHz (calibration path, non-critical)
  3. Row buffer write → DDR4 AXI: 3 cycle AXI pipeline at 300 MHz = 10 ns (acceptable)
  4. Gate timing CPV: 100 MHz dedicated counter → <1 ns setup slack at 100 MHz
  
No timing violations expected in nominal -1 speed grade device.
Use -2 speed grade for safety margin in industrial temperature range.
```

---

## 13. Best-Practice Design Patterns Summary

### 13.1 Multi-AFE Synchronization

```
✅ DO:
  - Use single TCXO as master clock source for ALL AFE chips
  - Fan-out MCLK and SYNC via matched LVDS/LVCMOS buffers
  - Implement IDELAY-based per-channel phase calibration at startup
  - Use pipelined Integrate-and-Read to maximize throughput
  - Apply JESD204B-style deterministic latency concepts (SYSREF)
  
❌ AVOID:
  - Daisy-chaining SYNC for parallel readout
  - Using different clock sources per AFE
  - Long unmatched PCB traces for MCLK/SYNC (max skew: ±7 ns at 32 MHz)
  - Asserting SYNC before MCLK is stable
```

### 13.2 FPGA LVDS Receiver Design

```
✅ DO:
  - Use HP I/O banks (VCCO ≤ 1.8V) for LVDS receivers
  - Implement dual-IDELAY technique for eye-center detection
  - Use training pattern + BITSLIP for word alignment
  - Add per-channel BIST with known patterns for production test
  - Use IDELAYCTRL to calibrate IDELAY taps (auto-corrects for PVT variation)
  
❌ AVOID:
  - Using HD I/O banks for LVDS (they don't support ISERDESE2 in Kintex UltraScale)
  - Ignoring clock domain crossing between DCLK and system clock
  - Assuming all 12 DCLK phases are aligned (they are NOT — calibrate each)
```

### 13.3 Frame Buffer and DMA

```
✅ DO:
  - Triple-buffer frame memory to avoid write/read conflicts
  - Size DDR4 for ≥90 MB (5× frame = capture + display + 3 dark/offset)
  - Use AXI CDMA with interrupt-driven MCU DMA for lowest CPU overhead
  - Implement frame metadata header (timestamp, error flags, frame count)
  - Enable DDR4 ECC for medical-grade reliability
  
❌ AVOID:
  - Single-buffer scheme (deadlock if MCU reads too slowly)
  - Polling-based DMA (wastes CPU cycles; use interrupt + DMA scatter-gather)
  - Bypassing the MIG calibration phase (never skip CALIB_DONE check)
```

### 13.4 Power Sequencing

```
✅ DO:
  - VGL before VGH (≥5 ms margin) — non-negotiable for gate IC protection
  - Soft-start all high-voltage rails (dV/dt ≤ 5 V/ms)
  - Monitor VGH voltage with ADC; halt imaging if VGH < 18V
  - Use independent enable signals per power domain (FPGA → PMIC)
  
❌ AVOID:
  - VGH ON before VGL is stable (causes CMOS latch-up in gate IC)
  - Simultaneous power-up of all rails (inrush current spike)
  - Floating MCLK during AFE power-up (internal state machine may latch wrong state)
```

### 13.5 Thermal Management

```
✅ DO:
  - Mount AFE2256 chips on dedicated copper pour (thermal relief to PCB edge)
  - Use Peltier cooler for scintillator temperature stability (±2°C)
  - Monitor FPGA die temperature via XADC (UltraScale internal sensor)
  - Implement thermal throttling: reduce frame rate if FPGA Tj > 80°C
  
❌ AVOID:
  - Ignoring scintillator temperature (CsI:Tl sensitivity varies 0.3%/°C)
  - Placing FPGA and AFEs without airflow path
  - Operating without temperature monitoring in enclosed detector housing
```

### 13.6 Error Handling Design Pattern

```
Recommended fault-tolerant readout pattern:
  1. Startup calibration: IDELAY scan + bitslip alignment + BER test
  2. Normal operation: Continuous BIST pattern check (1% of frames)
  3. Soft fault: Re-align affected channel(s) without stopping acquisition
  4. Hard fault: Tag frame as invalid; do NOT write to DDR4 until resolved
  5. Persistent fault: Assert ERROR_LATCH; halt imaging; wait MCU intervention
  6. Recovery log: Maintain error history in BRAM for post-analysis
```

---

## 14. References

1. **AFE2256 Product Page and Datasheet** — Texas Instruments  
   https://www.ti.com/product/AFE2256 | https://www.mouser.com/ds/2/405/afe2256-748911.pdf

2. **AFE2256 Timing Questions (SYNC, MCLK)** — TI E2E Forum (2019)  
   https://e2e.ti.com/support/data-converters-group/data-converters/f/data-converters-forum/789162/afe2256-timing-questions-in-afe2256

3. **AN165: Multi-Part Clock Synchronization Methods for Large Data Converter Systems** — Analog Devices (Linear Technology)  
   https://www.analog.com/media/en/technical-documentation/application-notes/an165fa.pdf

4. **SLAA643: Synchronizing Giga-Sample ADCs Interfaced with Multiple FPGAs** — Texas Instruments  
   https://www.ti.com/lit/pdf/slaa643

5. **Bit Alignment & Bit Slipping in FPGAs — ISERDESE2 Tutorial** — YouTube (Dr. Paul Kerstetter, 2025)  
   https://www.youtube.com/watch?v=kANqFPkTjvM  
   GitHub: https://github.com/pkerstetter/BitSlip

6. **General-Purpose Data Streaming FPGA TDC Synchronized by IDELAYE2/IOSERDESE2** — InspireHEP (2025)  
   https://inspirehep.net/files/8b87c259088d9f9b9b4883522c3b1466

7. **Regarding ISERDESE2 for High Speed Data Capture** — AMD Adaptive Support (2019)  
   https://adaptivesupport.amd.com/s/question/0D52E00007G0GEqSAN

8. **High Speed LVDS Deserializing Discussion** — Reddit r/FPGA (2025)  
   https://www.reddit.com/r/FPGA/comments/1jqkmh7/high_speed_lvds_deserilizing/

9. **DMA Implementations for FPGA-Based Data Acquisition Systems** — GSI/SPIE Wilga 2017  
   https://indico.gsi.de/event/6233/contributions/28592/attachments/20760/26216/WZab_DMA.pdf

10. **Parallelized FPGA Architecture for UWB Radar** — PMC/Sensors (January 2025)  
    https://pmc.ncbi.nlm.nih.gov/articles/PMC11723343/

11. **FPGA-Based Real-Time Image Manipulation and Advanced Data Acquisition** (RASHPA/Medipix3RX) — arXiv (2020)  
    https://arxiv.org/pdf/2010.15450

12. **Development of Readout Electronics for Timepix4 (CSNS)** — arXiv (March 2026)  
    https://arxiv.org/html/2603.09499v1

13. **High-Speed Readout of X-ray CMOS Image Sensor (HiZ-GUNDAM)** — arXiv (March 2024)  
    https://arxiv.org/html/2403.10409v1

14. **AMD Kintex UltraScale FPGAs Product Page** — AMD/Xilinx  
    https://www.amd.com/en/products/adaptive-socs-and-fpgas/fpga/kintex-ultrascale.html

15. **Kintex UltraScale FPGA Single Event Effects Study** — NASA NEPP (2019)  
    https://nepp.nasa.gov/docs/tasks/041-FPGA/NEPP-TR-2019-Berg-TR-15-061-Xilinx-XCKU040-2FFVA1156E-KintexUltraScale-LBNL-2019Nov18-20205007765.pdf

16. **ADI Medical X-Ray Imaging Solutions** — Analog Devices  
    https://www.analog.com/media/cn/technical-documentation/apm-pdf/adi-medical-x-ray-imaging-solutions_en.pdf

17. **Power Sequence Control Circuit for Gate Driver** — Patent US8599182B2  
    https://patents.google.com/patent/US8599182B2/en

18. **Control of Temperature of Flat Panel Type Radiation Detector** — Patent US20060076500A1  
    https://patents.google.com/patent/US20060076500A1/en

19. **CareView 1800RF-II Fluoroscopy FPD** — CareRay Digital Medical Technologies  
    https://careray.com/products/fluoroscopy/

20. **AXI DMA for High-Speed Data Streaming** — Analog Devices EZ Blog (2025)  
    https://ez.analog.com/ez-blogs/b/engineering-mind/posts/how-to-boost-fpga-dsp-with-axi-dma-for-high-speed-data-streaming

21. **Multichannel Radiation Detector Electronics with Real-Time DSP** — Berkeley Lab IPO (2023)  
    https://ipo.lbl.gov/2023/11/09/multichannel-radiation-detector-electronics-with-real-time-digital-signal-processing/

22. **Solid-State Fluoroscopic Imager for High-Resolution Angiography** — PMC Medical Physics (1997)  
    https://pmc.ncbi.nlm.nih.gov/articles/PMC4280188/

23. **How to Synchronize Clocks Between Multiple MMCM** — AMD Adaptive Support  
    https://adaptivesupport.amd.com/s/question/0D52E00006iHsOSSA0

24. **FPGA Based Novel High Speed DAQ System with Error Correction** — arXiv (2015)  
    https://arxiv.org/pdf/1507.01777

25. **A Compact and Highly Integrated 128-Channel FPGA-Based Readout for Nuclear Imaging** — ScienceDirect (2024)  
    https://www.sciencedirect.com/article/pii/S0168900224003784

---

*Research compiled: March 18, 2026. Data from academic papers, manufacturer datasheets, application notes, and engineering forums. Throughput calculations and power estimates are based on known specifications and engineering extrapolation where proprietary data is unavailable.*
