# Gate IC Control Algorithms for X-ray Flat Panel Detectors
## Research Report: NV1047 / NT39565D Type Gate Drivers

**Date:** March 18, 2026  
**Scope:** Gate driver IC operation, scan algorithms, timing optimization, FPGA FSM design

---

## Table of Contents

1. [Gate Driver IC Architecture and Operation](#1-gate-driver-ic-architecture-and-operation)
2. [Gate Pulse Timing Optimization](#2-gate-pulse-timing-optimization)
3. [Pre-charge and Reset Sequence](#3-pre-charge-and-reset-sequence)
4. [Bidirectional Scanning](#4-bidirectional-scanning)
5. [Panel Reset Algorithm](#5-panel-reset-algorithm)
6. [Partial Area Readout (ROI Scanning)](#6-partial-area-readout-roi-scanning)
7. [NT39565D / Novatek Family Specifics](#7-nt39565d--novatek-family-specifics)
8. [Gate Drive Voltage Optimization (VGG / VEE)](#8-gate-drive-voltage-optimization-vgg--vee)
9. [Multi-chip Cascade Synchronization](#9-multi-chip-cascade-synchronization)
10. [FPGA Control FSM Design Patterns](#10-fpga-control-fsm-design-patterns)
11. [Complete Timing Reference Tables](#11-complete-timing-reference-tables)
12. [Algorithm Pseudocode Library](#12-algorithm-pseudocode-library)
13. [Design Implications for SystemVerilog Implementation](#13-design-implications-for-systemverilog-implementation)
14. [References and Sources](#14-references-and-sources)

---

## 1. Gate Driver IC Architecture and Operation

### 1.1 Overview

TFT-LCD gate driver ICs (e.g., Novatek NT39207, NT52002, NT52001, and the NT39565D family) are dedicated shift-register-based ICs used in both display and X-ray flat-panel detector (FPD) panels. In an X-ray FPD, they serve to sequentially activate each row of TFT switches, allowing stored charge from photodiode pixels to transfer to readout amplifiers on the data lines.

**Key sources:**
- [NT39207 Datasheet (Orient Display)](https://www.orientdisplay.com/wp-content/uploads/2022/08/NT39207_v0.11.pdf)
- [NT52002 Datasheet (Phoenix Display)](https://www.phoenixdisplay.com/wp-content/uploads/2015/07/NT52002.pdf)
- [NT52001 Datasheet (Scribd)](https://www.scribd.com/document/555334122/NT52001-Novatek)

### 1.2 Shift Register Core

The gate IC operates as a multi-stage shift register. A **start pulse (STV — Start Vertical)** is clocked in at the first stage; subsequent rising edges of the shift clock (CLK/CPV) propagate the active state one row at a time. The output of each register stage directly drives the gate line of one TFT row.

**Signal names by IC family:**

| Function | NT39207 / NT52002 / NT52001 |
|---|---|
| Start pulse (down scan) | STVD (input when U_D=L) |
| Start pulse (up scan) | STVU (input when U_D=H) |
| Shift clock | CLKR / CLKL (internally shorted) |
| Scan direction | U_DR / U_DL (H=up, L=down) |
| All-gates-ON | XON (active low) |
| Output enable | OE1/OE2/OE3 (active high = disable) |
| Row outputs | G1 ~ Gn (amplitude = VGG − VEE) |
| Cascade out | STVU (to next IC's STVD when U_D=H) |

### 1.3 Shift Register Waveform Description

```
                 STV
                  |
    CLK: __|‾|__|‾|__|‾|__|‾|__|‾|__
                    ↑   ↑   ↑   ↑
    G1:  ___|‾‾|_________________________   (active for 1 CLK period)
    G2:  ________|‾‾|___________________
    G3:  ____________|‾‾|_______________
    G4:  ________________|‾‾|___________
    ...
    Gn:  ________________________________|‾‾|___
    STVU_out: ______________________________|‾|__ (emitted at end)
```

- Each row's gate is HIGH for exactly one CLK period (one "line time").
- Gate output amplitude is **VGG − VEE** (e.g., 25V − (−15V) = 40 V swing).
- All inactive rows remain at VEE (TFT fully OFF).

### 1.4 CPV / CLK Signal

- CPV (Clock Pulse Vertical) is the main shift clock to the gate ICs.
- In the NT39565D context, **CPV** takes the role of the CLK signal.
- Clock frequency: ≤200 kHz in cascade mode (per NT52002 spec).
- Clock duty cycle: typically 50% (PWCLK_H = PWCLK_L ≥ 500 ns each).

### 1.5 Bootstrap Circuits in Gate Drivers

The output stage of gate driver ICs must swing from VEE (typically −10 to −15 V) to VGG (typically +20 to +30 V). The internal level-shift and output stage use bootstrap techniques:

- **Bootstrap capacitor (CBOOT):** When the gate output is LOW (VEE), a bootstrap capacitor charges from the VGG supply through a diode. When transitioning HIGH, the stored charge on CBOOT lifts the gate voltage above VGG for overdrive.
- This is the same principle used in high-side MOSFET drivers: the "flying" capacitor follows the output, ensuring full VGH drive even against the load capacitance of the gate line.
- In integrated gate-driver-on-array (GOA) solutions (as found in some IGZO panels), bootstrap nodes (labeled "PU") are charged during the pre-charge phase, then the CLK swing is bootstrapped to drive the output to full VGG.

**Reference:** [Dual-bootstrapping gate driver (ScienceDirect)](https://www.sciencedirect.com/science/article/abs/pii/S0141938224001367)  
**Reference:** [High-Speed Shift Register with Dual-Gated TFTs (PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC9610482/)

---

## 2. Gate Pulse Timing Optimization

### 2.1 Minimum Gate-ON Time for Complete Charge Transfer

When the gate line goes HIGH (VGG), the TFT switches ON and stored pixel charge flows through the TFT onto the data (drain) line toward the charge-sensitive amplifier (CSA). The charge transfer is an RC exponential:

```
V_pixel(t) = V_data × [1 − exp(−t / (Ron × Cpixel))]
```

Where:
- **Ron** = TFT on-resistance (function of VGS − Vth)
- **Cpixel** = Storage capacitor + liquid crystal / photodiode capacitance (typically 150–250 fF for X-ray pixels)
- **V_data** = target data voltage

For 99% charge transfer, we need: **t ≥ 4.6 × Ron × Cpixel**

**Typical values for a-Si TFT X-ray detector:**

| Parameter | Typical Value | Notes |
|---|---|---|
| TFT threshold voltage (Vth) | ~2 V | a-Si:H |
| Gate-ON voltage (VGG) | +20 to +30 V | |
| VGS − Vth (overdrive) | 18–28 V | |
| Field-effect mobility (μ) | 0.3–1.0 cm²/V·s | a-Si:H |
| TFT channel W/L | 20/5 to 40/5 µm | |
| Ron (estimated) | 50 kΩ – 2 MΩ | |
| Cpixel | 150–400 fF | Including photodiode |
| RC time constant (τ) | 10–100 µs | |
| Gate-ON time (99.9%) | 70 µs – 700 µs | 7× τ |

For a practical 2048-row panel at 30 fps (total frame ~33 ms), the line time budget is:
```
Line time = 33 ms / 2048 rows ≈ 16 µs/row
```
This is tight. At 15 fps: ~32 µs/row, which is borderline.

**Key insight:** Low-mobility a-Si TFTs require longer gate-ON time. IGZO TFTs (µ~10 cm²/V·s) allow ~10× shorter gate-ON time for the same channel geometry, enabling higher frame rates.

**Reference:** [Pixel Charging Efficiency PMC paper](https://pmc.ncbi.nlm.nih.gov/articles/PMC10891668/)  
**Reference:** [Flat Panel Detector operation (Pediatric Radiology PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC2663651/)

### 2.2 Gate Overlap and Cross-Talk

- **Gate overlap between adjacent rows (simultaneous activation)** must be avoided during normal readout to prevent cross-talk.
- The gate IC ensures mutual exclusion: only one row is HIGH at a time.
- **Transition time:** Gate output rise/fall is 400–1000 ns (for 200 pF load). For long gate lines (high capacitance), this can be several µs.
- **Timing margin requirement:** The gate pulse must remain HIGH until the charge amplifier has settled, then fall before the next row's STV propagation.

### 2.3 Sequential vs. Simultaneous Activation

| Mode | Description | Application |
|---|---|---|
| Sequential (normal) | One row active at a time | Standard readout |
| Simultaneous (panel reset) | All rows HIGH via XON | Panel reset / preconditioning |
| Grouped simultaneous | Multiple rows via multi-STV | Faster reset in groups |
| Skip (ROI) | Non-sequential, skip rows | High frame rate ROI |

---

## 3. Pre-charge and Reset Sequence

### 3.1 The Need for Reset

In X-ray FPD operation, before an image acquisition cycle, all pixel capacitors must be discharged (reset) to a known dark level. Without reset, residual charge from prior exposure or dark current buildup would superimpose on the new signal — causing fixed-pattern offset errors and lag artifacts.

The standard operating cycle is:
```
RESET → WAIT (X-ray exposure) → READOUT
```

For fluoroscopy (continuous), it becomes:
```
RESET → EXPOSE → READOUT → RESET → EXPOSE → READOUT → ...
```

### 3.2 Row-by-Row Reset

The simplest reset is simply running a normal forward scan without connecting data lines to image signals — instead, the data lines are held at a reference (bias) voltage. As each row activates:
1. TFT turns ON
2. Pixel capacitor discharges to the reference level on the data line
3. Gate turns OFF; pixel is now reset

This is inherently the same as a dummy readout at the dark reference level.

**Reset timing:** Same as normal readout, but data lines are clamped to Vreset (typically VCOM or bias voltage, not read by ADC).

### 3.3 Panel-Wide Reset Using XON

The Novatek gate ICs include a **XON (active-low)** pin:
- When XON → LOW: **All outputs simultaneously forced to VGG** (regardless of shift register state)
- All TFTs on the panel turn ON simultaneously
- All pixel capacitors discharge to whatever voltage is on the data lines simultaneously
- Much faster than row-by-row (single step vs. N steps for N rows)

**XON-based full-panel reset algorithm:**
```
1. Assert XON_LOW (all gates → VGG)
2. Hold data lines at Vreset voltage
3. Wait ≥ max(RC_pixel × 7) ≈ 200–500 µs (for >99.9% discharge)
4. Deassert XON (XON → HIGH)
5. All gates return to VEE (all TFTs off)
6. Panel is fully reset
```

**Critical constraint:** The XON delay from assertion to all-outputs-VGG is ≤10–20 µs (per NT52002 specs: Txon ≤ 20 µs for CL=200pF). Account for this in timing calculations.

### 3.4 Pre-charge Voltage

In some implementations, the data lines are driven to a **pre-charge voltage (Vpc)** before and during gate activation:
- Vpc is set slightly below the expected dark-frame signal level
- This reduces the charge swing required, decreasing settling time
- In X-ray detectors, Vpc = VCOM (common bias reference)

**Pre-charge timing pattern:**
```
1. Set data lines to Vpc (pre-charge bus drivers)
2. Assert gate line (TFT ON)
3. Hold gate active during charge settling
4. De-assert gate (TFT OFF)
5. Data lines now carry settled signal for ADC sampling
```

---

## 4. Bidirectional Scanning

### 4.1 Forward / Reverse Scan Direction Control

All Novatek gate driver ICs support bidirectional scan via the **U_D (Up/Down)** pin:

| U_D | Direction | Scan Order | STVD/STVU roles |
|---|---|---|---|
| H (1) | Up shift | G1 → G2 → ... → Gn | STVD = input, STVU = output |
| L (0) | Down shift | Gn → Gn-1 → ... → G1 | STVU = input, STVD = output |

**STV cascade:** In a down scan (U_D=L), STVU receives the start pulse and STVD emits the cascade pulse to the next IC in chain. In up scan (U_D=H), the opposite.

### 4.2 Benefits of Bidirectional Scanning for Lag Reduction

In X-ray FPDs, alternating scan direction between frames provides several benefits:

1. **Lag averaging:** Charge trapping in a-Si:H causes asymmetric lag (earlier rows exhibit more lag due to longer hold time before readout). Alternating scan direction averages this asymmetry across frames.

2. **TFT threshold voltage compensation:** Prolonged gate stress causes Vth shift. Alternating direction distributes the stress more evenly across all rows.

3. **Interlaced scanning:** Some implementations alternate even/odd rows (interlaced), which in combination with bidirectional scan can mimic the temporal averaging of interlaced CRT patterns, reducing perceived flicker artifacts at low frame rates.

**Bidirectional scan sequence for lag reduction:**
```
Frame N:   Forward scan  G1 → G2 → ... → G2048
Frame N+1: Reverse scan  G2048 → G2047 → ... → G1
Frame N+2: Forward scan  G1 → G2 → ... → G2048
...
```

**Reference:** [Nonlinear lag correction algorithm (PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/)  
**Reference:** [NT39207 datasheet - bidirectional shift](https://www.orientdisplay.com/wp-content/uploads/2022/08/NT39207_v0.11.pdf)

### 4.3 Implementation Notes

- U_D must be stable before the STV pulse is issued.
- Changing U_D mid-scan is not permitted (shifts register state).
- FPGA must track current scan direction and present correct STV to correct STVD/STVU input.
- When U_D changes between frames, a gap of ≥ 1 CLK cycle after de-asserting STV is recommended before toggling U_D.

---

## 5. Panel Reset Algorithm

### 5.1 Multiple Reset Cycles Before Readout

A single reset pass may be insufficient due to:
1. **Charge trapping in a-Si layer** — deep traps release charge slowly, not fully emptied in one reset cycle
2. **Residual lag** — prior X-ray exposure leaves charge in traps that leak back into pixel during exposure window
3. **Settling of dark offset** — after mode-switching (e.g., from AED mode to image mode)

**Recommended multi-cycle reset protocol:**
```
Repeat N times (N = 3–8 recommended):
  1. Run complete gate scan (all rows, line-by-line)
  2. Data lines held at Vreset
  3. No ADC sampling
End repeat
→ Panel is "preconditioned" for stable offset level
```

The Varian 4030CB implementation uses 3–5 reset cycles minimum before a clean acquisition frame. [Reference: Forward bias lag correction (PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/)

### 5.2 Reset with Dummy Readout

A more thorough approach runs the full reset+readout cycle but discards the data:
```
Repeat M times:
  1. Run complete gate scan (all rows)
  2. All data lines connected to charge amplifiers
  3. ADC samples all pixels
  4. Discard acquired frame (not sent to host)
  5. Subtract from running baseline
End repeat
→ Use final discarded frame as offset reference
```

**Benefits:**
- Charge amplifiers are exercised (baseline established)
- Kink transients in dark current are stabilized
- Offset drift is minimized for subsequent real exposure

**Warmup frame count:**
- At power-on: 30–100 dummy frames (~2–7 s at 15 fps)
- After standby: 5–20 dummy frames
- Between exposures: 2–5 dummy frames

### 5.3 Forward Bias Reset (a-Si Specific)

For a-Si:H panels, a specialized hardware reset exists:
1. After standard readout, **forward-bias the photodiodes** (apply positive voltage across photodiode, reverse of normal operation)
2. This drives a large current (~20 pC/diode) through the photodiode, uniformly filling ALL charge traps
3. Then reset to reverse bias before next exposure

This reduces first-frame lag from ~2–3% to <0.3%. Requires special hardware capability (forward-biasing the bias line), not possible with standard gate IC alone.

**Reference:** [Forward bias method (PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/)

### 5.4 Flush-N Method

Insert N empty (non-X-ray) frames between acquired frames:
```
EXPOSE → READOUT_1 → READOUT_2 (empty) → ... → READOUT_N (empty) → EXPOSE → ...
```
- Each empty readout flushes residual lag signal
- N=1 reduces lag ~50%; N=3 reduces ~80%
- Cost: reduces effective frame rate by factor (N+1)

---

## 6. Partial Area Readout (ROI Scanning)

### 6.1 ROI Scan Principle

Instead of scanning all N rows, ROI scanning activates only a subset of rows:
- Rows within the ROI: activated normally (STV clocked through)
- Rows outside ROI: **skipped** (gate IC clock advanced without STV, so no gate output)

Or equivalently, using OE pin:
- Rows outside ROI: OE asserted (outputs forced LOW/VEE) while shift register still advances

### 6.2 Skip-Row Scanning Methods

**Method 1: STV held, CLK advances**
```
For each row i in 1..N:
  if i in ROI:
    Assert STV_pulse at correct time (produces gate output)
  else:
    No STV_pulse (shift register stays at idle, gate stays VEE)
  Advance CLK
```
This method is problematic because if STV is never asserted, the shift register never propagates. A more practical approach is to send STV only at the start of each ROI block.

**Method 2: Continuous scan with OE masking**
```
Start normal scan (STV + CLK)
For each row i:
  if i not in ROI:
    Assert OE (all outputs forced to VEE)
  else:
    De-assert OE (normal gate output)
  Advance CLK
```
**Advantage:** Shift register stays synchronized; OE can be toggled in sync with CLK independently.
**Disadvantage:** Full scan time is still N × line_time; frame rate improvement comes only from skipping ADC readout, not from faster scanning.

**Method 3: Reduced row scan (true ROI)**
For a true frame rate improvement, only scan ROI rows:
```
Issue STV at ROI_start_row
Clock through ROI_rows only
Stop after ROI_end_row (let STVU propagate, don't issue new STV)
Total scan time = ROI_rows × line_time  <<  N × line_time
```
This requires the FPGA to count rows precisely and terminate the scan.

**Reference:** [Detection Technology X-Panel 1412i (ROI: 300 fps)](https://www.deetee.com/wp-content/uploads/Detection-Technology-X-Panel-1412i-CMOS-X-ray-flat-panel-detector.pdf)

### 6.3 ROI Timing Example

| Configuration | Rows | Line time | Frame time | Frame rate |
|---|---|---|---|---|
| Full 2048-row panel | 2048 | 16 µs | 32.8 ms | 30 fps |
| ROI 512 rows (center) | 512 | 16 µs | 8.2 ms | 120 fps |
| ROI 100 rows | 100 | 16 µs | 1.6 ms | 625 fps |

### 6.4 FPGA ROI Control Pattern

```systemverilog
// ROI scan control
parameter ROI_FIRST = 512;
parameter ROI_LAST  = 1024;

always_ff @(posedge clk) begin
  case (state)
    IDLE: begin
      row_counter <= 0;
      state <= WAIT_VSYNC;
    end
    WAIT_VSYNC: begin
      if (vsync_trigger) begin
        // Issue STV at ROI_FIRST
        row_counter <= ROI_FIRST;
        stv_pulse <= 1;
        state <= SCAN_ROI;
      end
    end
    SCAN_ROI: begin
      stv_pulse <= 0;
      cpv_en <= 1;        // Enable CPV to gate IC
      if (row_counter >= ROI_LAST) begin
        cpv_en <= 0;
        state <= WAIT_VSYNC;
      end else begin
        row_counter <= row_counter + 1;
      end
    end
  endcase
end
```

---

## 7. NT39565D / Novatek Family Specifics

### 7.1 NT39565D Context

The NT39565D is a Novatek gate driver IC for large TFT-LCD / X-ray panels. While a full public datasheet is not widely available (confidential OEM distribution), its architecture is consistent with the NT52002 / NT52001 / NT39207 family. Key features inferred from Novatek product history and adjacent datasheets:

**Likely specifications (extrapolated from NT52002 family):**

| Feature | NT39565D (inferred) | NT52002 (confirmed) |
|---|---|---|
| Output channels | 480–960 | 600 |
| VGG max | +35–42 V | +42 V |
| VEE min | −20 V | −20 V |
| Bidirectional scan | Yes (U_D pin) | Yes |
| Double gate (2G) mode | Yes | Yes |
| OE1/OE2 separation | Yes | Yes |
| Cascade | Yes (STVD/STVU) | Yes |
| XON (all-gate reset) | Yes | Yes |
| CPV clock | Yes | Yes (CLK) |

### 7.2 Dual STV (Start Vertical Pulse)

Some panel configurations use **two independent STV signals (STVD and STVU)** simultaneously during a special "all gates reset" phase:

- **STVD** starts a forward scan from G1
- **STVU** starts a reverse scan from Gn simultaneously

This creates a **bidirectional simultaneous activation wavefront** that meets in the middle of the panel, ensuring all rows are reset in N/2 CLK cycles instead of N. In X-ray detector applications, this is used for rapid full-panel reset.

In the NT52002 documentation, STVD and STVU are inherently bidirectional (each is input when that direction is selected, output when it's the cascade end). Using both as inputs simultaneously is not standard operation and requires careful FPGA driving to avoid contention.

### 7.3 CPV Clock Signal

In the NT39565D context (and LCD panels using CPV terminology):
- **CPV = Clock Pulse Vertical** = shift clock for gate IC (same as CLK in NT39207/NT52002 notation)
- Frequency: typically 2–100 kHz for X-ray; up to 200 kHz max for cascade
- Duty cycle: 50% (tH = tL ≥ 500 ns each)

### 7.4 OE1/OE2 Odd/Even Channel Separation

The NT52002 implements three OE signals:
- **OE1:** Disables (3n+1)th channels → odd channels group 1
- **OE2:** Disables (3n+2)th channels → even channels group 2
- **OE3:** Disables (3n+3)th channels → group 3

In a 2-channel (OE1/OE2) configuration:
- **OE1 only asserted:** Only even rows active
- **OE2 only asserted:** Only odd rows active
- **Neither OE asserted:** All rows normal

**X-ray application:** This enables **interlaced scanning** (odd rows frame 1, even rows frame 2) for temporal oversampling or for split-exposure modes (e.g., different exposures for odd/even rows in dual-energy imaging).

**Reference:** [NT52002 Datasheet (Phoenix Display)](https://www.phoenixdisplay.com/wp-content/uploads/2015/07/NT52002.pdf)

### 7.5 Double Gate (2G) Mode

NT52002 SEL pin configurations define output scan order:

| SEL0 | SEL1 | F_Ctrl | Scan Mode | Output Order (U_D=H) |
|---|---|---|---|---|
| 0 | X | 0 | Z (standard) | G1→G2→G3→G4→G5... |
| 0 | X | 1 | Inv-Z | G2→G1→G4→G3→G6... |
| 1 | 0 | 0 | 2 (2G standard) | G1→G2→G4→G3→G5→G6→G8→G7... |
| 1 | 0 | 1 | Inv-2 | G2→G1→G3→G4→G6→G5→G7→G8... |
| 1 | 1 | 0 | Z+2 | G1→G2→G3→G4→G6→G5→G7→G8... |
| 1 | 1 | 1 | Inv(Z+2) | G2→G1→G4→G3→G5→G6→G8→G7... |

**2G mode explanation:**
- Two adjacent gate lines share one display row (dual-gate pixel structure)
- The "2" pattern activates pairs in an alternating order to balance pixel charging
- In X-ray detectors, 2G mode can be used to ensure complete charge transfer by providing overlapping gate activation of two rows simultaneously (effective doubling of charge transfer conductance)

**Reference:** [Dual-gate display patent CN102737591A (Google Patents)](https://patents.google.com/patent/CN102737591A/en)

---

## 8. Gate Drive Voltage Optimization (VGG / VEE)

### 8.1 VGG Selection for TFT On-Resistance

The TFT on-state channel resistance directly determines charge transfer speed:

```
Ron ≈ L / (W × µ × Cox × (VGS − Vth))
    = L / (W × µ × Cox × (VGG − VDATA − Vth))
```

Where:
- **VGG** = gate-on voltage applied to pixel TFT gate
- **VDATA** = voltage on source (data line, changes during transfer)
- **Vth** ≈ +2 V for a-Si:H TFTs

**VGG trade-offs:**

| VGG | Ron | Charge speed | TFT stress | Power |
|---|---|---|---|---|
| +15 V | High | Slow | Low | Low |
| +20 V | Medium | Good | Moderate | Moderate |
| +25 V | Lower | Fast | Higher | Higher |
| +30 V | Lowest | Very fast | High (Vth shift risk) | High |

**Typical recommended VGG for X-ray FPD:**
- **VGG = +20 to +25 V** — balances speed, on-resistance, and long-term Vth stability
- Higher VGG accelerates Vth shift (positive gate stress) — limits panel lifetime
- For a-Si:H: avoid prolonged gate-ON at VGG > 30 V

**Reference:** [a-Si TFT review (unisa.it)](https://www.fisica.unisa.it/antonio.dibartolomeo/MaterialeDidatticoFisicaSemiconduttori/Articoli/A-Si_andTFTtechnology%20for%20Xray%20detection.pdf)

### 8.2 VEE Selection for Off-Leakage Minimization

The TFT off-state must maintain pixel charge during the integration period (exposure time). Off-leakage current causes the pixel to lose charge, reducing sensitivity and SNR.

**VEE constraints:**

- More negative VEE → lower leakage (better isolation), but:
  - Risk of parasitic back-channel conduction in overlapped-pixel TFTs at VGS < −5 V
  - Increases power supply requirements
  - Potential dielectric breakdown if |VEE − VGG| > 40 V max
  - Bipolar stress on a-Si channel can reduce Vth more quickly

**Recommended VEE for a-Si:H X-ray FPD:**
- **VEE = −10 to −15 V** — typical optimum
  - At VGS = VEE − VDATA ≈ −10 to −17 V: leakage < 0.1 pA per pixel
  - At VGS < −15 V: risk of parasitic back-channel in overlapped TFT geometries increases

**Operating constraint:** VGG − VEE ≤ 40 V absolute maximum (gate driver IC limit)  
**Example:** VGG = +25 V, VEE = −15 V → VGG−VEE = 40 V (at limit, use +22V/−15V for margin)

**Bipolar bias benefit:** Alternating sign of gate voltage (positive during ON, negative during OFF) helps symmetrize trap occupation in a-Si:H, slowing long-term Vth drift. Some advanced panel designs use **AC gate bias** (slightly positive off-state voltage) for better Vth stability.

**Reference:** [a-Si:H TFT (collectionscanada.gc.ca)](https://www.collectionscanada.gc.ca/obj/s4/f2/dsk4/etd/NQ83000.PDF)

### 8.3 Voltage Summary Table

| Parameter | Min | Typical | Max | Notes |
|---|---|---|---|---|
| VCC (logic) | 2.3 V | 3.3 V | 3.6 V | Gate IC logic supply |
| VGG (gate ON) | +7 V | +20 to +25 V | VEE+40 V | TFT gate high |
| VEE (gate OFF) | −20 V | −10 to −15 V | −5 V | TFT gate low |
| VGG − VEE | 12 V | 35–40 V | 40 V | Output amplitude |
| TPOR | — | — | 20 ms | Power-on reset time |
| Power-on seq | VCC → VEE → VGG | | | |
| Power-off seq | VGG → VEE → VCC | | | |

---

## 9. Multi-chip Cascade Synchronization

### 9.1 Why Multi-Chip Cascading

A single Novatek gate driver IC handles 320–960 output channels. For large X-ray panels (e.g., 43×43 cm, 2560 rows), multiple ICs must be cascaded.

**Example: 2560-row panel with NT52002 (600 ch/IC):**
- IC1: G1–G600 (STVD input from FPGA, STVU output to IC2)
- IC2: G601–G1200 (STVD input = STVU of IC1, STVU output to IC3)
- IC3: G1201–G1800
- IC4: G1801–G2400
- IC5 (partial): G2401–G2560

### 9.2 Cascade Wiring

**Down scan (U_D=L, default):**
```
FPGA → STVU[IC1] → G600→G1 (IC1) → STVD[IC1] = STVU[IC2] → G600→G1 (IC2) → ...
```

**Up scan (U_D=H):**
```
FPGA → STVD[IC1] → G1→G600 (IC1) → STVU[IC1] = STVD[IC2] → G1→G600 (IC2) → ...
```

**All ICs share:**
- CLK (CPV) — same clock signal fanned out to all ICs
- VGG / VEE / VCC / GND — shared power rails
- U_D — same direction control
- OE1/OE2/OE3 — same enable signals (if panel-wide control)

### 9.3 Synchronization Signal Chain

The STV pulse propagates through the cascade chain like a token:
```
T=0:    FPGA asserts STV to IC1_STVD
T=600τ: IC1 finishes, emits STVD pulse → IC2 input
T=1200τ: IC2 finishes, emits STVD pulse → IC3 input
...
T=Nτ:   Last IC finishes, emits STVD to FPGA (optional: frame-end detection)
```

Where τ = one CLK period (line time).

**FPGA must:**
1. Issue only one STV at the beginning of each frame
2. Count the expected number of CLK periods until STVD returns from the last IC
3. Use the returned STVD as a "frame complete" signal (or alternatively, count rows and suppress)

### 9.4 Timing Constraints for Cascade

| Constraint | Value | Source |
|---|---|---|
| Max CLK frequency (cascade) | 200 kHz | NT52002 / NT39207 spec |
| STVD/STVU delay (IC propagation) | ≤ 500 ns | NT52002 |
| STVD setup time before CLK | ≥ 200 ns | NT52002 |
| STVD hold time after CLK | ≥ 300 ns | NT52002 |
| CLK pulse width (min) | 500 ns each | NT39207 |

**Inter-IC propagation:** STVD of IC(n) → STVD of IC(n+1) has one CLK period delay plus ≤500 ns IC-internal delay. Use slow CLK (5–50 kHz) for X-ray applications.

### 9.5 Bidirectional Cascade Wiring

For bidirectional scan, the cascade connections must be **commutable**:
- STVD pins of all ICs are both input (U_D=H) and output (U_D=L)
- STVU pins of all ICs are both input (U_D=L) and output (U_D=H)
- FPGA must apply STV to the correct end based on U_D

**Recommended FPGA-side logic:**
```systemverilog
assign ic1_stvd_in = (scan_direction == UP) ? fpga_stv : ic2_stvd_in;
assign ic1_stvu_out = ic1_stvo_pad;  // always output when U_D=H
// etc. for each IC
```

Or implement with tristated buffers on the STV lines.

---

## 10. FPGA Control FSM Design Patterns

### 10.1 Overall FSM Architecture

The gate IC controller FSM should be structured with a top-level frame sequencer and a per-row timing module:

```
┌─────────────────────────────────────────────────┐
│          FRAME SEQUENCER FSM                    │
│                                                 │
│  IDLE → POWER_ON_WAIT → RESET_PHASE →          │
│         EXPOSE_WAIT → READOUT_PHASE → IDLE      │
└──────────────┬──────────────────────────────────┘
               │ controls
               ▼
┌─────────────────────────────────────────────────┐
│         ROW TIMING MODULE                       │
│  INTER_ROW → GATE_HIGH → GATE_LOW →             │
│  (repeats N times)                              │
└──────────────┬──────────────────────────────────┘
               │ drives
               ▼
         CPV, STV, U_D, OE, XON signals
```

### 10.2 Top-Level Frame Sequencer FSM

```systemverilog
typedef enum logic [3:0] {
  S_IDLE          = 4'h0,
  S_POWER_ON      = 4'h1,
  S_RESET_MULTI   = 4'h2,  // Multiple reset cycles
  S_DUMMY_FRAMES  = 4'h3,  // Warmup dummy readouts
  S_WAIT_TRIGGER  = 4'h4,  // Wait for X-ray trigger
  S_EXPOSE        = 4'h5,  // X-ray integration window
  S_READOUT       = 4'h6,  // Active readout scan
  S_POST_RESET    = 4'h7,  // Post-readout reset
  S_FLUSH         = 4'h8   // Flush-N frames for lag
} frame_state_t;

module frame_sequencer (
  input  logic       clk, rst_n,
  input  logic       xray_trigger,
  input  logic       row_scan_done,    // from row timer
  output logic       scan_enable,      // enable row timer
  output logic       scan_is_reset,    // data lines = Vreset
  output logic       adc_capture_en,   // enable ADC
  output logic       xon_n,            // XON to gate IC
  output logic       ud,               // U_D direction
  output logic [2:0] oe_mask,          // OE1/OE2/OE3
  output logic       frame_done
);

  frame_state_t state, next_state;
  logic [5:0] reset_count;    // count reset cycles
  logic [5:0] dummy_count;    // count dummy frames
  logic [1:0] flush_count;    // count flush frames

  // Sequential state register
  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) state <= S_IDLE;
    else        state <= next_state;
  end

  // Counter management
  always_ff @(posedge clk) begin
    case (state)
      S_RESET_MULTI: if (row_scan_done) reset_count <= reset_count + 1;
                     else if (next_state == S_RESET_MULTI) reset_count <= 0;
      S_DUMMY_FRAMES: if (row_scan_done) dummy_count <= dummy_count + 1;
                      else if (next_state == S_DUMMY_FRAMES) dummy_count <= 0;
      S_FLUSH:       if (row_scan_done) flush_count <= flush_count + 1;
      default: begin reset_count <= 0; dummy_count <= 0; flush_count <= 0; end
    endcase
  end

  // Next-state logic
  always_comb begin
    next_state = state;
    case (state)
      S_IDLE:        next_state = S_POWER_ON;
      S_POWER_ON:    next_state = S_RESET_MULTI;  // after power-on delay
      S_RESET_MULTI: if (row_scan_done && reset_count >= 5) 
                       next_state = S_DUMMY_FRAMES;
      S_DUMMY_FRAMES: if (row_scan_done && dummy_count >= 30)
                       next_state = S_WAIT_TRIGGER;
      S_WAIT_TRIGGER: if (xray_trigger) next_state = S_EXPOSE;
      S_EXPOSE:      next_state = S_READOUT;  // trigger-controlled
      S_READOUT:     if (row_scan_done) next_state = S_FLUSH;
      S_FLUSH:       if (row_scan_done && flush_count >= 1)
                       next_state = S_WAIT_TRIGGER;
      default:       next_state = S_IDLE;
    endcase
  end

  // Output logic
  always_comb begin
    scan_enable    = (state inside {S_RESET_MULTI, S_DUMMY_FRAMES, S_READOUT, S_FLUSH});
    scan_is_reset  = (state inside {S_RESET_MULTI});
    adc_capture_en = (state == S_READOUT);
    xon_n          = 1'b1;   // XON only used for fast panel reset
    ud             = 1'b0;   // default down scan; toggle per frame for bidirectional
    oe_mask        = 3'b000; // all enabled by default
    frame_done     = (state == S_FLUSH && row_scan_done && flush_count >= 1);
  end

endmodule
```

### 10.3 Row Timing Module FSM

```systemverilog
typedef enum logic [2:0] {
  R_IDLE       = 3'h0,
  R_STV_PULSE  = 3'h1,  // Assert STV for 1 CLK
  R_GATE_HIGH  = 3'h2,  // Gate line active (TFT ON)
  R_GATE_LOW   = 3'h3,  // Gate line deactive (wait for ADC)
  R_ROW_DONE   = 3'h4   // End of this row, advance counter
} row_state_t;

// Timing parameters (in clock cycles at system_clk)
// Assume system_clk = 100 MHz, CPV = 50 kHz (20 µs period)
parameter CLK_PER_CPV   = 2000;    // 20 µs at 100 MHz
parameter STV_WIDTH     = 200;     // 2 µs STV pulse width (200 cycles)
parameter GATE_ON_CYCLES = 900;    // 9 µs gate-on time (900 cycles) 
parameter ADC_SETUP      = 1000;   // 10 µs ADC settle + sample
// Total line time = STV_WIDTH + GATE_ON_CYCLES + ADC_SETUP ≈ 21 µs
```

### 10.4 STV / CPV Signal Generation

```systemverilog
module gate_ic_driver (
  input  logic        clk,          // System clock (e.g., 100 MHz)
  input  logic        rst_n,
  input  logic        scan_start,   // Trigger new frame scan
  input  logic [10:0] n_rows,       // Total rows to scan
  input  logic [10:0] roi_first,    // ROI start row (0 for full frame)
  input  logic [10:0] roi_last,     // ROI end row (n_rows-1 for full)
  input  logic        ud_ctrl,      // U_D direction
  output logic        cpv,          // CPV to gate IC
  output logic        stv,          // STV to gate IC
  output logic        ud,           // U_D to gate IC
  output logic        xon_n,        // XON (active low)
  output logic        oe_n,         // OE (active low to enable)
  output logic        scan_done,    // Asserted when all rows scanned
  output logic [10:0] current_row   // Current row index
);

  // CPV generation: 50% duty cycle at target frequency
  logic [11:0] cpv_cnt;
  parameter CPV_HALF = 1000;  // 100 MHz / 2 / 50 kHz
  
  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      cpv <= 0; cpv_cnt <= 0;
    end else if (cpv_cnt >= CPV_HALF - 1) begin
      cpv <= ~cpv;
      cpv_cnt <= 0;
    end else begin
      cpv_cnt <= cpv_cnt + 1;
    end
  end

  // STV generation: One clock-wide pulse at row transition
  logic cpv_prev;
  logic cpv_posedge;
  always_ff @(posedge clk) cpv_prev <= cpv;
  assign cpv_posedge = cpv & ~cpv_prev;

  logic [10:0] row_cnt;
  logic scan_active;
  
  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      row_cnt    <= 0;
      stv        <= 0;
      scan_done  <= 0;
      scan_active <= 0;
      current_row <= 0;
    end else begin
      stv <= 0;  // Default de-assert
      scan_done <= 0;
      
      if (scan_start && !scan_active) begin
        scan_active <= 1;
        row_cnt <= roi_first;
        // Issue STV on next CPV rising edge
      end
      
      if (scan_active && cpv_posedge) begin
        current_row <= row_cnt;
        
        if (row_cnt == roi_first) begin
          stv <= 1;  // First row: pulse STV
        end
        
        if (row_cnt >= roi_last) begin
          scan_active <= 0;
          scan_done   <= 1;
          row_cnt     <= 0;
        end else begin
          row_cnt <= row_cnt + 1;
        end
      end
    end
  end

  assign ud    = ud_ctrl;
  assign xon_n = 1'b1;  // Normal operation; assert low for fast reset
  assign oe_n  = 1'b1;  // Outputs enabled (oe active high = disable)

endmodule
```

### 10.5 XON Fast-Reset Implementation

```systemverilog
// Fast panel reset using XON
module fast_reset_ctrl (
  input  logic clk,
  input  logic rst_n,
  input  logic reset_trigger,
  input  logic [15:0] hold_cycles,   // XON assert duration
  output logic xon_n,                // To gate IC XON pin
  output logic reset_done
);

  typedef enum logic [1:0] {
    XR_IDLE  = 2'b00,
    XR_ASSERT = 2'b01,
    XR_WAIT  = 2'b10,
    XR_DONE  = 2'b11
  } xr_state_t;
  
  xr_state_t state;
  logic [15:0] cnt;
  
  // Additional delay for XON → output delay (≤20 µs → 2000 cycles at 100 MHz)
  parameter XON_PROP_DELAY = 2000;

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      state <= XR_IDLE; xon_n <= 1; reset_done <= 0; cnt <= 0;
    end else begin
      reset_done <= 0;
      case (state)
        XR_IDLE: begin
          if (reset_trigger) begin
            xon_n <= 0;  // Assert XON (all gates → VGG)
            cnt <= 0;
            state <= XR_ASSERT;
          end
        end
        XR_ASSERT: begin
          // Wait for XON propagation + pixel reset settling
          if (cnt >= hold_cycles + XON_PROP_DELAY) begin
            xon_n <= 1;  // De-assert XON
            cnt <= 0;
            state <= XR_DONE;
          end else cnt <= cnt + 1;
        end
        XR_DONE: begin
          reset_done <= 1;
          state <= XR_IDLE;
        end
      endcase
    end
  end
endmodule
```

### 10.6 Bidirectional Scan Direction Toggle

```systemverilog
// Alternate scan direction each frame for lag reduction
logic ud_direction;  // 0 = down scan, 1 = up scan

always_ff @(posedge clk) begin
  if (frame_done) begin
    ud_direction <= ~ud_direction;  // Toggle each frame
  end
end

// For down scan (U_D=0): FPGA drives STV → STVU pin of IC1
// For up scan  (U_D=1): FPGA drives STV → STVD pin of IC1
assign ic_stvd_in = (ud_direction == 1'b1) ? stv : 1'b0;
assign ic_stvu_in = (ud_direction == 1'b0) ? stv : 1'b0;
```

### 10.7 FSM Best Practices for Gate IC Control

1. **Synchronize all outputs to posedge of system clock** — never drive CPV/STV combinatorially to avoid glitches.

2. **Use clock enable (CE) instead of gated clocks** for CPV generation — avoids clock skew issues on FPGA.

3. **One-hot encoding** for states with many parallel outputs — reduces combinatorial fan-out delay.

4. **Pipeline STV assertion** — assert STV one system clock before the target CPV rising edge (allow setup time ≥ 200 ns).

5. **Separate data path from control path** — the row counter and ADC trigger run independently from the STV/CPV state machine.

6. **Include timeout watchdog** — if row_scan_done never arrives (e.g., STVD return from last IC never comes), reset the FSM to avoid deadlock.

7. **Register all IC interface outputs** — minimize routing delay; CPV and STV are timing-critical signals.

8. **Constrain timing in XDC/SDC** — set max input delay on STVD_return (≤ one line time); set max output delay on CPV/STV to 10 ns.

---

## 11. Complete Timing Reference Tables

### 11.1 NT39207 / NT52002 AC Timing (VGG=25V, VEE=−15V, VCC=3.3V)

| Symbol | Parameter | Min | Typ | Max | Unit | Condition |
|---|---|---|---|---|---|---|
| Fclk | Clock frequency | — | — | 200 | kHz | Cascade |
| PWCLK | CLK pulse width (H or L) | 500 | — | — | ns | |
| Trck | CLK rise time | — | — | 100 | ns | CL=20pF |
| Tfck | CLK fall time | — | — | 100 | ns | CL=20pF |
| Tsu | STV setup time before CLK | 200 | — | — | ns | |
| Thd | STV hold time after CLK | 300 | — | — | ns | |
| Tdt | STVD/STVU output delay | — | — | 500 | ns | CL=20pF |
| Tdo | Gate output delay after CLK | — | — | 900 | ns | CL=200pF |
| Ttlh | Gate output rise time (10–90%) | — | 500 | 1000 | ns | CL=200pF |
| Tthl | Gate output fall time (90–10%) | — | 400 | 800 | ns | CL=200pF |
| Twcl | OE pulse width | 1 | — | — | µs | |
| Txon | XON to output delay | — | — | 20 | µs | CL=200pF |
| Toe | OE to output delay | — | — | 900 | ns | CL=200pF |
| TPOR | Power-on reset slew (0→90% VCC) | — | — | 20 | ms | |
| TCTE | VCC 90% → VEE 10% delay | 0 | — | — | ms | |
| TETG | VEE 10% → VGG 10% delay | 1 | — | — | ms | |

### 11.2 Line Time Budget (30 fps, 2048 rows)

| Phase | Duration | Notes |
|---|---|---|
| STV setup + CLK edge | 1–2 µs | STV pulse width |
| Gate ON (charge transfer) | 8–14 µs | ≥ 5τ for >99% transfer |
| Gate fall transition | 0.5–1 µs | Tthl |
| ADC settle + sample | 2–5 µs | Depends on CSA bandwidth |
| Precharge data lines | 1–2 µs | Optional |
| **Total per row** | **12–24 µs** | **Target: ≤16 µs at 30fps** |

### 11.3 Detector Operating Modes Summary

| Mode | Gate Scan | Data Lines | ADC | Notes |
|---|---|---|---|---|
| Power-on reset | XON LOW (all gates ON) | Vreset | Off | 200–500 µs hold |
| Multi-cycle reset | Full scan × N | Vreset | Off | N=3–8 cycles |
| Dummy readout | Full scan | Vreset → ADC | On (discard) | Warmup/offset |
| X-ray integration | No scan (gates OFF) | — | — | Charge accumulates |
| Full readout | Full scan | Vreset → ADC | On (save) | Real image |
| ROI readout | Partial scan | Vreset → ADC | On (ROI only) | High frame rate |
| Flush-N | N × full scan | Vreset → ADC | On (discard) | Lag reduction |
| Bidirectional | Alternating direction | Normal | Normal | Lag averaging |

---

## 12. Algorithm Pseudocode Library

### 12.1 Standard FPD Acquisition Sequence

```
procedure FPD_ACQUIRE_FRAME():
  // Phase 1: Pre-exposure reset
  for i in 1..N_RESET_CYCLES:   // N_RESET = 3-8
    GATE_SCAN_RESET()            // Run full scan with Vreset on data lines
  
  // Phase 2: Wait for X-ray trigger
  SET_GATE_ALL_OFF()             // All gates to VEE (integration mode)
  WAIT_FOR_XRAY_TRIGGER()
  
  // Phase 3: X-ray integration
  XRAY_EXPOSURE()                // Gates remain OFF during exposure
  WAIT_FOR_XRAY_END()
  
  // Phase 4: Image readout
  GATE_SCAN_READOUT()            // Full scan, ADC samples each row
  
  // Phase 5: Post-readout flush
  for i in 1..N_FLUSH:           // N_FLUSH = 1-3
    GATE_SCAN_RESET()            // Clear residual lag
  
  return FRAME_DATA

procedure GATE_SCAN_RESET():
  SET_DATA_LINES(Vreset)
  ISSUE_STV_PULSE()
  for row in 1..N_ROWS:
    WAIT_ONE_LINE_TIME()         // Gate IC shifts automatically
  WAIT_STV_RETURN()              // Wait for STVD from last IC

procedure GATE_SCAN_READOUT():
  SET_DATA_LINES(Vreset)         // Pre-charge
  ISSUE_STV_PULSE()
  for row in 1..N_ROWS:
    WAIT_GATE_ON_TIME()          // Tgate_on ≥ 5τ
    ADC_SAMPLE(row)              // Sample all columns
  WAIT_STV_RETURN()
```

### 12.2 Bidirectional Scan with Lag Reduction

```
procedure FPD_CONTINUOUS_FLUORO():
  scan_dir = FORWARD
  
  while RUNNING:
    // Reset with current direction
    GATE_SCAN_RESET(direction=scan_dir)
    
    // Expose + readout
    WAIT_XRAY()
    GATE_SCAN_READOUT(direction=scan_dir)
    
    // Toggle direction for next frame
    scan_dir = REVERSE if scan_dir == FORWARD else FORWARD
    
    OUTPUT_FRAME()
```

### 12.3 ROI High-Speed Acquisition

```
procedure FPD_ROI_ACQUIRE(roi_y1, roi_y2):
  // Skip rows before ROI (advance CLK without STV)
  ADVANCE_CLK_N_ROWS(roi_y1 - 1)    // No STV → gates stay at VEE
  
  // Scan ROI
  ISSUE_STV_PULSE()
  for row in roi_y1..roi_y2:
    WAIT_GATE_ON_TIME()
    ADC_SAMPLE(row)
  
  // Skip rows after ROI (optional - or just wait for next frame)
  ADVANCE_CLK_N_ROWS(N_ROWS - roi_y2)

  // Frame rate improvement = N_ROWS / (roi_y2 - roi_y1 + 1)
```

### 12.4 Multi-chip Panel Scan with Cascade

```
// Hardware setup:
// IC1: rows 1-600   (STVD_in=FPGA_STV, STVD_out→IC2_STVD_in)
// IC2: rows 601-1200 (STVD_in=IC1_STVD_out, ...)
// IC3: rows 1201-1800
// IC4: rows 1801-2048 (partial: 248 rows active)

procedure CASCADE_SCAN():
  SET_U_D(DOWN)               // All ICs same direction
  ISSUE_STV_TO_IC1()         // Single start pulse
  CLOCK_CPV(N_ROWS_TOTAL)    // Clock all ICs with shared CPV
  // STV propagates: IC1→IC2→IC3→IC4 automatically
  
  // Detect end-of-frame
  WAIT_FOR_STVD_FROM_IC4()   // Optional last-IC cascade output
  // Or: count CPV pulses = N_ROWS_TOTAL
```

### 12.5 XON Fast Panel Reset

```
procedure FAST_PANEL_RESET(hold_time_us):
  ASSERT_XON()                // All gates → VGG simultaneously
  WAIT(20e-6)                 // XON propagation delay (≤20 µs)
  // All pixel TFTs now ON, charge discharging to Vreset
  WAIT(hold_time_us - 20e-6)  // Hold: ≥ 5τ_pixel ≈ 100–500 µs
  DEASSERT_XON()              // All gates → VEE
  // Panel fully reset
```

---

## 13. Design Implications for FPGA SystemVerilog Implementation

### 13.1 Clock Domain Strategy

The FPGA system typically operates at a high system clock (50–200 MHz) while the gate IC signals operate at much lower frequencies (CPV: 5–50 kHz, line time: 20–200 µs). Strategy:

1. **Single clock domain preferred:** Generate CPV as a divided-down version of the system clock using a counter. Do NOT use MMCM/PLL-generated gated clocks for CPV — use clock enable approach instead.

2. **Timing diagram (100 MHz system_clk, CPV = 50 kHz):**
```
system_clk: __ | __ | __ | ... (10 ns period, 100 MHz)
CPV:        _____________________|‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾‾|____ (20 µs period, 50 kHz)
STV:        _____________________|‾‾‾|_____________________ (200 ns, setup before CPV fall)
G1 output:  _________________________|‾‾‾‾‾‾‾‾‾‾‾‾‾‾|____  (delayed by Tdo, lasts 1 CPV period)
ADC_trigger:                                   |‾‾|         (at 60% of gate-on time)
```

### 13.2 Critical Timing Closure Requirements

| Signal | Setup to CPV rising edge | Hold after CPV rising edge |
|---|---|---|
| STV input | ≥ 200 ns | ≥ 300 ns |
| U_D | Stable during scan | — |
| OE (gate IC input) | ≥ 100 ns (datasheet) | ≥ 100 ns |

At 100 MHz system clock:
- 200 ns = 20 clock cycles → STV must be asserted 20 cycles before CPV rising edge
- Register outputs with flip-flops close to FPGA I/O pads
- Add IOB (I/O Block) register constraints in XDC: `set_property IOB TRUE [get_ports cpv_o]`

### 13.3 Signal Quality and Electrical Considerations

1. **CPV drive strength:** CPV line has significant capacitance (sum of all IC inputs + PCB trace). Use FPGA I/O with 24 mA drive strength or add an external buffer.

2. **VCC level translation:** Gate IC VCC = 3.3 V but FPGA I/O = 1.8 V (common in modern FPGAs). Use level translators (e.g., TXS0108E) on CPV, STV, U_D, OE, XON lines.

3. **VGG/VEE decoupling:** Each gate IC switches significant current (100 µA DC + switching transients). Place 100 nF + 10 µF decoupling on each VGG and VEE pin close to the IC.

4. **Timing variation due to temperature:** Gate line RC increases at low temperature (higher a-Si resistance). Increase gate-ON time by 20–50% at temperatures below 0°C.

### 13.4 Register Map for FPGA Gate IC Controller

```systemverilog
// Proposed AXI-Lite register map for configurable gate IC control
// Base address: 0x4000_0000

localparam REG_CTRL        = 8'h00;  // [0]=scan_enable [1]=reset_mode [2]=bidirectional [7]=xon
localparam REG_N_ROWS      = 8'h04;  // [10:0] total rows to scan
localparam REG_ROI_FIRST   = 8'h08;  // [10:0] ROI start row
localparam REG_ROI_LAST    = 8'h0C;  // [10:0] ROI end row
localparam REG_GATE_ON_CYC = 8'h10;  // [15:0] gate-on cycles (in system clk)
localparam REG_CPV_HALF    = 8'h14;  // [15:0] CPV half-period (in system clk)
localparam REG_RESET_CNT   = 8'h18;  // [7:0]  number of reset cycles
localparam REG_DUMMY_CNT   = 8'h1C;  // [7:0]  number of dummy frames
localparam REG_STATUS      = 8'h20;  // [0]=scan_done [1]=frame_done [2]=xon_active
localparam REG_SCAN_DIR    = 8'h24;  // [0]=0:down/1:up [1]=auto_toggle
localparam REG_OE_CTRL     = 8'h28;  // [2:0]=OE1/OE2/OE3 mask
localparam REG_XON_HOLD    = 8'h2C;  // [15:0] XON assert duration (cycles)
```

### 13.5 Recommended SystemVerilog Module Hierarchy

```
gate_ic_top.sv
├── axi_lite_regfile.sv          // AXI-Lite config registers
├── frame_sequencer.sv           // Top-level frame state machine
│   ├── xon_reset_ctrl.sv        // Fast XON panel reset
│   └── row_scan_engine.sv       // Row-by-row scan engine
│       ├── cpv_gen.sv           // CPV clock generation
│       ├── stv_gen.sv           // STV pulse generation
│       └── row_counter.sv       // Row tracking + ROI control
├── output_stage.sv              // Registered outputs to gate IC
│   └── level_shift_if.sv        // If level shifters needed
└── cascade_monitor.sv           // STVD return monitoring + timeout
```

### 13.6 Timing Diagram for Complete One-Row Cycle

```
System CLK: ─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬ (100 MHz)
             │ │ │ │ │ │ │ │ │ │ │ │ │ │ │ │ │ │ │ │

CPV_O:       ──────────────────────────┐          ┌────── (LOW)
(to gate IC)                           └──────────┘
                                       ↑          ↑
                               CPV_rise (T=0)   CPV_rise (T=20µs)
                           
STV_O:       ──────────────────┐                           (stays LOW after row 1)
(to gate IC)                   └──────┘
                           STV_ON (200ns before CPV_rise)

Gate output: ──────────────────────────────┐               (gate IC output, delayed)
(measured at TFT gate line)                └─────────── 
                                ←  Tdo ≤900ns  →
                                ← gate-on time (9-14µs) →

ADC_trigger: ─────────────────────────────────────┐─┘     (at 80% of gate-on period)
(to ADC CNVST)

Data_valid:  ──────────────────────────────────────────┐─  (after ADC conversion time)
```

---

## 14. References and Sources

### Datasheets and Application Notes

1. **NT39207 Gate Driver Datasheet** (Novatek / Orient Display)  
   [https://www.orientdisplay.com/wp-content/uploads/2022/08/NT39207_v0.11.pdf](https://www.orientdisplay.com/wp-content/uploads/2022/08/NT39207_v0.11.pdf)

2. **NT52002 Gate Driver Datasheet** (Novatek / Phoenix Display)  
   [https://www.phoenixdisplay.com/wp-content/uploads/2015/07/NT52002.pdf](https://www.phoenixdisplay.com/wp-content/uploads/2015/07/NT52002.pdf)

3. **NT52001 Gate Driver Datasheet** (Novatek)  
   [https://www.scribd.com/document/555334122/NT52001-Novatek](https://www.scribd.com/document/555334122/NT52001-Novatek)

4. **Novatek Product Portfolio**  
   [https://www.novatek.com.tw/en-global/Product/product/Index/product2](https://www.novatek.com.tw/en-global/Product/product/Index/product2)

### Academic Papers

5. **Starman et al. (2012)** — "A nonlinear lag correction algorithm for a-Si flat-panel x-ray detectors"  
   [https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/](https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/)

6. **Starman et al. (2011)** — "A forward bias method for lag correction of an a-Si flat panel detector"  
   [https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/)

7. **Ma et al. (2024)** — "Enhancing Pixel Charging Efficiency by Optimizing TFT Dimensions in Gate Driver Circuits for AMLCDs"  
   [https://pmc.ncbi.nlm.nih.gov/articles/PMC10891668/](https://pmc.ncbi.nlm.nih.gov/articles/PMC10891668/)

8. **Dual-bootstrapping gate driver using IGZO TFTs** (ScienceDirect, 2024)  
   [https://www.sciencedirect.com/science/article/abs/pii/S0141938224001367](https://www.sciencedirect.com/science/article/abs/pii/S0141938224001367)

9. **High-Speed Shift Register with Dual-Gated TFTs** (PMC, 2022)  
   [https://pmc.ncbi.nlm.nih.gov/articles/PMC9610482/](https://pmc.ncbi.nlm.nih.gov/articles/PMC9610482/)

10. **a-Si detector and TFT technology for X-ray detection** (University of Salerno)  
    [https://www.fisica.unisa.it/antonio.dibartolomeo/MaterialeDidatticoFisicaSemiconduttori/Articoli/A-Si_andTFTtechnology%20for%20Xray%20detection.pdf](https://www.fisica.unisa.it/antonio.dibartolomeo/MaterialeDidatticoFisicaSemiconduttori/Articoli/A-Si_andTFTtechnology%20for%20Xray%20detection.pdf)

11. **Flat-panel detectors: how much better are they?** (Pediatric Radiology, PMC)  
    [https://pmc.ncbi.nlm.nih.gov/articles/PMC2663651/](https://pmc.ncbi.nlm.nih.gov/articles/PMC2663651/)

12. **Uneri et al. (2022)** — "Technical assessment of IGZO-based flat-panel X-ray detector"  
    [https://pmc.ncbi.nlm.nih.gov/articles/PMC10153656/](https://pmc.ncbi.nlm.nih.gov/articles/PMC10153656/)

### Patents

13. **CN102737591A** — "Gate driver of dual-gate display and frame control method thereof"  
    [https://patents.google.com/patent/CN102737591A/en](https://patents.google.com/patent/CN102737591A/en)

14. **US20250069539A1** — "Power optimized multi-regional update display"  
    [https://patents.google.com/patent/US20250069539A1/en](https://patents.google.com/patent/US20250069539A1/en)

### Application Documents

15. **Fujifilm FPGA-based FPD control** — "Technology for Improving Sensitivity of X-ray Automatic Detection"  
    [https://asset.fujifilm.com/www/jp/files/2019-12/5334265711e74d8d2d8b74e94c262527/ff_rd059_003_en.pdf](https://asset.fujifilm.com/www/jp/files/2019-12/5334265711e74d8d2d8b74e94c262527/ff_rd059_003_en.pdf)

16. **AAPM — Digital Radiography Secondary Quanta Detector**  
    [https://www.aapm.org/meetings/amos2/pdf/26-5957-59342-727.pdf](https://www.aapm.org/meetings/amos2/pdf/26-5957-59342-727.pdf)

17. **PRORAD FPD Operation Manual**  
    [https://fcc.report/FCC-ID/2A7E500001/6084598.pdf](https://fcc.report/FCC-ID/2A7E500001/6084598.pdf)

18. **a-Si:H TFT bipolar bias (collectionscanada.gc.ca)**  
    [https://www.collectionscanada.gc.ca/obj/s4/f2/dsk4/etd/NQ83000.PDF](https://www.collectionscanada.gc.ca/obj/s4/f2/dsk4/etd/NQ83000.PDF)

---

*Research compiled March 18, 2026. All timing values and specifications should be verified against the specific IC datasheets for the target panel. NT39565D-specific values (dual STV, CPV naming) are inferred from the Novatek family architecture; obtain the actual NT39565D datasheet from Novatek directly for confirmed specifications.*
