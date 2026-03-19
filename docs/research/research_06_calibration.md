# Offset, Gain, and Bad Pixel Correction for X-ray Flat Panel Detectors
## Comprehensive Technical Research Report

**Prepared:** March 2026  
**Scope:** Dark frame (offset) correction, flat field (gain) correction, two-point/multi-point calibration, bad pixel detection and replacement, temperature drift compensation, calibration data storage, FPGA pipeline design, and IEC 62220-1 standards.

---

## Table of Contents

1. [Detector Architecture Overview](#1-detector-architecture-overview)
2. [Dark Frame (Offset) Correction](#2-dark-frame-offset-correction)
3. [Flat Field (Gain) Correction](#3-flat-field-gain-correction)
4. [Two-Point (Dark + Bright) Correction](#4-two-point-dark--bright-correction)
5. [Multi-Point Gain Correction](#5-multi-point-gain-correction)
6. [Bad Pixel Detection](#6-bad-pixel-detection)
7. [Bad Pixel Replacement](#7-bad-pixel-replacement)
8. [Temperature Drift Compensation](#8-temperature-drift-compensation)
9. [Calibration Data Storage Architecture](#9-calibration-data-storage-architecture)
10. [FPGA-Based Correction Pipeline](#10-fpga-based-correction-pipeline)
11. [IEC 62220-1 Standard: DQE, MTF, NPS](#11-iec-62220-1-standard-dqe-mtf-nps)
12. [AAPM TG-150 Quality Control Recommendations](#12-aapm-tg-150-quality-control-recommendations)
13. [Performance Benchmarks from Literature](#13-performance-benchmarks-from-literature)
14. [Summary: Design Recommendations for 3072×3072 Detector](#14-summary-design-recommendations-for-3072×3072-detector)

---

## 1. Detector Architecture Overview

### 1.1 Amorphous Silicon (a-Si:H) Flat Panel Detector Structure

A medical X-ray flat panel detector (FPD) typically consists of:

```
X-ray photons
    ↓
Scintillator layer (CsI:Tl or GOS, Gd₂O₂S:Tb)
    ↓ converts X-rays → visible light
a-Si:H photodiode array (NIP or PIN structure)
    ↓ converts visible light → charge
TFT switching array (a-Si:H or LTPS TFTs)
    ↓
Charge amplifier readout (per column)
    ↓
ADC (12–16 bit)
    ↓
Digital pixel data
```

**Key pixel parameters (typical values):**

| Parameter | Typical Value | Notes |
|-----------|--------------|-------|
| Pixel pitch | 139–200 µm (radiography); 70–100 µm (mammography) | |
| Fill factor | 70–85% | Active area fraction |
| Photodiode capacitance | 1.9–3.5 pF | Per pixel |
| Dark current density | < 1 nA/cm² at −4V bias | At 25°C |
| Pixel ADC resolution | 14–16 bits | |
| Array size (large-area) | 2048×2048 to 3072×3072 | |
| Quantum efficiency | > 80% at 550 nm | For visible light |

Sources: [Flat-Panel Imaging Arrays for Digital Radiography (Tredwell, 2009)](https://www.imagesensors.org/Past%20Workshops/2009%20Workshop/2009%20Papers/065_paper_tredwell_invited.pdf); [Amorphous Silicon X-Ray Detectors (Hoheisel, 1996)](https://www.mhoheisel.de/docs/ISCMP91996112.pdf)

### 1.2 Fixed Pattern Noise Sources

Fixed pattern noise (FPN) in a-Si:H FPDs arises from:

1. **Offset (dark) non-uniformity** — pixel-to-pixel variation in dark current and readout electronics offset
2. **Gain (sensitivity) non-uniformity** — variation in photodiode quantum efficiency, fill factor, scintillator thickness, and charge amplifier gain
3. **Defective pixels** — pixels with offset or gain outside correctable range (manufacturing defects, radiation damage)
4. **Column/row non-uniformity** — systematic variation from shared readout amplifiers

---

## 2. Dark Frame (Offset) Correction

### 2.1 Physical Origin

The dark signal in an a-Si:H detector accumulates during the integration period even without X-ray exposure. Sources include:
- Thermal generation of electron-hole pairs in the a-Si:H photodiode (dark current)
- Leakage through TFT switching elements
- Electronic offset in readout amplifiers and ADCs
- Charge trapping/release in a-Si:H defect states (metastable states)

The dark signal follows a linear model:
```
D(i,j,t,T) = D_offset(i,j) + D_current(i,j,T) × t_int
```
where:
- `D_offset(i,j)` = per-pixel electronic offset (ADU)
- `D_current(i,j,T)` = per-pixel dark current (ADU/s) at temperature T
- `t_int` = integration time (s)

### 2.2 Dark Frame Acquisition Protocol

**Standard procedure:**

```
DARK FRAME ACQUISITION PROTOCOL
================================
1. PREPARATION
   - Block all X-ray from reaching the detector (shutter closed or X-ray off)
   - Wait for detector thermal stabilization (typically 15–30 min after power-on)
   - Execute detector "refresh/prepare" cycles (a-Si:H trap state stabilization)

2. ACQUISITION
   - Acquire N dark frames sequentially (same integration time, same gain mode)
   - Typical N = 16–128 frames (larger N → lower noise in dark map)
   - Recommended: N = 64 for good noise reduction with acceptable time

3. AVERAGING
   D_map(i,j) = (1/N) × Σ[n=1 to N] D_n(i,j)
   
   Noise reduction: σ_dark_map = σ_single / √N

4. STORAGE
   - Store D_map as 16-bit integer or 32-bit float per pixel
   - Tag with temperature, integration time, gain mode metadata
   - For 3072×3072 @ 16-bit: 18.9 MB per dark map
```

**Noise reduction from averaging:**

| N frames averaged | Noise reduction | Remaining % of single-frame noise |
|-------------------|-----------------|-----------------------------------|
| 4 | 2× | 50% |
| 16 | 4× | 25% |
| 64 | 8× | 12.5% |
| 256 | 16× | 6.25% |

Sources: [Dark correction for digital X-ray detector, EP2148500A1](https://patents.google.com/patent/EP2148500A1/en); [Granfors et al., US2003/0223539]

### 2.3 Update Frequency and Temperature Dependence

Dark map update frequency is driven by detector temperature stability:

**Update triggers:**
1. **After power-up / mode change** — thermal transient settles over 2–15 minutes; portable DR detectors require adaptive correction
2. **Periodic in-session update** — every 1–5 minutes for high-drift environments (e.g., portable operation from 15–35°C ambient)
3. **Temperature-triggered** — update when ΔT > 2–3°C from last calibration temperature
4. **Weighted rolling update** — single dark frame periodically blended with existing map:
   ```
   D_map_new = α × D_new + (1 − α) × D_map_old    (typical α = 0.1–0.3)
   ```
   This approach (Granfors et al., US2003/0223539) captures long-term drift while reducing noise.

**Temperature sensitivity:**
- Dark current doubles approximately every **5–10°C** for silicon-based devices (follows Arrhenius law)
- For a-Si:H specifically: dark current increases exponentially with temperature; compensation requires per-pixel temperature model
- Rule of thumb for bulk silicon detectors: **leakage current doubles every ~8°C**

```
Dark current temperature model (Arrhenius):
I_dark(T) = I_0 × exp(-ΔE / kT)

where:
  ΔE = activation energy (~0.55–1.1 eV for a-Si:H, depending on mechanism)
  k  = Boltzmann constant (8.617×10⁻⁵ eV/K)
  T  = absolute temperature (K)
```

A per-pixel linear model for temperature compensation:
```
D_dark(i,j,T) = a(i,j) + b(i,j) × T
```
where coefficients a(i,j) and b(i,j) are calibrated at factory for each pixel.

Sources: [Silicon Strip Detectors for Scanned Multi-Slit X-Ray Imaging](http://www.diva-portal.org/smash/get/diva2:9333/FULLTEXT01.pdf); [CMOS Image Sensor Dark Current Compensation (Sensors, 2023)](https://pmc.ncbi.nlm.nih.gov/articles/PMC10674984/); [Amorphous Silicon X-Ray Detectors (Hoheisel)](https://www.mhoheisel.de/docs/ISCMP91996112.pdf)

### 2.4 Practical Considerations for Portable Detectors

For portable/wireless FPDs (operating at 15–35°C ambient), per [EP2148500A1](https://patents.google.com/patent/EP2148500A1/en):

```
PORTABLE DETECTOR DARK CORRECTION ALGORITHM
============================================
1. At factory: acquire "reference dark map" at multiple temperatures
   - T1 = 15°C, T2 = 20°C, T3 = 25°C, T4 = 30°C, T5 = 35°C
   - 64 frames per temperature point
   
2. Store per-pixel temperature offset coefficients (difference maps)

3. At runtime:
   a. Acquire 1–2 dark frames (no time for large N)
   b. Read current temperature T_now
   c. Interpolate/extrapolate from stored maps:
      D_corrected_map = D_ref(T_nearest) + ΔD(T_now)
   d. Apply combined dark correction
```

**Evaluation result** (from patent EP2148500A1): 72 flat-field exposures at 15–35°C ambient, 1–70 min between studies, demonstrated effective correction with residual non-uniformity < 0.5%.

---

## 3. Flat Field (Gain) Correction

### 3.1 Gain Map Acquisition

**Standard flood field protocol:**

```
GAIN MAP ACQUISITION PROTOCOL
==============================
1. SETUP
   - Remove all objects from X-ray beam path (flat, uniform exposure)
   - Use beam quality matching clinical use (e.g., RQA-5: 74 kVp, 21 mm Al filtration)
   - SID: same as clinical use (or manufacturer specification)
   - Ensure dose is in detector's linear operating range (NOT near saturation)
   - Typical: 1–5 µGy per frame for 74 kVp

2. ACQUISITION
   - Acquire N flat field frames (N = 64–256 typical)
   F_avg(i,j) = (1/N) × Σ[n=1 to N] F_n(i,j)
   
3. DARK CORRECTION
   F_dark_corr(i,j) = F_avg(i,j) - D_map(i,j)
   
4. MEAN COMPUTATION
   M = mean(F_dark_corr) = (1/(Nx×Ny)) × ΣΣ F_dark_corr(i,j)
   
5. NORMALIZED GAIN MAP
   G(i,j) = M / F_dark_corr(i,j)
   
   Or equivalently, store:
   Gain_norm(i,j) = F_dark_corr(i,j) / M    [ranges ~0.7 to 1.3 typical]
```

**Pixel-to-pixel gain variation:**
- Typical variation ±10–20% across detector area for a-Si:H systems
- Sources: scintillator thickness variation (±5%), pixel photodiode QE variation (±3–5%), readout amplifier gain variation (±2–5%)
- After correction: residual non-uniformity typically < 0.5–1%

Sources: [Flat-field correction (Wikipedia)](https://en.wikipedia.org/wiki/Flat-field_correction); [Dark correction for digital X-ray detector, EP2148500A1](https://patents.google.com/patent/EP2148500A1/en); [Heel effect adaptive flat field correction (Wang, 2013)](https://pubmed.ncbi.nlm.nih.gov/23927327/)

### 3.2 Non-Uniformity Sources in Gain Map

| Source | Typical Magnitude | Spatial Frequency |
|--------|-------------------|-------------------|
| Scintillator thickness variation | ±3–8% | Low (smooth) |
| Individual pixel QE variation | ±2–5% | High (random) |
| Readout amplifier gain variation | ±1–3% | Column-correlated |
| Heel effect (anode-cathode axis) | 5–20% gradient | Very low (gradual) |
| Beam hardening (clinical) | 2–10% | Low (varies with object) |

### 3.3 Scintillator Non-Uniformity Correction

For structured scintillators (CsI:Tl needle arrays):
- Coupling efficiency of individual needles varies → structured gain pattern (needle period ~5–10 µm)
- High-frequency gain map component must be included or removed by frequency decomposition

For powder (particulate) scintillators (GOS):
- Relatively smooth gain variation at pixel scale
- Long-range (low-frequency) non-uniformity from thickness gradients

**Filtered gain calibration** (Rodricks et al., SPIE Vol. 3977, 1998):
- Decompose gain map into high-frequency and low-frequency components
- High-frequency component (pixel-level) → apply directly
- Low-frequency component → can be updated more frequently with fewer frames (less noise penalty)

---

## 4. Two-Point (Dark + Bright) Correction

### 4.1 Standard Correction Equation

The fundamental two-point calibration corrects both offset and gain in a single operation:

```
STANDARD TWO-POINT CORRECTION FORMULA
======================================

           (Raw(i,j) - Dark(i,j))
Corr(i,j) = ─────────────────────── × M
           (Bright(i,j) - Dark(i,j))

where:
  Raw(i,j)    = uncorrected pixel value (ADU)
  Dark(i,j)   = dark map value (ADU), i.e., D_map(i,j)
  Bright(i,j) = offset-corrected flat field value (ADU) = F_avg(i,j) - D_map(i,j)
  M           = image-averaged value of (Bright - Dark) [scalar normalization]
  Corr(i,j)   = corrected pixel value (ADU)
```

Equivalent forms:

```
Form 1 (direct):
  Corr = (Raw - Dark) / (Bright - Dark) × M

Form 2 (via gain map, pre-computed):
  Gain(i,j) = M / (Bright(i,j) - Dark(i,j))
  Corr(i,j) = (Raw(i,j) - Dark(i,j)) × Gain(i,j)

Form 3 (normalized, X-ray tomography convention):
  N = (P - D) / (F - D)
  where P = projection with sample, F = flat field, D = dark field
  (result N is normalized transmission, not absolute counts)
```

**FPGA-friendly form** (integer arithmetic, pre-computed gain):
```
1. Pre-compute and store (offline):
   DARK_MAP[i][j]  = D_map(i,j)          [16-bit integer]
   GAIN_LUT[i][j]  = round(M/Bright[i][j] × 2^16)   [16-bit Q16 fixed-point]

2. Real-time correction (per pixel):
   temp = RAW[i][j] - DARK_MAP[i][j]     [17-bit signed]
   CORR[i][j] = (temp × GAIN_LUT[i][j]) >> 16   [16-bit result]
```

Sources: [Flat-field correction (Wikipedia)](https://en.wikipedia.org/wiki/Flat-field_correction); [Dark correction patent EP2148500A1](https://patents.google.com/patent/EP2148500A1/en)

### 4.2 Algorithm Flowchart

```
╔══════════════════════════════════════════════════════════╗
║          TWO-POINT CORRECTION PIPELINE                   ║
╠══════════════════════════════════════════════════════════╣
║                                                          ║
║  CALIBRATION PHASE (offline):                           ║
║  ─────────────────────────────                          ║
║  [Acquire N dark frames] → average → DARK_MAP           ║
║         ↓                                               ║
║  [Acquire N bright frames] → average → BRIGHT_AVG      ║
║         ↓                                               ║
║  BRIGHT_CORR = BRIGHT_AVG − DARK_MAP                   ║
║         ↓                                               ║
║  M = mean(BRIGHT_CORR)                                  ║
║         ↓                                               ║
║  GAIN_MAP = M / BRIGHT_CORR  (per pixel)               ║
║         ↓                                               ║
║  [Store DARK_MAP + GAIN_MAP to Flash/SRAM]              ║
║                                                          ║
║  CORRECTION PHASE (real-time per frame):               ║
║  ─────────────────────────────────────                  ║
║  [Acquire RAW frame]                                    ║
║         ↓                                               ║
║  OFFSET_CORR = RAW − DARK_MAP                          ║
║         ↓                                               ║
║  GAIN_CORR = OFFSET_CORR × GAIN_MAP                    ║
║         ↓                                               ║
║  [Bad pixel replacement]                                ║
║         ↓                                               ║
║  FINAL_IMAGE                                            ║
╚══════════════════════════════════════════════════════════╝
```

### 4.3 Dark Map Update with Metadata

For portable detectors, the correction uses metadata-linked dark maps:

```
METADATA-LINKED DARK CORRECTION (Granfors EP2148500A1)
=======================================================
Metadata fields:
  - Integration time
  - Detector temperature  
  - Power mode (standby/active)
  - Time since last exposure
  - Prep time (time from ready to exposure)

Algorithm:
  1. Capture exposure image + metadata
  2. Find stored reference dark map with matching metadata
  3. Compute intermediate correction: RAW − D_reference
  4. Apply offset adjustment map (computed from stored
     difference maps between temperature points)
  5. Output: corrected image
```

---

## 5. Multi-Point Gain Correction

### 5.1 Motivation

The linear two-point model fails when:
1. Detector response is non-linear (a-Si:H saturates above ~80% of full-scale)
2. Tube voltage for patient imaging differs from calibration voltage
3. Beam hardening through object changes effective spectrum
4. Multi-gain-mode detector requires calibration per mode

### 5.2 Multi-Point (Dose-Linearity) Correction

**Calibration procedure:**

```
MULTI-POINT GAIN CALIBRATION
==============================
1. Acquire flat field images at K exposure levels:
   E_1, E_2, ..., E_K  (distributed across operating range)
   Typical: K = 6–12 levels; E range = 0.05–10 µGy per frame

2. For each pixel (i,j), fit a polynomial or LUT:
   Response(i,j, E) = f(E; c_0(i,j), c_1(i,j), ..., c_P(i,j))
   
   Linear fit (2-point):
     Response = c_1 × E + c_0
   
   Quadratic fit (for mild nonlinearity):
     Response = c_2 × E² + c_1 × E + c_0
   
   Lookup table (most general):
     LUT(i,j)[k] = Response at E_k for pixel (i,j)

3. Correction at runtime:
   Given RAW(i,j), find corrected value via LUT interpolation:
     Dose_corr(i,j) = LUT_inverse(i,j, RAW(i,j) - DARK(i,j))
     Normalized(i,j) = Dose_corr(i,j) / mean(Dose_corr)
```

**Variable flat field (VFF) correction** (nonlinear model):
- Accounts for nonlinearity of individual pixel response functions
- Uses inverse function `f⁻¹` learned through sampling and regression
- More effective than single-point FFF when exposure differs from calibration

**Beam hardening–respecting (BHR) correction** ([Wang & Yu, ICIP 2012](https://www.math.union.edu/~wangj/papers/Wang12.Flat%20Field%20Correction%20%5BICIP%5D.pdf)):
- Extends multi-dimensional signal model with tube voltage and anatomy filtration
- Achieves 70–80% lower correction error than FFF and VFF for non-matching kVp
- Requires calibration at multiple kVp and filtration conditions (~1–2 hrs)

### 5.3 Lookup Table Approach

**Memory requirements for per-pixel LUT:**

```
LUT Architecture for 3072×3072 detector:
  K = 8 exposure levels per pixel
  Each LUT entry: 16-bit
  Memory: 3072 × 3072 × 8 × 2 bytes = 151 MB
  
Compressed approach (shared LUT per column or per group):
  Groups of 64×64 pixels share same LUT → 48×48 groups × 8 × 2 = 36 KB
  (Acceptable if within-group gain variation < 0.5%)
```

---

## 6. Bad Pixel Detection

### 6.1 Statistical Thresholding (Factory/Periodic)

**Standard offset-based detection:**

```
BAD PIXEL DETECTION — OFFSET THRESHOLDING
==========================================
1. Acquire N dark frames (N ≥ 64)
2. Compute per-pixel dark map D(i,j)
3. Compute image statistics:
     μ_dark = mean(D)
     σ_dark = std(D)
4. Identify bad offset pixels:
     if D(i,j) > μ_dark + k_high × σ_dark → HIGH_OFFSET_DEAD
     if D(i,j) < μ_dark - k_low × σ_dark → LOW_OFFSET_DEAD
     
   Typical: k_high = 4–10, k_low = 4–10
```

**Gain-based detection using median filtering:**

```
BAD PIXEL DETECTION — GAIN THRESHOLDING
========================================
1. Acquire N flat field frames, compute F_corr = F_avg - D_map
2. Apply median filter with kernel K×K (K=5 typical):
     F_smooth(i,j) = median_filter(F_corr, K=5)
3. Compute normalized deviation:
     Residual(i,j) = F_corr(i,j) - F_smooth(i,j)
4. Compute residual statistics:
     μ_res = mean(Residual)
     σ_res = std(Residual)
5. Identify bad gain pixels:
     if |Residual(i,j)| > k × σ_res → BAD_GAIN_PIXEL
     
   Typical: k = 3–5 sigma threshold
```

**Why median filtering:** X-ray field non-uniformity and electronics gain differences vary slowly with position (low spatial frequency), while bad pixels are highly localized (high spatial frequency). The median filter suppresses bad pixels while preserving the background. ([US5657400A](https://patents.google.com/patent/US5657400A/en))

### 6.2 Multi-Exposure Bad Pixel Detection

Pixels may have "correct" offset but non-linear response — identifiable only via multiple exposure levels:

```
MULTI-EXPOSURE BAD PIXEL TEST
==============================
For each pixel (i,j), test at exposures E_low, E_mid, E_high:
  S_low = F(i,j, E_low) - D(i,j)
  S_mid = F(i,j, E_mid) - D(i,j)
  S_high = F(i,j, E_high) - D(i,j)

Linearity check:
  Slope = (S_high - S_low) / (E_high - E_low)
  Expected_S_mid = S_low + Slope × (E_mid - E_low)
  if |S_mid - Expected_S_mid| > threshold → NONLINEAR_PIXEL
```

### 6.3 Dynamic Bad Pixel Detection (Online)

For runtime detection of newly degraded pixels, an FPGA-based adaptive algorithm ([MIPRO 2012](https://mipro-proceedings.com/sites/mipro-proceedings.com/files/upload/sp/sp_009.pdf)):

```
DYNAMIC BAD PIXEL DETECTION ALGORITHM
======================================
Stage 1: CANDIDATE SCREENING (per frame)
  For each pixel (i,j) in current frame:
    avg_neighbors = mean of same-color adjacent pixels in 5×5 window
    if |pixel(i,j) - avg_neighbors| > threshold_1:
      → mark as CANDIDATE, save coordinates to Candidate FIFO

Stage 2: DEFECT CONFIRMATION (multi-frame)
  For each candidate in FIFO:
    Compare current value to stored candidate value:
    if |current - stored| ≤ noise_threshold:
      increment time_counter
    if time_counter > time_threshold:
      → confirm as DEFECT, move to Defect FIFO
      → periodically recheck to avoid permanent false positives

Stage 3: CORRECTION
  If pixel coordinates match entry in Defect FIFO:
    → replace with interpolated value (see Section 7)
```

**FPGA resource usage for dynamic detection** (ECP2 FPGA):
- Registers: 1,018
- LUT4s: 1,332
- EBR (Block RAM): 8
- Maximum frequency: 108 MHz

### 6.4 Bad Pixel Map Storage Formats

```
BAD PIXEL MAP ENCODING
=======================
Option A: Bitmap (1 bit per pixel)
  3072 × 3072 bits = 1.15 MB
  Access: random access by address

Option B: Coordinate list (12 bytes per bad pixel)
  At 0.1% defect rate: ~9,400 entries × 12 bytes = 113 KB
  Access: requires search → use sorted list or hash map

Option C: FPGA-optimized: Correction code per bad pixel
  Per [US5657400A]: stores {pixel_address, correction_code}
  correction_code (4-bit) encodes which neighbor(s) to use
  Example codes: N+S average, E+W average, NW+SE average, etc.
  During readout: FIFO-ordered list enables in-stream correction
```

Sources: [US5657400A — Automatic identification and correction of bad pixels](https://patents.google.com/patent/US5657400A/en); [Deep learning for pixel-defect corrections in flat-panel X-ray (PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC7930811/)

---

## 7. Bad Pixel Replacement

### 7.1 Nearest-Neighbor Replacement

The simplest method: replace bad pixel with value of nearest good neighbor.

```
NEAREST-NEIGHBOR PIXEL REPLACEMENT
=====================================
Priority order (per US5657400A):
  1. Average of 2 nearest good neighbors (N+S, E+W, NW+SE, NE+SW)
  2. Average of 2 next-nearest good neighbors (diagonal pairs)
  3. Single nearest good neighbor
  4. Single next-nearest good neighbor

Neighbor map (8-connectivity):
  NW | N | NE
  ───┼───┼───
   W | X | E
  ───┼───┼───
  SW | S | SE
  
X = bad pixel
Priority: N+S > E+W > NW+SE > NE+SW > N > E > S > W > NW > NE > SW > SE
```

**Advantages:** O(1) hardware implementation; single clock cycle; minimal resources  
**Disadvantages:** Does not preserve edges; poor for clusters of bad pixels

### 7.2 Bilinear Interpolation

```
BILINEAR INTERPOLATION
=======================
For isolated bad pixel at (i,j):
  Use 4 nearest good pixels (N, S, E, W):
  
  Corr(i,j) = [w_N × P(i-1,j) + w_S × P(i+1,j) +
               w_E × P(i,j+1) + w_W × P(i,j-1)] / (w_N+w_S+w_E+w_W)
               
  Uniform weights: Corr(i,j) = (P(i-1,j)+P(i+1,j)+P(i,j+1)+P(i,j-1)) / 4

  With distance weighting (higher quality):
  w_k = 1 / dist_k²
```

### 7.3 Directional Interpolation (FPGA-Optimized)

For edge-preserving replacement, use directional pairs:

```
DIRECTIONAL INTERPOLATION (Side-Window Filter Method)
======================================================
Compute 8 directional estimates:
  P_E  = linear interp from East neighbor pair
  P_W  = linear interp from West neighbor pair
  P_N  = linear interp from North neighbor pair
  P_S  = linear interp from South neighbor pair
  P_NE = linear interp from NE-SW diagonal pair
  P_NW = linear interp from NW-SE diagonal pair
  P_EN = 45° right diagonal
  P_WN = 45° left diagonal

Weight by inverse of distance²:
  Best estimate = arg_min(k) of |P_k - mean(all)|

FPGA implementation:
  - 8 convolution kernels computed simultaneously (parallel)
  - Comparator tree selects minimum-weight result
  - 2-line buffer sufficient for 3×3 neighborhood
  
  Resource saving: no intermediate storage needed if
  8 kernels computed in parallel and compared in single pass
```

Source: [Lightweight FPGA Infrared Image Processor (Sensors 2024)](https://pmc.ncbi.nlm.nih.gov/articles/PMC10893426/)

### 7.4 Bad Pixel Cluster Handling

For clusters (2×2, 3×3, or larger defect regions):

```
CLUSTER BAD PIXEL REPLACEMENT
===============================
Strategy 1: Ordered replacement (radially outward)
  1. Identify cluster boundary pixels (adjacent to good pixels)
  2. Replace boundary pixels first using available good neighbors
  3. Use newly-replaced boundary values for interior pixels
  4. Iterate until cluster is filled

Strategy 2: Inpainting (deep learning)
  Per [PMC7930811]: CNN-based correction outperforms template
  matching for 3×3 and 5×5 defect clusters
  MSE comparison (3×3 defect block):
    - Template Match Correction (TMC): baseline
    - ANN (single layer): MSE = 69.40 (vs TMC)
    - CNN: MSE = 75.13
    - Concatenate CNN: MSE = 68.21 (best)
    - GAN: MSE = 73.77
  
  ANN preferred for hardware: comparable MSE to CNN,
  much lower encoding complexity → suitable for FPGA
```

Source: [Using deep learning for pixel-defect corrections (PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC7930811/)

---

## 8. Temperature Drift Compensation

### 8.1 Dark Current Temperature Coefficient

**Arrhenius model for dark current:**

```
I_dark(T) = I_0 × exp(-ΔE / k_B × T)

For a-Si:H at room temperature:
  Activation energy ΔE ≈ 0.55–0.80 eV (depletion-dominated)
  At higher temperatures: ΔE → 1.1 eV (diffusion-dominated)
  
Doubling rule:
  - General silicon detectors: doubles every ~8°C
  - CMOS image sensors: doubles every 5–10°C
  - CCD: follows similar Arrhenius behavior
  
Example for 25°C baseline:
  T=33°C: ~2× dark current
  T=41°C: ~4× dark current
  T=49°C: ~8× dark current
```

**Impact on correction quality:**
- Without temperature compensation: 5°C drift → ~41% dark map error
- Without temperature compensation: 10°C drift → ~100% dark map error
- Residual dark map error amplified by subsequent gain correction step

Sources: [Temperature dependence of dark current in CCD (PDX)](https://web.pdx.edu/~d4eb/ccd/SPIE_2002.pdf); [CMOS Image Sensor Dark Current Compensation (Sensors 2023)](https://pmc.ncbi.nlm.nih.gov/articles/PMC10674984/); [Silicon Strip Detectors](http://www.diva-portal.org/smash/get/diva2:9333/FULLTEXT01.pdf)

### 8.2 Thermal Model for Real-Time Compensation

**Multi-point thermal calibration (preferred for portable FPDs):**

```
THERMAL CALIBRATION PROCEDURE
==============================
1. Factory calibration at M temperature points:
   T_1 = 15°C, T_2 = 20°C, T_3 = 25°C, T_4 = 30°C, T_5 = 35°C
   
2. Per pixel, acquire dark map D_m(i,j) at each T_m
   Average N=64 frames per temperature point

3. Fit per-pixel thermal model:
   Linear model: D(i,j,T) = a(i,j) + b(i,j) × T
   - a(i,j) = intercept (ADU)  
   - b(i,j) = slope (ADU/°C)
   - Fit quality: R² typically > 0.99 for a-Si:H in 15–35°C range
   
   Quadratic model (higher accuracy):
   D(i,j,T) = a(i,j) + b(i,j) × T + c(i,j) × T²

4. Storage: a, b coefficients per pixel
   At 32-bit float: 3072×3072 × 2 × 4 bytes = 75 MB

5. Runtime compensation:
   T_now = read_temperature_sensor()
   D_compensated(i,j) = a(i,j) + b(i,j) × T_now
   (or use pre-computed LUT of dark maps at discrete T)
```

### 8.3 Temperature Sensor Placement

For 3072×3072 detector arrays (30 cm × 30 cm typical):

```
TEMPERATURE SENSOR DISTRIBUTION
================================
Recommended: 16–64 sensors distributed across array

Placement strategy:
  - 4 corners + center (minimum viable): 5 sensors
  - 4×4 grid: 16 sensors (recommended)
  - 8×8 grid: 64 sensors (high-accuracy thermal mapping)

Temperature interpolation across array:
  - Bilinear (4 sensors): sufficient if gradient < 2°C across array
  - Bicubic (16 sensors): better for non-uniform heating
  
Typical thermal gradients:
  - Near readout electronics: +2 to +5°C above array center
  - Near power supply: +3 to +8°C
  - Corner sensors: 1–3°C cooler than center under steady state
```

Source: [In-pixel temperature sensors for dark current compensation (TU Delft, 2021)](https://research.tudelft.nl/files/96743059/Thesis_AAbarca_v9.pdf)

### 8.4 Synthetic Dark Correction (Scene-Based)

For systems where shutter dark frames are impractical, a synthetic model approach ([AMOS 2022](https://amostech.com/TechnicalPapers/2022/Poster/Chrien.pdf)):

```
SYNTHETIC DARK CORRECTION
==========================
1. Build temperature-parameterized dark model from
   historical data (frames with known low stray light)
   
2. Per pixel, fit: D(i,j,T) = a(i,j) + b(i,j) × T
   (first-order polynomial adequate per Chrien 2022)
   
3. For each raw frame:
   a. Record focal plane temperature T_raw
   b. Compute synthetic dark: D_synth(i,j) = a(i,j) + b(i,j) × T_raw
   c. Subtract: F_corr = F_raw - D_synth
```

---

## 9. Calibration Data Storage Architecture

### 9.1 Memory Requirements for 3072×3072 Detector

**Complete calibration dataset:**

| Data Element | Size (16-bit) | Size (32-bit float) | Notes |
|---|---|---|---|
| Dark map (1 temp point) | 18.9 MB | 37.7 MB | Per gain mode |
| Dark maps (5 temp points) | 94.4 MB | 188.5 MB | Full thermal model |
| Gain map (1 calibration) | 18.9 MB | 37.7 MB | Per kVp setting |
| Gain maps (4 kVp settings) | 75.5 MB | 150.9 MB | RQA 3,5,7,9 |
| Bad pixel map (bitmap) | 1.15 MB | — | 0/1 per pixel |
| Bad pixel correction codes | ~113 KB | — | At 0.1% defect rate |
| Per-pixel thermal coeff (a,b) | — | 75.4 MB | 2 coefficients |
| Multi-point LUT (8 levels) | 151.0 MB | 302.0 MB | Full dose range |
| **Total (comprehensive)** | **~360 MB** | **~~793 MB** | |

**Practical minimum for single gain/kVp mode:**

| Data Element | Memory |
|---|---|
| Dark map (16-bit) | 18.9 MB |
| Gain map (16-bit) | 18.9 MB |
| Bad pixel map (1-bit) | 1.15 MB |
| **Minimum total** | **~39 MB** |

### 9.2 Memory Architecture Recommendation

**Recommended architecture for FPGA-based correction:**

```
CALIBRATION DATA MEMORY ARCHITECTURE
======================================

┌─────────────────────────────────────────────────────────┐
│                    HOST SYSTEM                           │
│  ┌────────────┐    ┌────────────┐    ┌────────────────┐ │
│  │  Flash NVM │    │    DRAM    │    │   Host CPU     │ │
│  │ 256–512 MB │    │ 512MB–2GB  │    │                │ │
│  │            │    │            │    │                │ │
│  │ Persistent:│    │ Runtime:   │    │ Calibration    │ │
│  │ - Dark maps│    │ - Dark map │    │ management     │ │
│  │ - Gain maps│    │ - Gain map │    │ software       │ │
│  │ - Bad pixel│    │ - Working  │    │                │ │
│  │   maps     │    │   buffers  │    │                │ │
│  │ - Thermal  │    │            │    │                │ │
│  │   coeffs   │    │            │    │                │ │
│  └────────────┘    └────────────┘    └────────────────┘ │
└──────────────────────────┬──────────────────────────────┘
                           │ PCIe / Camera Link / USB 3.0
┌──────────────────────────▼──────────────────────────────┐
│              DETECTOR EMBEDDED ELECTRONICS               │
│                                                          │
│  ┌──────────┐  ┌──────────┐  ┌────────────────────────┐ │
│  │  FLASH   │  │  SRAM    │  │        FPGA             │ │
│  │ 64–128MB │  │ 256–512MB│  │                         │ │
│  │(SPI/QSPI)│  │ (DDR3/4) │  │ ┌─────────────────────┐│ │
│  │          │  │          │  │ │ Pipeline:           ││ │
│  │ Active   │  │ Ping-pong│  │ │ Dark subtract       ││ │
│  │ dark map │  │ frame    │  │ │ Gain multiply       ││ │
│  │ Active   │  │ buffers  │  │ │ Bad pixel replace   ││ │
│  │ gain map │  │ (3 frames│  │ └─────────────────────┘│ │
│  │ Bad pixel│  │ = 170 MB)│  │                         │ │
│  │ map      │  │          │  │ Block RAMs (BRAM):      │ │
│  └──────────┘  └──────────┘  │  - Row line buffers     │ │
│                              │  - Bad pixel FIFO       │ │
│  ┌──────────────────────┐    │  - Coeff cache          │ │
│  │ Temperature Sensors  │    └────────────────────────┘ │
│  │ (16–64 distributed)  │                               │
│  └──────────────────────┘                               │
└─────────────────────────────────────────────────────────┘
```

### 9.3 SRAM/Flash Partitioning

**Flash (Non-volatile, persistent storage):**
- Factory dark maps at 3–5 temperature points: 57–94 MB
- Factory gain maps at 1–4 kVp settings: 19–76 MB  
- Bad pixel bitmap + correction codes: 2–4 MB
- Per-pixel thermal coefficients (a, b): 75 MB (32-bit)
- Firmware + metadata: 4–8 MB
- **Recommended Flash: 256 MB (NOR/QSPI for fast random access)**

**SRAM/DDR (Volatile, high-speed working memory):**
- Active dark map (current temperature): 19 MB
- Active gain map: 19 MB
- Ping-pong frame buffers (3× 3072×3072×16-bit): 57 MB
- Bad pixel list (sorted FIFO): 1 MB
- Working registers and pipeline buffers: 4 MB
- **Recommended: 128–512 MB LPDDR4**

Source: [Real Time NUC Algorithm and Implementation (Semanticscholar)](https://pdfs.semanticscholar.org/fd8c/90995c06103ff7a863f58a4266a40dcca319.pdf)

---

## 10. FPGA-Based Correction Pipeline

### 10.1 Throughput Requirements

For a 3072×3072 detector at various frame rates:

| Frame Rate | Pixels/second | Data rate (16-bit) | Clock required (pipeline) |
|---|---|---|---|
| 1 fps | 9.4 Mpix/s | 150 Mbps | 9.4 MHz minimum |
| 5 fps | 47 Mpix/s | 754 Mbps | 47 MHz |
| 15 fps (fluoroscopy) | 141 Mpix/s | 2.26 Gbps | 141 MHz |
| 30 fps | 282 Mpix/s | 4.52 Gbps | 282 MHz |

Most modern FPGAs operate at 200–500 MHz → easily handles 30 fps at 3072×3072 with margin for pipelining.

### 10.2 Pipelined Correction Architecture

```
FPGA CORRECTION PIPELINE (per pixel, 3 pipeline stages)
=========================================================

Input: RAW_PIXEL[15:0] (16-bit, synchronous with pixel clock)
       ADDR_ROW[11:0], ADDR_COL[11:0]

Stage 1: DARK SUBTRACTION (1 clock cycle)
────────────────────────────────────────
  DARK_VALUE ← BRAM_DARK[ADDR_ROW][ADDR_COL]   (1-cycle BRAM read)
  TEMP ← RAW_PIXEL - DARK_VALUE                 (16-bit subtractor)
  Check: if TEMP < 0 → clamp to 0               (underflow protection)

Stage 2: GAIN MULTIPLICATION (2 clock cycles)
─────────────────────────────────────────────
  GAIN_VALUE ← BRAM_GAIN[ADDR_ROW][ADDR_COL]   (Q16 fixed-point, 1-cycle)
  PRODUCT ← TEMP × GAIN_VALUE                  (32-bit multiply)
  RESULT ← PRODUCT[31:16]                       (right-shift 16, Q16 → integer)
  (uses DSP48 block for single-cycle multiply)

Stage 3: BAD PIXEL REPLACEMENT (variable, 1–3 cycles)
──────────────────────────────────────────────────────
  BPM_FLAG ← BRAM_BPM[ADDR_ROW][ADDR_COL]     (1-bit, 1-cycle)
  if BPM_FLAG = 1:
    INTERP_VALUE ← compute from line buffer neighbors
    OUTPUT ← INTERP_VALUE
  else:
    OUTPUT ← RESULT

Output: CORR_PIXEL[15:0]
Total latency: 4–6 clock cycles (pipeline depth)
Throughput: 1 pixel per clock cycle (fully pipelined)
```

### 10.3 Line Buffer for Bad Pixel Replacement

```
LINE BUFFER ARCHITECTURE
=========================
For 3×3 neighborhood bad pixel replacement:
  - Need: 1 complete row above and below current pixel
  - Buffer: 2 line buffers × 3072 pixels × 16 bits = 12,288 bytes

For 5×5 neighborhood (higher quality):
  - Buffer: 4 line buffers × 3072 × 16 = 24,576 bytes

BRAM allocation (Xilinx/Intel 18Kb BRAMs):
  - 5×5 neighborhood: 2 BRAMs of 18Kb each (24,576 bits < 36Kb total)
  
  Total BRAM for full pipeline:
    Dark map:      18.9 MB / 18Kb per BRAM = 8,533 BRAMs ← Too large!
    
Solution: Off-chip SRAM for dark/gain maps, 
          on-chip BRAM only for line buffers and pipeline registers
```

### 10.4 Memory Access Pattern and Bandwidth

```
MEMORY ACCESS OPTIMIZATION
===========================
Challenge: 3072 columns × 16-bit = 48,576 bits per row
           Need access to current pixel's dark + gain simultaneously

Solution: Dual-port SRAM with split addressing
  - Port A (read): current pixel address → dark map value
  - Port B (read): current pixel address → gain map value
  (Simultaneous reads each clock cycle)

Alternative: Interleaved storage
  Store [DARK(i,j), GAIN(i,j)] as 32-bit word at address (i×3072+j)
  Single 32-bit read → both values in 1 clock cycle
  Memory layout: even bits = dark, odd bits = gain

Memory bandwidth requirement (30fps, simultaneous read):
  Dark map reads:  9.4 Mpix/s × 2 bytes = 18.9 MB/s
  Gain map reads:  9.4 Mpix/s × 2 bytes = 18.9 MB/s
  Total: 37.8 MB/s → well within DDR3/4 bandwidth (10+ GB/s)
```

### 10.5 Fixed-Point Arithmetic Design

**Recommended number format for gain correction:**

```
FIXED-POINT FORMAT SELECTION
==============================
Gain values range: typically 0.5 to 2.0 (±50% variation)
Required precision: < 0.1% (1 part in 1000)

Format: Q1.15 (1 integer bit + 15 fractional bits)
  Range: 0 to 1.99997
  Resolution: 1/32768 ≈ 0.003%
  Storage: 16 bits

For extended range (detector with >2× variation):
  Format: Q2.14
  Range: 0 to 3.99994  
  Resolution: 1/16384 ≈ 0.006%

Multiplication implementation:
  16-bit × 16-bit → 32-bit product (DSP48 block)
  Right-shift result by 15 (Q1.15 normalization)
  Clamp to [0, 65535] for 16-bit output

Error analysis:
  Gain precision: 0.003% → negligible
  Quantization noise: < 0.003% of signal → negligible
  Round-trip accuracy: < 0.01%
```

Per [Lightweight FPGA Infrared Image Processor (Sensors 2024)](https://pmc.ncbi.nlm.nih.gov/articles/PMC10893426/):
- Recommended format: **1-bit sign + 15-bit integer + 16-bit fraction (32-bit)**
- Ensures real-time processing while preserving precision

### 10.6 Complete FPGA Resource Estimate for 3072×3072

Based on scaled results from literature implementations:

| Resource | Correction Pipeline Only | With Bad Pixel Detection |
|---|---|---|
| LUTs | ~8,000–15,000 | ~15,000–25,000 |
| Flip-Flops | ~6,000–10,000 | ~10,000–15,000 |
| BRAM (18Kb) | 4–8 (line buffers only) | 12–20 |
| DSP48 blocks | 6–12 (multipliers) | 12–18 |
| External SRAM | 256 MB DDR3/4 | 256 MB DDR3/4 |
| Max frequency | 200–400 MHz | 150–300 MHz |

**Reference implementation** ([Real Time NUC on XC5VLX110](https://pdfs.semanticscholar.org/fd8c/90995c06103ff7a863f58a4266a40dcca319.pdf)):
- Resources: 18% LUT, 7% FF, 11% BRAM, 18% DSP48
- Speed: 80 MHz system clock
- Noise reduction: RFPN from 11.7% → < 1%

### 10.7 FPGA Pipeline Flowchart

```
╔════════════════════════════════════════════════════════════════╗
║          COMPLETE FPGA CORRECTION PIPELINE                      ║
╠════════════════════════════════════════════════════════════════╣
║                                                                  ║
║  ┌─────────┐   ┌───────────┐   ┌─────────────┐                 ║
║  │ADC      │   │Dark Map   │   │Gain Map     │                  ║
║  │14/16-bit│   │(SRAM/DDR) │   │(SRAM/DDR)   │                 ║
║  └────┬────┘   └─────┬─────┘   └──────┬──────┘                 ║
║       │              │                │                         ║
║       ▼              ▼                │                         ║
║  ┌─────────────────────────┐          │                         ║
║  │  STAGE 1: Subtraction   │          │                         ║
║  │  TEMP = RAW - DARK      │          │                         ║
║  │  (clamp to 0 if negative)│         │                         ║
║  └─────────┬───────────────┘          │                         ║
║            │                          ▼                         ║
║            └────────────►┌───────────────────────┐             ║
║                          │  STAGE 2: Multiply     │             ║
║                          │  RESULT = TEMP × GAIN  │             ║
║                          │  (DSP48, Q16 format)   │             ║
║                          └─────────┬─────────────┘             ║
║                                    │                            ║
║  ┌──────────────┐                  │                            ║
║  │Bad Pixel Map │                  ▼                            ║
║  │(1-bit BRAM)  │   ┌─────────────────────────┐               ║
║  └──────┬───────┘   │  STAGE 3: BPC           │               ║
║         └──────────►│  if bad: interpolate    │               ║
║  ┌──────────────┐   │  from line buffer       │               ║
║  │Line Buffers  │   │  neighbors              │               ║
║  │(2 rows BRAM) ├───►                          │               ║
║  └──────────────┘   └─────────┬───────────────┘               ║
║                               │                                ║
║                               ▼                                ║
║                    ┌──────────────────────┐                    ║
║                    │  OUTPUT CORRECTED    │                    ║
║                    │  IMAGE (16-bit)      │                    ║
║                    └──────────────────────┘                    ║
╚════════════════════════════════════════════════════════════════╝
```

---

## 11. IEC 62220-1 Standard: DQE, MTF, NPS

### 11.1 Standard Overview

**IEC 62220-1:2003** — *Characteristics of Digital X-ray Imaging Devices — Part 1: Determination of Detective Quantum Efficiency*

**IEC 62220-1-1:2015** — Updated for radiographic imaging detectors  
**IEC 62220-1-3:2008** — For dynamic imaging (fluoroscopy)

Scope: 2D detectors for general radiography (flat panel, storage phosphor, image intensifiers, CCD/CMOS arrays). Excludes: scanning systems, dental, mammography, CT.

### 11.2 DQE Definition and Formula

```
DETECTIVE QUANTUM EFFICIENCY
==============================
                [MTF(f)]²
DQE(f) = ────────────────────
           Φ × NPS(f)

where:
  f    = spatial frequency (mm⁻¹ or cycles/mm)
  MTF  = Modulation Transfer Function (dimensionless, 0–1)
  Φ    = X-ray quanta per area at detector input (photons/mm²)
  NPS  = Noise Power Spectrum (mm²)

Equivalently:
              MTF²(f)
DQE(f) = ─────────────────
           NNPS(f) × Φ

where NNPS(f) = NPS(f) / S² is the normalized NPS
(S = mean large-area signal, S² normalizes for signal level)
```

**Physical interpretation:** DQE(f) = ratio of output SNR² to input SNR² as function of spatial frequency. An ideal photon-counting detector has DQE = 1 at all frequencies.

**Typical DQE values:**

| Detector Type | DQE at 0 | DQE at 1 lp/mm | DQE at 2 lp/mm |
|---|---|---|---|
| a-Si CsI:Tl (indirect) | 0.65–0.75 | 0.50–0.65 | 0.30–0.45 |
| a-Se (direct) | 0.60–0.70 | 0.55–0.65 | 0.40–0.55 |
| CR (phosphor) | 0.20–0.35 | 0.15–0.25 | 0.08–0.15 |

Source: [DQE Methodology Step by Step — IEC 62220-1 (AAPM, Granfors)](https://www.aapm.org/meetings/03am/pdf/9811-91358.pdf)

### 11.3 Standard Radiation Spectra (RQA)

| Spectrum | Added Filtration (mm Al) | HVL (mm Al) | Photons/(mm²·nGy) |
|---|---|---|---|
| RQA 3 | 10 | 4.0 | 21.76 |
| RQA 5 | 21 | 7.1 | 30.17 |
| RQA 7 | 30 | 9.1 | 32.36 |
| RQA 9 | 40 | 11.5 | 31.08 |

**Preferred single spectrum: RQA 5** (most commonly used for general radiography DQE benchmarking)

**Setup requirements:**
- SID ≥ 1.5 m
- X-ray field at detector: 160 mm × 160 mm
- Filtration placed close to X-ray source
- Monitor detector calibration: precision < 2%
- Radiation meter uncertainty: < 5% (coverage factor k=2)

### 11.4 MTF Measurement Procedure

```
MTF MEASUREMENT (IEC 62220-1 Edge Method)
==========================================
Test object: Tungsten plate with precision-machined edge
  - 1 mm thick tungsten + 3 mm lead (opaque to X-rays)
  - Tilt: 1.5°–3° from detector axis (to achieve sub-pixel sampling)

Procedure:
  1. Place test object on detector surface; center on X-ray beam axis
  2. Acquire image with RQA-5 spectrum
  3. Linearize image using inverse of conversion function
  4. Define analysis ROI: ±50 mm from edge transition
  5. Compute Edge Spread Function (ESF) within ROI
  6. Differentiate ESF numerically → Line Spread Function (LSF)
  7. MTF(f) = |FT{LSF(x)}|
  8. Average MTF over f ± 0.01/pitch frequency band
  
Key accuracy requirements:
  - Only ESF averaging allowed (per 2015 revision)
  - Diagonal (45°) MTF optionally measured
  - MTF method affects DQE estimate by ~11% (major source of variation)
```

### 11.5 NPS Measurement Procedure

```
NPS MEASUREMENT (IEC 62220-1)
==============================
Acquisition:
  - Flat field images (no test object), RQA-5 spectrum
  - Minimum: 4 million independent image pixels
  - Example: 10 images × 640×640 pixels = 4.1 Mpix (sufficient)
  - All images at same exposure level

Image processing:
  1. Linearize to units of quanta/area (via conversion function)
  2. Select central 125 mm × 125 mm ROI
  3. Optional: subtract 2D second-order polynomial (detrending)
  4. Divide into overlapping 256×256 sub-ROIs (50% overlap)
  5. Compute 2D Fourier transform of each sub-ROI:
  
          Δx × Δy     M
  NPS = ─────────── × Σ |FT{ I(x,y) - S(x,y) }|²
           N²×M      m=1
  
  where: Δx,Δy = pixel pitch; N = ROI size; M = number of ROIs;
         S(x,y) = optional 2D polynomial background

  6. Average 2D NPS over ±7 frequency bins excluding axis
  7. Report 1D NPS along principal axes

Corrections required before DQE measurement:
  - Offset correction (dark frame subtraction)
  - Gain correction (flat field division)
  - Bad pixel correction
  - Lag/ghosting effects must be assessed
```

Source: [IEC 62220-1 Standard (University of Michigan copy)](https://websites.umich.edu/~ners580/ners-bioe_481/lectures/pdfs/2003-10-IEC_62220-DQE.pdf); [Assessment of DQE: Intercomparison (Radiology, PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC2464291/)

### 11.6 Conversion Function Determination

```
CONVERSION FUNCTION (IEC 62220-1)
===================================
Definition: Plot of large-area output level vs. X-ray quanta/area (Q)

Measurement:
  1. Irradiate at ≥5 exposure levels from 0 to 4× normal level
  2. Measure air kerma at detector surface for each level
  3. Convert air kerma → Q using IEC table (e.g., 30.17 photons/(mm²·nGy) for RQA-5)
  4. Record mean pixel value for each Q
  5. Fit function: Output = f(Q)

Use:
  - Linearize images before MTF and NPS analysis
  - Must be linear (or have measurable nonlinearity documented)
  - Log-scale spacing required if full range needed: ΔlogQ ≤ 0.1
```

### 11.7 Impact of Calibration on DQE

Critical: DQE measurement requires proper prior calibration. Measurement errors from:
- Incorrect dark subtraction → overestimates NPS → underestimates DQE
- Imperfect gain correction → adds fixed-pattern noise → underestimates DQE
- Uncorrected bad pixels → artifacts in NPS → unreliable DQE

**Effect of flat-field correction** on DQE measurements: gain/offset calibration reduces fixed-pattern noise contribution to NNPS by 50–60%, significantly improving measured DQE values. ([Willis et al., 2011](https://pmc.ncbi.nlm.nih.gov/articles/PMC3965338/))

---

## 12. AAPM TG-150 Quality Control Recommendations

### 12.1 Recommended Tests (AAPM TG-150, 2024)

**Publication:** [AAPM Report No. 150, July 2024](https://www.aapm.org/pubs/reports/detail.asp?docid=283)

| Test | Frequency | Trigger |
|---|---|---|
| Visual inspection | At each use | Routine |
| Flat field (dark frame acquisition) | Annual (baseline at acceptance) | After repair |
| Signal nonuniformity | Annual | After calibration failure |
| Noise nonuniformity | Annual | More sensitive to calibration issues |
| SNR nonuniformity | Annual | |
| Minimum SNR | Annual | |
| Anomalous pixel count | Annual | |
| MTF (spatial resolution) | Annual | |
| Detector response (linearity) | Annual | |
| Gain/offset recalibration | Per manufacturer (often annual) | After test failure |

### 12.2 Anomalous Pixel Criterion (TG-150)

From [AAPM TG-150 evaluation study (JACMP, 2016)](https://pmc.ncbi.nlm.nih.gov/articles/PMC5874089/):

```
ANOMALOUS PIXEL DEFINITION (TG-150)
=====================================
A pixel is anomalous if it satisfies in ALL four flat-field images:

  p(i,j) - μ_i ≥ 3 × σ_i

where:
  p(i,j) = pixel value in i-th ROI
  μ_i    = mean of i-th ROI (10×10 mm squares typical)
  σ_i    = standard deviation of i-th ROI

Note: This detects pixels not identified or adequately corrected
by the manufacturer's gain/offset calibration and dead pixel maps.
These are "residual" bad pixels after correction.

Typical acceptable threshold: < 0.1% of total pixels anomalous
```

### 12.3 Calibration Conditions for GE and Carestream Detectors

**Carestream DRX-1C (CsI):** 80 kVp, 4 mAs stations (0.71, 2.8, 9.0, 18.0 mAs), 182 cm SID, no backscatter

**GE FlashPad:** 80 kVp, 20 mm Al filtration, grid removed, SID varies by position (180 cm wall, 100 cm table)

---

## 13. Performance Benchmarks from Literature

### 13.1 Correction Effectiveness

| Study/Implementation | Initial RFPN | After Correction | Method |
|---|---|---|---|
| Real-time NUC on XC5VLX110 ([Semanticscholar](https://pdfs.semanticscholar.org/fd8c/90995c06103ff7a863f58a4266a40dcca319.pdf)) | 11.7% | < 1% | Two-point, 64-frame avg |
| IRFPA Modified TPC ([SpringerPlus, 2016](https://d-nb.info/1119551773/34)) | 0.34% | 0.12% | Modified two-point + shutter |
| Duo-SID heel effect correction ([Wang, 2013](https://pubmed.ncbi.nlm.nih.gov/23927327/)) | residual 100% | −70 to −80% | Iterative separation |
| BHR vs FFF ([ICIP, 2012](https://www.math.union.edu/~wangj/papers/Wang12.Flat%20Field%20Correction%20%5BICIP%5D.pdf)) | NVF=4.18 | NVF=2.17 | BHR multi-point vs FFF |
| GE flat panel CsI ([Willis et al. 2011](https://pmc.ncbi.nlm.nih.gov/articles/PMC3965338/)) | SNR variation large | −18% SD | Gain/offset calibration |

### 13.2 FPGA Implementations

| Implementation | FPGA | Clock | Frame Rate | Resolution | Resources |
|---|---|---|---|---|---|
| NUC + bad pixel ([Sensors 2024](https://pmc.ncbi.nlm.nih.gov/articles/PMC10893426/)) | XC7A100T | 50 MHz | 30 fps | 640×480 | 10,894 LUT, 9,367 FF, 4 BRAM, 5 DSP |
| Bad pixel detection ([MIPRO 2012](https://mipro-proceedings.com/sites/mipro-proceedings.com/files/upload/sp/sp_009.pdf)) | Lattice ECP2 | 108 MHz | Video | Bayer | 1,332 LUT4, 1,018 FF, 8 EBR |
| IRFPA NUC ([Semanticscholar 2019](https://pdfs.semanticscholar.org/fd8c/90995c06103ff7a863f58a4266a40dcca319.pdf)) | XC5VLX110 | 80 MHz | RT | 640×512 14-bit | 10% LUT, 7% FF, 11% BRAM, 18% DSP |
| Two-point NUC ([Njuguna]) | FPGA | 300 MHz | High | Full | 4,293 LUT, 4,261 FF, 11 DSP, 5 BRAM |

### 13.3 Dark Current Specifications

| Detector Type | Dark Current Density | Conditions |
|---|---|---|
| a-Si:H PIN photodiode | < 1 nA/cm² | −4V bias, 25°C |
| a-Si:H switching diode | 10 nA/cm² | −4V bias |
| a-Si:H FPA (typical) | 10¹ pA/cm² (peak distribution) | 40°C, −2.5V |
| CMOS image sensor | 15–248 electrons/s/pixel | 0–50°C range |

Source: [Tredwell 2009](https://www.imagesensors.org/Past%20Workshops/2009%20Workshop/2009%20Papers/065_paper_tredwell_invited.pdf); [Hoheisel 1996](https://www.mhoheisel.de/docs/ISCMP91996112.pdf); [Sensors 2023 CMOS](https://pmc.ncbi.nlm.nih.gov/articles/PMC10674984/)

### 13.4 Number of Frames Required for Calibration Maps

| Map Type | Minimum N | Recommended N | Noise improvement |
|---|---|---|---|
| Dark (offset) map | 16 | 64 | 4× → 8× noise reduction |
| Gain (flat field) map | 32 | 128 | 5.7× → 11× noise reduction |
| Thermal dark coefficients (per temperature) | 64 | 128 | 8× → 11× |
| Update (rolling weighted) | 1 | 1 (weighted) | Gradual drift correction |

For 64-frame average: noise reduced to 12.5% of single-frame noise. This is the practical standard balance between calibration time and correction quality.

---

## 14. Summary: Design Recommendations for 3072×3072 Detector

### 14.1 Calibration System Architecture

```
RECOMMENDED CALIBRATION SYSTEM FOR 3072×3072 FPD
==================================================

1. DARK MAP CALIBRATION
   ├── Acquisition: 64 frames per temperature point
   ├── Temperature points: 5 (15, 20, 25, 30, 35°C)
   ├── Per-pixel thermal model: linear (a + b×T)
   ├── Update trigger: ΔT > 2°C from last calibration
   ├── Rolling update: α = 0.1 weight for single new dark frame
   └── Storage: 64 MB Flash (5 dark maps × 19 MB each)

2. GAIN MAP CALIBRATION
   ├── Acquisition: 128 frames per kVp setting
   ├── kVp settings: 2–4 (e.g., 70, 80, 100, 120 kVp)
   ├── Standard: RQA-5 (74 kVp, 21 mm Al) for DQE compliance
   ├── Frequency: Annual or after detector component replacement
   └── Storage: 76 MB Flash (4 gain maps × 19 MB each)

3. BAD PIXEL MAP
   ├── Factory map: stored in Flash (1.15 MB bitmap)
   ├── Detection: mean ± 3σ on dark and gain maps
   ├── Median-filter subtraction for gain-based detection
   ├── Multi-exposure test for nonlinear pixel detection
   ├── Dynamic detection: FPGA-based adaptive algorithm
   └── Update: Annual or after radiation damage event

4. CORRECTION PIPELINE (FPGA)
   ├── Architecture: 3-stage pipeline (dark sub → gain mul → BPC)
   ├── Data format: 16-bit signed (dark sub), Q1.15 gain (mul)
   ├── Throughput: 1 pixel/clock, handles 30 fps at 300 MHz
   ├── Line buffers: 2 rows × 3072 px × 16-bit = 2 BRAMs
   ├── Dark/gain maps: External DDR3 (256 MB, dual-port)
   ├── Bad pixel map: On-chip BRAM (512 KB)
   └── Total FPGA resources: ~25,000 LUTs, 20 BRAMs, 18 DSPs

5. TEMPERATURE COMPENSATION
   ├── Sensors: 4×4 grid (16 sensors across array)
   ├── Compensation: per-pixel bilinear interpolation of T map
   ├── Dark map selection: nearest-T or linear interpolation
   └── Real-time model: D(i,j,T) = a(i,j) + b(i,j)×T
```

### 14.2 Memory Summary

| Component | Memory Type | Size |
|---|---|---|
| 5× Dark maps (16-bit) | SPI NOR Flash | 94 MB |
| 4× Gain maps (16-bit) | SPI NOR Flash | 76 MB |
| Thermal coefficients (a, b, 32-bit) | SPI NOR Flash | 75 MB |
| Bad pixel bitmap | SPI NOR Flash | 2 MB |
| **Flash total** | **NOR Flash** | **~247 MB → use 256 MB** |
| Frame ping-pong buffers (3×) | LPDDR4 | 57 MB |
| Active dark map (1×) | LPDDR4 | 19 MB |
| Active gain map (1×) | LPDDR4 | 19 MB |
| Working buffers | LPDDR4 | 10 MB |
| **DRAM total** | **LPDDR4** | **~105 MB → use 256 MB** |

### 14.3 Correction Equations Reference Card

```
CORRECTION EQUATIONS SUMMARY
==============================

1. Dark Subtraction:
   D_corr(i,j) = Raw(i,j) - Dark_map(i,j)
   [Dark_map acquired at matching temperature and integration time]

2. Gain Correction:
   G_corr(i,j) = D_corr(i,j) × Gain_map(i,j)
   where Gain_map(i,j) = M / (Bright_avg(i,j) - Dark_map(i,j))

3. Two-Point Combined:
   Corr(i,j) = [Raw(i,j) - Dark(i,j)] / [Bright(i,j) - Dark(i,j)] × M

4. Multi-Point (LUT):
   Corr(i,j) = LUT_inverse(i,j, Raw(i,j) - Dark(i,j))

5. Temperature-Compensated Dark:
   Dark(i,j,T) = a(i,j) + b(i,j) × T

6. Dynamic Dark Update (rolling):
   Dark_new = α × D_single_frame + (1-α) × Dark_old   [α = 0.1]

7. Bad Pixel Replacement (bilinear):
   Corr(i,j) = [P(i-1,j)+P(i+1,j)+P(i,j-1)+P(i,j+1)] / 4

8. DQE:
   DQE(f) = MTF²(f) / [Φ × NPS(f)]

9. Flat-field correction (X-ray convention):
   N = (P - D) / (F - D)
   [P=projection, F=flat field, D=dark field]
```

---

## References

1. [Dark correction for digital X-ray detector — Patent EP2148500A1 (Granfors et al., 2009)](https://patents.google.com/patent/EP2148500A1/en)
2. [Automatic identification and correction of bad pixels — Patent US5657400A (Antonuk et al., 1997)](https://patents.google.com/patent/US5657400A/en)
3. [Flat-field correction — Wikipedia](https://en.wikipedia.org/wiki/Flat-field_correction)
4. [Flat-Panel Imaging Arrays for Digital Radiography (Tredwell, IISW 2009)](https://www.imagesensors.org/Past%20Workshops/2009%20Workshop/2009%20Papers/065_paper_tredwell_invited.pdf)
5. [Amorphous Silicon X-Ray Detectors (Hoheisel, ISCMP 1996)](https://www.mhoheisel.de/docs/ISCMP91996112.pdf)
6. [Heel effect adaptive flat field correction of digital X-ray detectors (Wang et al., Med Phys 2013)](https://pubmed.ncbi.nlm.nih.gov/23927327/)
7. [Beam hardening-respecting flat field correction (Wang & Yu, ICIP 2012)](https://www.math.union.edu/~wangj/papers/Wang12.Flat%20Field%20Correction%20%5BICIP%5D.pdf)
8. [Lightweight and Real-Time Infrared Image Processor Based on FPGA (Sensors 2024)](https://pmc.ncbi.nlm.nih.gov/articles/PMC10893426/)
9. [Implementation of Algorithm for Detection and Correction of Defective Pixels in FPGA (MIPRO 2012)](https://mipro-proceedings.com/sites/mipro-proceedings.com/files/upload/sp/sp_009.pdf)
10. [Real Time Non-uniformity Correction Algorithm and FPGA Implementation (Semanticscholar)](https://pdfs.semanticscholar.org/fd8c/90995c06103ff7a863f58a4266a40dcca319.pdf)
11. [DQE Methodology Step by Step — IEC 62220-1 (Granfors, AAPM 2003)](https://www.aapm.org/meetings/03am/pdf/9811-91358.pdf)
12. [IEC 62220-1:2003 Standard (University of Michigan)](https://websites.umich.edu/~ners580/ners-bioe_481/lectures/pdfs/2003-10-IEC_62220-DQE.pdf)
13. [IEC 62220-1-1:2015 — Detectors used in radiographic imaging (IEC Webstore)](https://webstore.iec.ch/en/publication/21937)
14. [Assessment of DQE: Intercomparison of a standardized method (Radiology, PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC2464291/)
15. [AAPM TG-150: Acceptance Testing and QC of Digital Radiographic Imaging Systems (2024)](https://www.aapm.org/pubs/reports/detail.asp?docid=283)
16. [Evaluation of cassette-based digital radiography detectors using AAPM TG-150 (JACMP 2016)](https://pmc.ncbi.nlm.nih.gov/articles/PMC5874089/)
17. [Gain and offset calibration reduces variation in SNR among FPD systems (Willis et al., Med Phys 2011)](https://pmc.ncbi.nlm.nih.gov/articles/PMC3965338/)
18. [Using deep learning for pixel-defect corrections in flat-panel X-ray (JMI 2021)](https://pmc.ncbi.nlm.nih.gov/articles/PMC7930811/)
19. [In-pixel temperature sensors for dark current compensation (TU Delft Thesis 2021)](https://research.tudelft.nl/files/96743059/Thesis_AAbarca_v9.pdf)
20. [CMOS Image Sensor Dark Current Compensation Using In-Pixel Temperature Sensors (Sensors 2023)](https://pmc.ncbi.nlm.nih.gov/articles/PMC10674984/)
21. [Temperature dependence of dark current in a CCD (Woltjer et al., SPIE 2002)](https://web.pdx.edu/~d4eb/ccd/SPIE_2002.pdf)
22. [Silicon Strip Detectors — dark current doubles every ~8°C (DiVA Portal)](http://www.diva-portal.org/smash/get/diva2:9333/FULLTEXT01.pdf)
23. [Synthetic Correction of Dark Signal Data (AMOS 2022, Chrien)](https://amostech.com/TechnicalPapers/2022/Poster/Chrien.pdf)
24. [Nonuniformity correction algorithm with efficient pixel offset estimation (SpringerPlus 2016)](https://d-nb.info/1119551773/34)
25. [A forward bias method for lag correction of a-Si flat panel detector (Med Phys 2011, PMC)](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/)
