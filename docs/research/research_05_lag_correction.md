# Image Lag and Ghosting Correction Algorithms for a-Si TFT X-ray Flat Panel Detectors

**Compiled:** 2026-03-18  
**Classification:** Technical Research Report  
**Scope:** Physics mechanisms, measurement methods, hardware reduction techniques, software correction algorithms, FPGA implementation

---

## Table of Contents

1. [Lag Mechanism Physics](#1-lag-mechanism-physics)
2. [Lag Measurement Methods](#2-lag-measurement-methods)
3. [Hardware-Based Lag Reduction](#3-hardware-based-lag-reduction)
4. [Linear Time-Invariant (LTI) Recursive Lag Correction](#4-linear-time-invariant-lti-recursive-lag-correction)
5. [Multi-Exponential Lag Model and Time Constants](#5-multi-exponential-lag-model-and-time-constants)
6. [Nonlinear Lag Correction (NLCSC Algorithm)](#6-nonlinear-lag-correction-nlcsc-algorithm)
7. [Autoregressive Model Approach (AR(1))](#7-autoregressive-model-approach-ar1)
8. [Ghosting in Fluoroscopy](#8-ghosting-in-fluoroscopy)
9. [FPGA-Implementable Lag Correction](#9-fpga-implementable-lag-correction)
10. [Offset Correction for Lag](#10-offset-correction-for-lag)
11. [Scan-Dependent Lag](#11-scan-dependent-lag)
12. [Real-Time Systems and Pipeline Implementation](#12-real-time-systems-and-pipeline-implementation)
13. [Deep Learning Approaches (2025)](#13-deep-learning-approaches-2025)
14. [Comparison: a-Si vs. a-Se Detectors](#14-comparison-a-si-vs-a-se-detectors)
15. [Summary Tables](#15-summary-tables)
16. [References](#16-references)

---

## 1. Lag Mechanism Physics

### 1.1 Definition

**Detector lag** (also called *image lag* or *residual signal*) is defined as signal present in detector frames *following* the frame in which it was generated. It arises from charge that is deposited into trap states during X-ray illumination and then released over subsequent frames.

In fluoroscopy, lag causes temporal blurring and reduces temporal resolution. In cone-beam CT (CBCT), differing trap histories across pixels cause shading artifacts known as the **"radar artifact"**, measured at 20–35 HU for pelvic phantoms and up to 51 HU maximum.

### 1.2 a-Si Photodiode Charge Trap Physics

The primary source of lag in indirect-conversion a-Si flat-panel detectors is **charge trapping in the a-Si photodiode layer**, not the TFT switch or the scintillator (though the CsI scintillator contributes ~0.7% exponential rise via afterglow).

**Why a-Si traps exist:**
- Amorphous silicon lacks long-range crystalline order → dangling bonds → localized defect states in the bandgap
- Band gap energy: ~1.7 eV (vs. 1.1 eV for crystalline Si)
- Exponential band-tail spreading creates a large population of trap states
- Trap density: **10^14 – 10^19 cm⁻³·eV⁻¹** for energies 0.1–0.8 eV below the conduction band edge
- Total integrated trap density: ~**10^18 states/cm³**

**Trap dynamics (Wieczorek model):**

During X-ray illumination, free carriers fill trap states; after illumination ends, trapped charge releases at rates set by the trap energy level:

```
Trap filling rate:
  R_β(E_tr) = ν₀ × exp[-(E_c - E_Fn)/kT] × N_t(E_tr) × [1 - f(E_tr)]

Trap emptying rate:
  R_α(E_tr) = ν₀ × exp[-(E_c - E_tr)/kT] × N_t(E_tr) × f(E_tr)

Total trap rate:
  a(E_tr) = ν₀ × exp[-(E_c - E_tr)/kT] + ν₀ × exp[-(E_c - E_Fn)/kT]
           = a_thermal + a_exposure(n_e)
```

Where:
- `E_tr` = trap energy level in bandgap
- `E_c` = conduction band edge energy
- `E_Fn` = quasi-Fermi level (illumination-dependent)
- `N_t(E_tr)` = trap density at energy E_tr
- `ν₀` = attempt-to-escape frequency
- `f(E_tr)` = trap occupation function
- `k` = Boltzmann constant, `T` = temperature

```
a_thermal = ν₀ × exp[-(E_c - E_tr)/kT]    (exposure-independent)

a_exposure(n_e) = ν₀ × n_e / N_c           (proportional to free carriers)
```

Where `n_e` = free carrier density, `N_c` = effective density of states at conduction band edge.

**Key physical consequences:**
1. **Non-linearity**: The quasi-Fermi level shift with illumination makes trap dynamics exposure-dependent → system is **not** linear time-invariant (LTI)
2. **Time variance**: `a(E_tr)` changes with current exposure → stored charge cannot be modeled with fixed time constants
3. **Extremely wide time constant range**: Traps from milliseconds to days (deep traps)
4. **Spatial non-uniformity**: Gain calibration and beam profile create variable lag across detector pixels

### 1.3 TFT Off-State Leakage Contribution

While the dominant lag source is the a-Si photodiode, TFT off-state leakage also contributes:
- TFT off-state current through the gate insulator and semiconductor interface
- **Trap-assisted tunneling (TAT)**: Phonon-assisted tunneling through trap states in the gate insulator at the drain/LDD junction
- Leakage manifests as incomplete charge reset between frames
- TFT gate voltage modulates this leakage; more negative gate bias → lower leakage but longer gate pulse required
- Temperature dependence: activation energy `E_a` decreases as gate and drain voltages increase; TAT current suppressed at lower temperatures

### 1.4 Scintillator Afterglow (CsI)

- CsI:Tl scintillator contributes a residual ~0.7% exponential rise in the Rising Step-Response Function (RSRF) over 600 frames
- Hardware lag correction methods (forward bias) do not address scintillator afterglow
- Indirect-conversion detector lag is dominated by a-Si photodiode traps, not scintillator

### 1.5 Two-Component Temporal Response

A simplified two-time-constant picture:
- **Fast component** (τ₁ ~ms to ~100ms): Shallow traps near band edges; responsible for first-frame lag
- **Slow component** (τ₂ ~seconds to minutes): Deep traps; responsible for the radar artifact in CBCT and ghosting persisting for 100+ frames

In practice, four exponential components (N=4) are needed to accurately model the full decay over hundreds of frames.

---

## 2. Lag Measurement Methods

### 2.1 Standard Definitions

**Lag fraction at frame n (IEC 62220-1-1:2015 / IEC 62220-1-3:2008 standard):**

```
L_n = (S_n - B) / (S_0 - B)
```

Where:
- `S_n` = mean pixel signal in the n-th frame after X-ray termination
- `S_0` = mean pixel signal during X-ray exposure (0th frame)
- `B` = dark current (background signal without X-ray)

**Typical values:**
- 1st frame lag: **2–5%** (uncorrected, Varian 4030CB at various exposures)
- 50th frame lag: **0.4–1.0%** (uncorrected)
- After 100+ frames: still detectable, >0.1%

### 2.2 Step-Response Measurements

Two complementary step-response protocols:

**Falling Step-Response Function (FSRF):** X-ray ON for sufficient frames → X-ray OFF → measure residual signal decay. Directly measures lag.

**Rising Step-Response Function (RSRF):** X-ray suddenly turned ON → measures exponential rise to steady state. The 4% exponential rise observed on Varian 4030CB represents gain increase as traps fill.

Both are used to calibrate correction algorithms; FSRF at low exposure (1.6–3.4% saturation) gives best correction for large-object CBCT.

### 2.3 Lag Correction Factor (LCF) — IEC 62220-1-3

The **Lag Correction Factor** is used to correct the measured Noise Power Spectrum (NPS):

```
NPS_corrected = NPS_measured × LCF
```

**Measurement methods for LCF:**
1. **IEC 62220-1-3 standard method**: Temporal power spectral density (PSD) under constant-potential X-ray generator; sensitive to noise at low doses
2. **Granfors-Aufrichtig (GA) method**: Uses synchronized pulsed X-ray source; asymptotically unbiased estimate; requires adaptation for gate-line scanning nonuniformity
3. **Temporal correlation method** (Kim & Lee, 2020): Uses correlation coefficients from steady-state images; more robust to noise; generalization of the regression model

**IEC 62220-1-3 requires:** 8–32 images for accurate LCF measurement (established experimentally).

### 2.4 Exposure Dependence

Lag is **nonlinear with respect to exposure**:
- Normalized FSRF decreases >2× from lowest (1.6%) to highest (84%) saturation exposure at 50th frame
- This exposure dependence is the primary reason LTI models fail, especially for large-field CBCT

---

## 3. Hardware-Based Lag Reduction

### 3.1 Empty Frame Flushing ("Flush-N" Method)

**Principle:** Insert N empty frames (X-ray source OFF) between acquired X-ray frames. The lag decays exponentially during these empty frames, which can be discarded.

**Performance (Flush-1):**
- Reduces FSRF by approximately 50% (half the lag signal removed)
- Frame rate is reduced by factor of (N+1) — a major drawback
- Does not address deep traps (charge at deeper energy levels remains)

**Flush-1 impulse response:**
```
h_flush-1(k) = b₀δ(k) + Σ b_m × exp(-2·a_m·k)
```
(time constants effectively doubled)

### 3.2 LED Light Saturation Method

**Principle:** Illuminate the a-Si photodiode with an LED between readout and X-ray exposure. This fills the trap states, converting spatially varying lag into a spatially uniform offset that can be subtracted.

**Results:**
- Reduces residual RSRF to <1%
- First-frame residual lag: >2% for reset light durations up to 250 μs (insufficient for deep trap saturation)
- Does not address scintillator afterglow

### 3.3 Forward Bias (FB) Hardware Method

**Principle:** Between the readout and X-ray exposure stages, briefly operate each photodiode in forward bias mode. A positive voltage is applied across the diode, inducing a larger-than-normal current that fills the charge trap states uniformly across the entire panel.

**Implementation (Varian 4030CB modified):**
- Groups of 8 rows simultaneously forward biased at **4 V, 100 kHz**
- Injected charge: **20 pC/photodiode** (sufficient for >95% first-frame lag reduction)
- Forward bias time per pixel: **40 μs**
- Total FB time overhead: ~30 ms/frame (limited by charge amplifier current capacity)
- Maximum X-ray pulse width (15 fps): 18 ms; (10 fps): 32 ms

**Quantitative performance:**

| Mode | 2nd Frame Lag | 100th Frame Lag | CBCT Pelvic | CBCT Head |
|------|--------------|----------------|-------------|-----------|
| Standard Dual Gain | 355 counts | 44 counts | 35 HU | 20 HU |
| Forward Bias (4 pF) | 42 counts (−88%) | 13 counts (−70%) | 7 HU (−81%) | 12 HU (−48%) |

**Charge injection vs. lag reduction:**

| FB Charge | 1st Frame Lag Reduction |
|-----------|------------------------|
| 5.8 pC/diode | 93% |
| 15.4 pC/diode | 95% |
| 20 pC/diode | ~95% (set point) |

**Limitations:**
- Does not address scintillator afterglow
- Reduces DQE due to higher noise floor and reduced signal
- SNR reduction: ~4–7% depending on mode
- Switching between FB and standard timing causes offset drift for ~38 frames

### 3.4 Gate Reset Optimization

**Multiple Reset Scans:** Running additional gate reset cycles before readout drains more residual charge. Multiple reset scans progressively deplete shallow traps.

**Gate Pulse Width (τ_gate):** Wider gate pulses allow longer TFT conduction time per row:
- Longer gate pulse → more complete charge readout → lower residual lag offset
- Trade-off: Longer gate pulse → longer frame readout time → lower maximum frame rate
- Gate pulse width determines the fraction of TFT off-state leakage removed per frame

**Gate Drive Voltage (V_gate_off):** More negative gate-off voltage reduces TFT off-state leakage:
- Standard range: V_gate_off = −5 V to −10 V
- More negative → lower off-current → less lag from TFT source
- Constrained by panel design and reliability

### 3.5 Hybrid Hardware-Software Method

The optimal approach combines:
1. Forward bias hardware for large lag reduction between frames
2. Software recursive correction to handle residual lag during X-ray exposure
3. Delay frames during mode transition

---

## 4. Linear Time-Invariant (LTI) Recursive Lag Correction

### 4.1 System Model

The detector is modeled as a linear time-invariant system with multiexponential impulse response function (IRF):

```
h(k) = b₀·δ(k) + Σ_{n=1}^{N} b_n · exp(-a_n · k)      [Eq. 1]
```

Where:
- `k` = discrete frame number
- `N` = number of exponential terms (typically N = 2–4)
- `b_n` = lag coefficient for n-th term (dimensionless, units of detector counts per input count)
- `a_n` = lag rate for n-th term (frames⁻¹); time constant τ_n = 1/a_n frames
- `b₀` = signal fraction transmitted without lag (close to 1)
- `δ(k)` = unit impulse function

The measured detector output `y(k)` is the convolution of ideal input `x(k)` with the IRF:

```
y(k) = x(k) * h(k)        [Eq. 2]
```

### 4.2 Recursive Deconvolution Algorithm (Hsieh/LTI)

The recursive algorithm (originally Hsieh, 2000 for CT afterglow; adapted for a-Si FP by Starman et al.) efficiently inverts the lag model frame-by-frame:

**Corrected output (estimated lag-free signal):**
```
x̂(k) = [ y(k) - Σ_{n=1}^{N} b_n · S_{n,k} · exp(-a_n) ] / [ Σ_{n=0}^{N} b_n ]
                                                                              [Eq. 3]
```

**State variable update:**
```
S_{n,k+1} = x̂(k) + S_{n,k} · exp(-a_n)        [Eq. 4]
```

**Initial condition:** `S_{n,1} = 0` for the first frame of each scan.

**Key property:** Only the current frame `y(k)` and state variables `S_{n,k}` are needed — no history buffer required beyond N state arrays of size equal to one frame.

### 4.3 Continuous-Time Hsieh Formulation (Patent US5249123A)

For continuous-domain CT detectors (sampling interval Δt):

```
x_k = [ y(kΔt) - Σ_{n=1}^{N} S_{nk} ] / [ 1 - Σ_{n=1}^{N} β_n ]

S_{nk} = x_{k-1} + exp(-Δt/τ_n) · S_{n(k-1)}

β_n = α_n · (1 - exp(-Δt/τ_n))
```

Where:
- `α_n` = relative strength of n-th component
- `τ_n` = time constant in seconds
- `β_n` = precomputed constant

**CT detector time constants (US5249123A, N=4):**

| Component | Time Constant τ_n |
|-----------|-------------------|
| τ₁ (primary speed) | 1 ms |
| τ₂ | 6 ms |
| τ₃ | 40 ms |
| τ₄ (afterglow) | ~300 ms |

### 4.4 Falling/Rising Step-Response Function (FSRF/RSRF) Relationships

```
FSRF(k) = Σ_{n=1}^{N} b̃_n · exp(-a_n · k) · u(k)      [Eq. 5]

RSRF(k) = (1 - Σ_{n=1}^{N} b̃_n · exp(-a_n · k)) · u(k)  [Eq. 6]

where: b̃_n = b_n / (1 - exp(-a_n))    (normalized coefficient)
```

For a finite-length FSRF with N_f frames:
```
FSRF_{N_f}(k) = Σ_{n=1}^{N} w_n · b̃_n · exp(-a_n · k) · u(k)

w_n = (exp(-a_n) - exp(-a_n · N_f)) · exp(a_n)
```

### 4.5 LTI Performance

| Exposure | 1st Frame Residual Lag (after LTI) | 50th Frame Residual Lag |
|----------|-------------------------------------|-------------------------|
| 2% sat. | 1.4% | 0.48% |
| 27% sat. | 0.25% | 0.0038% |
| 92% sat. | 0.0053% | −0.16% (over-correction) |

LTI performs best when the IRF is calibrated near the actual imaging exposure. Calibrating at the wrong exposure leaves significant residual lag.

---

## 5. Multi-Exponential Lag Model and Time Constants

### 5.1 Standard N=4 Model Parameters

Experimentally measured IRF parameters for Varian 4030CB (FSRF at 27% saturation exposure, 15 fps):

| Parameter | n=1 (slow) | n=2 | n=3 | n=4 (fast) |
|-----------|-----------|-----|-----|-----------|
| a_n (frames⁻¹) | 2.5×10⁻³ | 2.1×10⁻² | 1.6×10⁻¹ | 7.6×10⁻¹ |
| b_n | 7.1×10⁻⁶ | 1.1×10⁻⁴ | 1.7×10⁻³ | 1.8×10⁻² |

**Time constants at 15 fps:**

| Component | a_n (fr⁻¹) | τ_n (frames) | τ_n (seconds) |
|-----------|-----------|-------------|--------------|
| n=1 (slowest) | 2.5×10⁻³ | ~400 | **26.7 s** |
| n=2 | 2.1×10⁻² | ~48 | **3.2 s** |
| n=3 | 1.6×10⁻¹ | ~6 | **0.4 s** |
| n=4 (fastest) | 7.6×10⁻¹ | ~1.3 | **0.09 s (90 ms)** |

### 5.2 Alternative Parameters at Different Exposure (FSRF 3.4%, best pelvic phantom)

| n | a_n (frames⁻¹) | b_n |
|---|----------------|-----|
| 1 | 0.0023 | 1.611×10⁻⁵ |
| 2 | 0.0150 | 1.468×10⁻⁴ |
| 3 | 0.0827 | 9.190×10⁻⁴ |
| 4 | 0.5786 | 1.480×10⁻² |

### 5.3 Two-Exponential Model (MV Indirect-Conversion FPI, Pang et al.)

For megavoltage imaging with indirect-conversion FPI at 3.5 fps:

```
L_n = A · exp(-n/τ₁) + C · exp(-n/τ₂)
```

- Coefficients A, C: weakly frame rate dependent
- Time constants τ₁, τ₂: strongly frame rate dependent (scale with inter-frame interval), dose-independent

### 5.4 Estimated Stored Charge Model

The total charge stored in the detector (sum of all future lag signals):

```
q_k = Σ_{n=1}^{N} q_{n,k}

q_{n,k} = S_{n,k} · b_n · exp(-a_n) / (1 - exp(-a_n))     [Eq. 9]
```

Frame lag estimates:
```
L_1 = y(k+1) = Σ_{n=1}^{N} b_n · exp(-a_n) · S_{n,k+1}   [Eq. 5]
L_2 = y(k+2) = Σ_{n=1}^{N} b_n · exp(-a_n) · S_{n,k+2}   [Eq. 6]
```

---

## 6. Nonlinear Lag Correction (NLCSC Algorithm)

### 6.1 Motivation

The LTI model fails because:
1. Lag rates `a_n` increase with exposure intensity (quasi-Fermi level shift)
2. Lag coefficients `b_n` depend on how many traps are filled (nonlinear with exposure)
3. The detector is neither linear nor time-invariant

At 2% saturation exposure, LTI calibrated at 27% leaves 1.4% 1st-frame residual lag. The NLCSC algorithm addresses this.

### 6.2 Exposure-Dependent Impulse Response

```
h(k, x_k) = b₀(x_k)·δ(k) + Σ_{n=1}^{N} b_n(x_k) · exp(-a_n(x_k) · k)    [Eq. 11]
```

**Exposure-dependent lag rates:**
```
a_n(x) = a_{1,n} + a_{2,n}(x)                              [Eq. 34]

a_{2,n}(x) = c₁ · (1 - exp(-c₂·x))                        [Eq. 36]
```

Where:
- `a_{1,n}` = base lag rate (exposure-independent, from thermodynamic trapping: `a_thermal`)
- `a_{2,n}(x)` = exposure-dependent rate (from `a_exposure ∝ n_e`)
- `c₁`, `c₂` = calibration constants

**Exposure-dependent lag coefficients:**
```
b_n(x) = Q_n(x) / [x · (1 - exp(-a_n(x)))² · exp(-a_n(x))]     [Eq. 30]

Q(x) = Σ_{n=1}^{N} x·b_n·exp(-a_n) / (1 - exp(-a_n))²           [Eq. 20]
```

### 6.3 NLCSC State Variable Equations

**Consistent stored charge constraint** — when IRF changes, stored charge must be conserved:

```
S*_{n,k} = F₁(q_{n,k}, h(k, x_k))                         [Eq. 12]

S*_{n,k} = q_{n,k} · (1 - exp(-a_n(x_k))) / (b_n(x_k) · exp(-a_n(x_k)))
                                                             [Eq. 16]
```

**NLCSC deconvolution:**
```
x_k = y(k) - Σ_{n=1}^{N} b_n(x_k)·S*_{n,k}·exp(-a_n(x_k))
      / Σ_{n=0}^{N} b_n(x_k)                               [Eq. 13]

S_{n,k+1} = x_k + S*_{n,k} · exp(-a_n(x_k))               [Eq. 14]

q_{n,k+1} = F₂(S_{n,k+1}, h(k, x_k))                      [Eq. 15]

q_{n,k+1} = S_{n,k+1} · b_n(x_k) · exp(-a_n(x_k)) / (1 - exp(-a_n(x_k)))
                                                             [Eq. 17]
```

Note: `x_k` appears on both sides of Eq. 13 → iterative evaluation using `h(k, y(k)) ≈ h(k, x_k)` as initial approximation.

### 6.4 NLCSC Calibration (Three Steps)

1. **Base lag rates a_{1,n}**: Fit N=4 FSRF at 27% exposure to find exposure-independent rates
2. **Maximum stored charge Q_n(x)**: Integrate FSRF exponentials at 9 different exposure levels (2%–92%)
3. **Exposure-dependent rates a_{2,n}(x)**: Global search minimizing std deviation of corrected RSRF

Requires 6 calibration constants + 4 exposure-dependent functions.

### 6.5 NLCSC Performance vs. LTI

| Algorithm | 1st Frame Residual (2% exp.) | 50th Frame Residual (2% exp.) | Pelvic CBCT Avg | Head CBCT Avg |
|-----------|------------------------------|-------------------------------|-----------------|---------------|
| Uncorrected | 3.7% | 0.96% | 35 HU | 16 HU |
| LTI (best) | 1.4% | 0.48% | 14–19 HU | 2–11 HU |
| Intensity-weighted | ~0.5% | ~0.2% | 15 HU | 9 HU |
| **NLCSC** | **0.25%** | **−0.0028%** | **11 HU** | **3 HU** |

---

## 7. Autoregressive Model Approach (AR(1))

### 7.1 Model Description

Lee & Kim (2023, IEEE Access) proposed an AR(1) autoregressive model for lag correction with lower computational complexity than N=4 exponential models:

**AR(1) lag model:**
```
y(k) = x(k) + α · y(k-1) + ε(k)
```

Where:
- `y(k)` = measured detector output at frame k
- `x(k)` = ideal lag-free signal
- `α` = single autoregressive coefficient (analogous to single-pole recursive filter)
- `ε(k)` = noise term

**Linear decorrelation (correction):**
```
x̂(k) = y(k) - α · y(k-1)
```

**Nonlinear version** for exposure-dependent lag:
```
x̂(k) = y(k) - α(x̂(k)) · y(k-1)
```
Where `α(x̂)` is a function of estimated exposure intensity.

### 7.2 Lag Correction Factor

The **Lag Correction Factor (LCF)** measures residual lag:
```
LCF = 1 - α²
```
For ideal correction: LCF → 1. The AR(1) model requires only the previous frame, minimizing memory and compute.

### 7.3 AR(1) vs. Multi-Exponential Comparison

| Feature | AR(1) | N=4 Exponential |
|---------|-------|-----------------|
| Memory | 1 frame | 4 state arrays + 1 frame |
| Operations/pixel | 2 mult + 1 add | 2N mult + N add per component |
| Calibration | 1–2 parameters | 2N parameters per exposure level |
| Accuracy | Moderate | High |
| Nonlinear extension | Simple | Complex (NLCSC) |

---

## 8. Ghosting in Fluoroscopy

### 8.1 Types of Ghost Images

**Ghost** = spatially varying sensitivity (gain) change due to irradiation history, visible when exposed area is followed by different exposure. Different from lag (which is an additive offset).

| Ghost Type | Mechanism | Clinical Scenario |
|-----------|-----------|------------------|
| **Lag ghost** (additive) | Residual charge released from traps adds signal to subsequent frames | High-dose radiograph followed by fluoroscopy; step-wedge visible as ghost overlay |
| **Structural ghost** (multiplicative) | Prolonged exposure changes local sensitivity; structure imprinted in gain map | Lead marker, beam-stop device, anatomy imprinted in sensitivity |
| **Inverse lag ghost** | Lag-contaminated dark correction creates inverted artifact | Offset correction performed during residual lag period |
| **Radar artifact** (CBCT-specific) | Asymmetric lag history in rotating geometry causes arc-shaped shading | Off-center objects in CBCT → ring/radar pattern in reconstruction |

### 8.2 Ghosting Physics (a-Si)

- Ghost contrast changes approximately ±0.5–1% for clinical doses (up to 10 cGy at 6 MV)
- Ghost from high dose (>5 Gy): Sensitivity increases monotonically up to ~5 Gy, then saturates or decreases
- After 20-minute pause: ghost partially recovers
- At clinical doses (≤10 cGy): ghosting ~0.1% — **smaller than 1% lag** → lag is dominant artifact
- Ghosting correction: Standard offset and gain calibration after high-dose exposure

### 8.3 Fluoroscopy Radiography Mixed Mode Ghost

For a-Si panels used for both high-dose radiography and low-dose fluoroscopy:
- A high-dose radiograph fills traps deeply
- Subsequent fluoroscopy frames contain the radiograph ghost as a persistent lag background
- First fluoroscopy frame after radiograph: lag ghost several percent of fluoroscopy signal level
- Corrected by: (1) lag software correction, (2) delay frames, (3) FB hardware reset

### 8.4 Ghosting vs. Lag Quantitative Comparison

| Parameter | Lag (a-Si) | Ghost/Sensitivity (a-Si) |
|-----------|-----------|--------------------------|
| Magnitude at 1st frame | 2.5–5% | <0.1–1% (dose dependent) |
| Duration | Hundreds of frames | Hours (deep trap saturation) |
| Dose dependence | Weak for lag fraction | Significant above 100 cGy |
| Correction method | Recursive deconvolution | Gain recalibration |

---

## 9. FPGA-Implementable Lag Correction

### 9.1 Recursive Filter Structure (FPGA-Suitable)

The LTI recursive lag correction maps directly to an IIR (Infinite Impulse Response) filter in hardware:

**Direct implementation — per pixel, per frame:**

```
// FPGA Pseudocode: Single-Pole Recursive Lag Correction (AR(1)/N=1)
// Input: raw_pixel[row][col] at each frame k
// Output: corrected_pixel[row][col]

CONSTANT alpha = 0x0800;  // Q15 fixed-point: e.g., 0.25 ≈ 0x2000
CONSTANT b1 = 0x0400;     // Q15 fixed-point lag coefficient
CONSTANT norm = 0x7C00;   // Q15 fixed-point normalization 1/(sum of b_n)

REGISTER S[NUM_ROWS][NUM_COLS];  // State variable, initialized to 0 at scan start

FOR each frame k:
  FOR each pixel (row, col):
    lag_estimate = (b1 * S[row][col] * alpha) >> 15   // fixed-point multiply
    x_hat = (raw_pixel[row][col] - lag_estimate) * norm >> 15
    S[row][col] = x_hat + (S[row][col] * alpha) >> 15
    corrected_pixel[row][col] = x_hat
```

**Multi-component N=4 implementation:**

```verilog
// Verilog pseudocode: 4-pole recursive lag correction
// Parameters stored in lookup tables (precomputed from calibration)
parameter N = 4;
parameter BITWIDTH = 16;  // 16-bit fixed-point, Q12 format

// Lag rates (exp(-a_n)) precomputed as fixed-point constants
reg [BITWIDTH-1:0] decay[N-1:0];  // exp(-a_n), Q15
reg [BITWIDTH-1:0] b_coeff[N-1:0]; // b_n coefficients, Q15
reg [BITWIDTH-1:0] b_sum;          // sum of all b_n, Q15

// State variables: one array per exponential component
reg [BITWIDTH-1:0] S[N-1:0][MAX_ROWS-1:0][MAX_COLS-1:0];

// Per-frame, per-pixel computation pipeline
always @(posedge clk) begin
  if (new_pixel) begin
    // Stage 1: Compute lag estimate
    lag_sum = 0;
    for (n = 0; n < N; n++) begin
      lag_sum += (b_coeff[n] * S[n][row][col] * decay[n]) >> (2*FRAC_BITS);
    end
    
    // Stage 2: Deconvolve
    x_hat = (raw_in - lag_sum) / b_sum;  // Division by b_sum via LUT multiply
    
    // Stage 3: Update state variables
    for (n = 0; n < N; n++) begin
      S[n][row][col] = x_hat + (S[n][row][col] * decay[n]) >> FRAC_BITS;
    end
    
    corrected_out = x_hat;
  end
end
```

### 9.2 Fixed-Point Arithmetic Considerations

| Parameter | Recommended Precision | Notes |
|-----------|----------------------|-------|
| Pixel data | 14–16 bits unsigned | Matches ADC resolution |
| State variable S_{n,k} | 20–24 bits | Accumulates products; overflow risk |
| Lag coefficients b_n | Q15 (16-bit, 1 integer + 15 fractional) | b_n << 1 typically |
| Decay factors exp(-a_n) | Q15 | Close to 1.0 for slow components |
| Intermediate products | 32-bit | After multiply before truncation |
| Final corrected output | 14–16 bits | Match input width |

**Critical:** The slow component (n=1, τ₁ ≈ 400 frames) requires high precision for S_{1,k} as it accumulates small increments over many frames. Recommend 24-bit accumulator for the slow state variable.

### 9.3 Frame Buffer Requirements

For a 2048×2048 pixel detector, 16 fps, N=4 components:

| Resource | Quantity | Memory (16-bit words) |
|----------|----------|----------------------|
| State variables S_{n,k} (N=4) | 4 arrays × 2048×2048 | **16 MB per array → 64 MB total** |
| Frame buffer (1 frame) | 1 × 2048×2048 | 8 MB |
| Previous frame (for AR(1)) | 1 × 2048×2048 | 8 MB |
| Coefficient LUTs | Per pixel calibration | Optional: 4×N floats per pixel |

**Memory optimization:** For typical detectors (e.g., 3000×3000 at 14 fps):
- N=1 (AR(1)): 1 state frame × 14-bit = ~12.6 MB
- N=4 (LTI): 4 state frames = ~50 MB (requires DDR3 SDRAM, not on-chip BRAM)

### 9.4 Pipeline Implementation

```
Timing budget at 15 fps, 2048×2048:
  Frame period: 66.7 ms
  Pixel rate required: 2048 × 2048 / 66.7 ms ≈ 62.9 Mpixels/sec
  At 100 MHz FPGA clock: 1.59 clock cycles/pixel → need parallel processing

Pipeline stages:
  Stage 1: Read S_{n,k} from SDRAM        (2–4 cycles)
  Stage 2: Multiply b_n × S_{n,k}         (1 DSP slice, 3 cycles pipelined)
  Stage 3: Multiply by exp(-a_n)          (1 DSP slice, 3 cycles pipelined)
  Stage 4: Accumulate lag estimate sum    (N cycles serial, or N parallel adders)
  Stage 5: Subtract from raw pixel        (1 cycle)
  Stage 6: Divide by b_sum (multiply by 1/b_sum) (1 DSP slice, 3 cycles)
  Stage 7: Write new x̂ to output         (1 cycle)
  Stage 8: Update S_{n,k+1}, write to SDRAM (2–4 cycles)

Total latency: ~15–20 clock cycles (150–200 ns at 100 MHz)
  → Well within one pixel clock budget at reasonable pixel rates
```

### 9.5 DSP Resource Estimate (Xilinx/Intel FPGA)

For N=4 recursive filter, per pixel pipeline:

| Operation | DSP Slices |
|-----------|-----------|
| 4× multiply b_n × S_{n,k} | 4 DSP18 |
| 4× multiply by exp(-a_n) | 4 DSP18 |
| 1× divide (multiply by 1/b_sum LUT) | 1 DSP18 |
| 4× state variable update S multiply | 4 DSP18 |
| **Total** | **~13–16 DSP48E slices** |

For a 1024×1024 @ 30 fps system with parallel pixel processing (4 pixels/cycle):
- ~16 DSP48 slices per pipeline
- 4 BRAM blocks for coefficient storage
- External DDR for state variable storage (~50 MB for N=4, 1024×1024)
- FPGA logic: ~5,000–10,000 LUTs for control, address generation, pipeline stages

### 9.6 Latency Requirements

For real-time fluoroscopy:
- Frame rate: 7.5–30 fps (period: 33–133 ms)
- Maximum allowable algorithmic latency: **1 frame period** (33 ms at 30 fps)
- The recursive filter processes pixel-by-pixel in streaming mode: output available within 1–2 pixel clocks after input
- Total processing latency << 1 ms for a pipelined implementation → meets real-time requirements

### 9.7 FPGA IIR Filter for Poisson Noise + Lag (Fluoroscopy Use Case)

An FPGA-oriented IIR temporal filter for X-ray fluoroscopy (Boracchi et al.) implemented on StratixIV:
- **Uses only 22% of resources** for 1024×1024 @ 49 fps (vs. 80% for equivalent FIR filter)
- Conditional reset to minimize motion blur
- Fixed-point coefficients optimized using Steiglitz–McBride iterative method for minimal nonzero elements
- AXI4-Stream interface for integration with image processing pipeline

---

## 10. Offset Correction for Lag

### 10.1 Dark Frame Subtraction Method

Standard offset correction subtracts a dark frame (acquired without X-ray) to remove the DC offset from lag-induced residual charge:

```
I_corrected(k) = I_raw(k) - I_dark(k)
```

**Limitation:** The dark frame captures lag at the moment of dark acquisition; as lag decays between dark acquisition and image use, the correction becomes stale.

**Improved method (Patent US7792251B2):** Measure lag *immediately before* each integration phase:

```
Algorithm:
  1. Prior to each X-ray exposure frame:
     a. Acquire dark frame I_dark at initial read phase (measures current lag)
     b. Acquire offset image I_offset (very brief read without X-ray, << read period)
     c. Lag image = I_dark - I_offset
     d. Multiply lag image by (N+1) to extrapolate to integration period
  2. During X-ray exposure:
     a. Acquire raw image I_raw
  3. Correction:
     I_corrected = I_raw - (N+1) × (I_dark - I_offset)
```

This enables pixel-by-pixel real-time correction without a mathematical lag model.

### 10.2 Inverse Lag Ghost from Flawed Offset Correction

If a dark frame calibration is acquired *during* residual lag (after a high-dose exposure):
- The calibration "writes" the lag signal into the dark correction as a negative offset
- Subsequent images show **radiolucent inverse ghost** artifacts (apparent hypodense regions)
- This artifact persists (or intensifies) as lag decays in subsequent acquisitions
- Can persist 10+ minutes
- Prevention: Ensure offset calibration is done with no recent high-dose exposures

### 10.3 Residual Lag Uniformization (LED Method)

By uniformly illuminating all photodiodes with an LED between frames, lag is converted from spatially varying to spatially uniform:
- Uniform lag → subtracted entirely by standard dark correction
- Implementation: LED array beneath panel, synchronized to frame timing
- Limitation: LED emission creates uniform bright offset; does not address deep traps

---

## 11. Scan-Dependent Lag

### 11.1 Gate Line Scanning Effect

In rolling-readout (progressive scan) flat-panel detectors, gate lines are activated sequentially:
- Different pixel rows are read out at different times within a frame
- The residual lag signal is **nonuniform within a frame** (earlier rows have had longer time for lag to decay before readout)
- This "gate line scanning" effect causes the conventional GA lag measurement method to yield incorrect estimates
- The LCF must be measured accounting for this temporal gradient within a frame

### 11.2 Frame Rate Dependence

Lag time constants are frame-rate dependent when expressed in frame units:
- At higher frame rates (more frames/second), fewer trap states fully empty between frames → larger lag fraction per frame
- Physical time constants (in seconds) are frame-rate independent
- At 30 fps: inter-frame interval = 33 ms; at 7.5 fps: 133 ms
- The slow component (τ₁ ~ 27 s) spans ~400 frames at 15 fps but ~200 frames at 7.5 fps

### 11.3 Gate Pulse Width Dependence

- Wider gate pulses → more complete reset of each row → lower residual charge → lower lag
- Narrower gate pulse (for higher frame rate) → incomplete reset → higher lag fraction
- Trade-off: gate pulse width vs. maximum frame rate vs. readout noise

### 11.4 Temperature Dependence

- TFT off-state leakage (and thus TFT-source lag) increases with temperature (TFT activation energy decreases at higher temperatures)
- Trap-assisted tunneling current is temperature-dependent: suppressed at low temperatures, enhanced at elevated temperatures
- Deep trap emission rates (a_thermal) are Arrhenius-activated:
  ```
  a_thermal ∝ ν₀ × exp[-(E_c - E_tr)/kT]
  ```
  Higher T → faster thermal emission → shorter effective time constants for deep traps
- Practical implication: Detector warm-up period changes lag characteristics; calibration at operating temperature is essential

### 11.5 Exposure History Dependence

- Lag coefficients `b_n(x)` and rates `a_n(x)` depend on current AND recent exposure history
- Extended high-dose exposure fills traps more completely → subsequent lag release per frame is larger
- For RSRF (rising step), the 4% exponential gain increase reflects traps filling progressively as detector is irradiated
- First-frame lag measurement depends on pre-conditioning (number of prior exposure frames)

---

## 12. Real-Time Systems and Pipeline Implementation

### 12.1 System Architecture

```
Block Diagram: Real-time lag correction pipeline

  X-ray Panel
      │
      │ Raw pixel data (14-bit ADC)
      ▼
  ┌─────────────┐
  │  Gain/Offset │  (standard flat-field and dark correction)
  │  Correction  │
  └──────┬──────┘
         │
         ▼
  ┌─────────────┐
  │  Lag Correct │  (recursive IIR filter, N=4 or N=1)
  │  FPGA Engine │◄── State RAM (DDR3, ~50-64 MB for N=4)
  └──────┬──────┘    Coefficient LUT (on-chip BRAM)
         │
         │ Corrected image
         ▼
  ┌─────────────┐
  │  Frame Buffer│  → Display / CT Reconstruction
  └─────────────┘
```

### 12.2 Real-Time Processing Requirements

| Parameter | Typical Fluoroscopy | CBCT |
|-----------|--------------------|----|
| Frame rate | 7.5–30 fps | 10–15 fps (rotation) |
| Pixel count | 1024²–2048² | 1024²–2048² |
| Required latency | ≤1 frame | ≤1 frame |
| Processing throughput | 10–125 Mpixels/sec | 10–60 Mpixels/sec |
| State memory | 4× frame buffer (N=4) | 4× frame buffer |

### 12.3 Clinical Implementation Considerations

**Step 1: Calibration (offline):**
- Acquire FSRF at 3–9 exposure levels spanning detector dynamic range
- Fit N=4 exponential model to extract a_n, b_n
- For NLCSC: additionally extract Q(x) at each exposure, fit exposure-dependent rates
- Store calibration LUTs in FPGA/processor memory

**Step 2: Runtime correction (per frame):**
- Apply recursive filter per pixel (Eq. 3–4)
- For NLCSC: look up exposure-dependent coefficients based on current estimated signal

**Step 3: Monitoring:**
- Periodically re-acquire FSRF (e.g., daily QA) to track parameter drift
- Temperature monitoring and optional temperature compensation

### 12.4 GPU-Based Alternative

For software implementations (not FPGA):
- The per-pixel recursive filter parallelizes trivially on GPU
- Each pixel's state is independent → embarrassingly parallel
- GPU with 2000+ CUDA cores: processes 1024²=1M pixels in parallel
- Typical GPU latency: <5 ms for one frame
- Suitable for research/post-processing; may have latency concerns for real-time fluoroscopy

### 12.5 Latency Budget Analysis

```
For 15 fps fluoroscopy (66.7 ms/frame):
  X-ray exposure window:  13 ms (pulsed mode)
  Gate readout time:      ~20 ms (rolling readout, 2048 rows × 20 μs/row)
  Lag correction pipeline:
    - Pixel arrival → processing start: 0 (streaming)
    - Per-pixel latency: ~0.15 μs (150 ns at 100 MHz, 15-stage pipeline)
    - Full frame correction done within readout period
  Output available:      Simultaneously with readout completion
  Total algorithmic latency: < 1 ms (negligible vs. frame period)
```

---

## 13. Deep Learning Approaches (2025)

### 13.1 Lag-Net (Ren et al., 2025)

A convolutional neural network trained to remove lag artifacts from CBCT projections:

**Training data generation:**
- "Nearly lag-free" images created by hardware dual-scan method:
  - First scan: standard acquisition (produces lag)
  - Second scan of same object: timing adjusted to measure lag signal from first scan
  - Lag-corrected = first scan minus measured lag
- Lag-Net trained with these hardware-corrected images as ground truth

**Performance:**
- Outperforms LTI correction in low-exposure scenarios
- Achieves results comparable to hardware correction without operational complexity
- Better handles exposure-dependent lag without explicit calibration

**Architecture:** Convolutional encoder-decoder network operating on temporal sequences of projections.

**Limitation:** Hardware dual-scan reference data required for training; cannot generalize to different detector geometries without retraining.

---

## 14. Comparison: a-Si vs. a-Se Detectors

### 14.1 Direct vs. Indirect Conversion Lag

| Property | a-Si (Indirect) | a-Se (Direct) |
|----------|----------------|---------------|
| Conversion | X-ray → CsI scintillator → a-Si photodiode | X-ray → a-Se photoconductor directly |
| Primary lag source | a-Si trap states in photodiode | a-Se trap states + hole transport |
| Lag magnitude | 2–5% first frame | Generally lower than a-Si |
| Ghosting behavior | Dominated by lag | Both lag and sensitivity change |
| Image quality (low dose) | Superior to a-Se | Lower DQE at low dose settings |
| First-frame lag | ~4% typical | ~0.044 measured (Granfors et al.) |
| Lag after 1 second | <0.5% | < 0.007 (after 1 s) |

### 14.2 IGZO TFT Replacement

Metal-oxide IGZO (InGaZnO) TFTs combined with a-Si:H photodiodes offer significantly improved lag:
- Off-state current of IGZO TFT ~1.7× lower than a-Si:H TFT → reduced TFT-source lag
- Lower electronic noise floor
- The photodiode lag (a-Si trap physics) remains; IGZO only addresses TFT contribution
- IGZO-based FPD shows ≥25% improvement in NEQ for head CBCT vs. a-Si:H TFT FPD

---

## 15. Summary Tables

### 15.1 Lag Algorithms Comparison

| Algorithm | Type | Accuracy | Compute | Memory | Nonlinear? | FPGA-Suitable? |
|-----------|------|----------|---------|--------|-----------|----------------|
| No correction | — | Baseline | None | None | N/A | N/A |
| Flush-N empty frames | Hardware | Moderate | None | None | No | N/A |
| LED/Forward bias | Hardware | Good | Minimal | None | Partial | N/A |
| LTI N=1 (AR(1)) | Software | Low-moderate | Very low | 1 frame | No | ★★★★★ |
| LTI N=4 (Hsieh) | Software | Good (near-calibration) | Low | 4 frames | No | ★★★★ |
| NLCSC N=4 | Software | Excellent | Moderate | 4 frames + LUTs | Yes | ★★★ |
| Lag-Net (DL) | Software | Excellent | High | Large model | Yes | ★ (GPU only) |
| Direct measurement (Pat.) | Hardware-SW | Good | Low | 2 dark frames | No | ★★★★ |

### 15.2 Quantitative Lag Values from Literature

| Condition | 1st Frame Lag | 50th Frame Lag | Detector/Reference |
|-----------|-------------|----------------|---------------------|
| Uncorrected, 2% exposure | 3.7% | 0.96% | Varian 4030CB, 15 fps |
| Uncorrected, 27% exposure | 2.9% | 0.49% | Varian 4030CB, 15 fps |
| Uncorrected, 84% exposure | 2.3% | 0.42% | Varian 4030CB, 15 fps |
| LTI corrected, 2% exp. | 1.4% | 0.48% | Varian 4030CB, 15 fps |
| NLCSC corrected, 2% exp. | 0.25% | ~0% | Varian 4030CB, 15 fps |
| Forward bias, residual | <0.3% | <0.1% | Varian 4030CB (modified) |
| a-Si (41×41cm), 1st frame | 0.044 | — | Granfors et al. 2003 |
| a-Si, after 1 s (fluoroscopy) | — | 0.007 | Granfors et al. 2003 |
| MV indirect FPI (6 MV) | ~2% | — | Pang et al. 2007 |

### 15.3 N=4 Multi-Exponential Time Constants

| Component | Physical Decay | Time (s) at 15fps | Time (s) at 30fps | Responsible for |
|-----------|---------------|-------------------|-------------------|----|
| n=4 (fast) | τ₄ = 0.09 s | 0.09 s | 0.09 s | First-frame lag |
| n=3 | τ₃ = 0.4 s | 0.4 s | 0.4 s | Short-term lag |
| n=2 | τ₂ = 3.2 s | 3.2 s | 3.2 s | Fluoroscopy lag |
| n=1 (slow) | τ₁ = 26.7 s | 26.7 s | 26.7 s | **Radar artifact / CBCT** |

### 15.4 Hardware Method Performance Summary

| Method | 1st Frame Reduction | Implementation Cost | Frame Rate Impact |
|--------|-------------------|--------------------|----|
| Flush-1 (1 empty frame) | ~50% | Software only | −50% |
| LED light saturation | >93%, RSRF <1% | LED array hardware | ~1 ms overhead |
| Forward bias (20 pC) | 95% (first frame) | Modified charge amplifiers | 30 ms overhead/frame |
| Forward bias (optimized) | 95% | Next-gen amplifiers | ~40 μs overhead |

---

## 16. References

### Key Academic Papers

1. **Starman J., Star-Lack J., Virshup G., Shapiro E., Fahrig R.** (2012). "A nonlinear lag correction algorithm for a-Si flat-panel x-ray detectors." *Medical Physics* 39(10). DOI: 10.1118/1.4752087. [PMC3465354](https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/)

2. **Shapiro E., Virshup G., Star-Lack J., Starman J., Fahrig R.** (2011). "Investigation into the optimal linear time-invariant lag correction for radar artifact removal." *Medical Physics* 38(5). DOI: 10.1118/1.3574873. [PMC3098893](https://pmc.ncbi.nlm.nih.gov/articles/PMC3098893/)

3. **Starman J., Tognina C., Partain L., Fahrig R.** (2012). "A forward bias method for lag correction of an a-Si flat panel detector." *Medical Physics* 39(1). DOI: 10.1118/1.3664004. [PMC3257750](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/)

4. **Lee E., Kim D.S.** (2023). "Nonlinear Lag Correction Based on the Autoregressive Model for Dynamic Flat-Panel Detectors." *IEEE Access* 11. DOI: 10.1109/ACCESS.2023.3268521.

5. **Kim D.S., Lee E.** (2021). "Measurement of the Lag Correction Factor in Low-Dose Fluoroscopic Imaging." *IEEE Transactions on Medical Imaging* 40(6). DOI: 10.1109/TMI.2021.3063350.

6. **Kim D.S., Lee E.** (2020). "Signal Lag Measurements Based on Temporal Correlations." *IEEE Signal Processing Letters*. DOI: 10.1109/LSP.2020.3043976.

7. **Lee E., Kim D.S.** (2022). "Lag Correction Factor Measurement Based on the Temporal Power Spectral Density and Its Size." *ITC-CSCC*. DOI: 10.1109/ITC-CSCC55581.2022.9894932.

8. **Pang G., Mail N., O'Brien P.** (2007). "Lag correction model and ghosting analysis for an indirect-conversion flat-panel imager." *J. Appl. Clin. Med. Phys.* 8(3). DOI: 10.1120/jacmp.v8i3.2483. [PMC5722609](https://pmc.ncbi.nlm.nih.gov/articles/PMC5722609/)

9. **Sato H. et al.** (2015). "Evaluation of image lag in a flat-panel, detector-equipped cardiovascular X-ray machine using a newly developed dynamic phantom." *J. Appl. Clin. Med. Phys.* 16(2). DOI: 10.1120/jacmp.v16i2.5213.

10. **Ren C., Kan S., Huang W., Xi Y., Ji X., Chen Y.** (2025). "Lag-Net: Lag correction for cone-beam CT via a convolutional neural network." *Computer Methods and Programs in Biomedicine* 261. DOI: 10.1016/j.cmpb.2025.108753.

11. **Sheth N. et al.** (2022). "Technical assessment of 2D and 3D imaging performance of an IGZO-based flat-panel X-ray detector." *Medical Physics*. DOI: 10.1002/mp.15605.

12. **Walz-Flannigan A. et al.** (2012). "Artifacts in Digital Radiography." *AJR Am. J. Roentgenol.* 198(6). [https://ajronline.org/doi/full/10.2214/AJR.11.7237](https://ajronline.org/doi/full/10.2214/AJR.11.7237)

13. **Han B. et al.** (2022). "Characterization of Flexible Amorphous Silicon Thin-Film Transistor-Based Detectors with PIN Diode in Radiography." *Diagnostics* 12(9). DOI: 10.3390/diagnostics12092103. [PMC9497934](https://pmc.ncbi.nlm.nih.gov/articles/PMC9497934/)

14. **Konst B. et al.** (2020). "Novel method to determine recursive filtration and noise reduction in fluoroscopic imaging." *J. Appl. Clin. Med. Phys.* 22(1). DOI: 10.1002/acm2.13115. [PMC7856489](https://pmc.ncbi.nlm.nih.gov/articles/PMC7856489/)

### Patents

15. **Hsieh J. (General Electric)** (1993). "Compensation of computed tomography data for detector afterglow." US Patent 5,249,123. [https://patents.google.com/patent/US5249123A/en](https://patents.google.com/patent/US5249123A/en)

16. **French invention (Thales/GE Healthcare)** (2008). "Method for the correction of lag charge in a flat-panel X-ray detector." US Patent 7,792,251. [https://patents.google.com/patent/US7792251B2/en](https://patents.google.com/patent/US7792251B2/en)

17. **GE Healthcare** (filed 2003). "Methods and apparatus for processing a fluoroscopic image." US Publication 20040218729. [https://patexia.com/us/publication/20040218729](https://patexia.com/us/publication/20040218729)

18. **Varian Medical Systems** (Japanese Patent JP4366177B2). "Method and apparatus for correcting retained image." [https://patents.google.com/patent/JP4366177B2/en](https://patents.google.com/patent/JP4366177B2/en)

### Standards

19. **IEC 62220-1-3:2008.** "Medical electrical equipment — Characteristics of digital X-ray imaging devices — Part 1-3: Determination of the detective quantum efficiency — Detectors used in dynamic imaging." International Electrotechnical Commission.

20. **IEC 62220-1-1:2015.** "Medical electrical equipment — Characteristics of digital X-ray imaging devices — Part 1-1: DQE for detectors used in radiographic imaging." International Electrotechnical Commission.

### Conference Papers

21. **Hsieh J., Gurmen O.E., King K.F.** (2000). "Recursive correction algorithm for detector decay characteristics in CT." *Proc. SPIE 3977*, 298–305. DOI: 10.1117/12.384505. [NASA ADS](https://ui.adsabs.harvard.edu/abs/2000SPIE.3977..298H/abstract)

22. **Starman J., Tognina C., Virshup G., Star-Lack J., Mollov I., Fahrig R.** (2008). "Parameter investigation and first results from a digital flat panel detector with forward bias capability." *Proc. SPIE 6913*. DOI: 10.1117/12.772356.

---

*End of Report*

**File:** `/home/user/workspace/research_05_lag_correction.md`  
**Word Count:** ~7,500 words  
**Last Updated:** 2026-03-18
