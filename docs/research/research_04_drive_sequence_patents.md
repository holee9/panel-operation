# X-ray Flat Panel Detector Driving Sequences
## Patents, Academic Papers, and Standards — Comprehensive Research

**Compiled:** March 18, 2026  
**Scope:** FPD panel driving sequences, calibration algorithms, FPGA control, and acquisition protocols  
**Key manufacturers covered:** Varian / Varex, GE Healthcare, Carestream Health, Canon, Siemens Healthineers, Perkin Elmer, Samsung

---

## Table of Contents

1. [Fundamental FPD Architecture and Operation Principles](#1-fundamental-fpd-architecture)
2. [Panel Stabilization and Initialization Sequences](#2-panel-stabilization-and-initialization)
3. [Dark Frame (Offset Map) Acquisition Algorithm](#3-dark-frame-offset-map-acquisition)
4. [Flat Field / Bright Frame Calibration](#4-flat-field--bright-frame-calibration)
5. [Lag Correction and Multi-Frame Averaging](#5-lag-correction-and-multi-frame-averaging)
6. [Preconditioning / Prep Pulse Sequences](#6-preconditioning--prep-pulse-sequences)
7. [Fluoroscopy Continuous Mode](#7-fluoroscopy-continuous-mode)
8. [Static Radiography Mode](#8-static-radiography-mode)
9. [Triggered Acquisition and X-ray Generator Synchronization](#9-triggered-acquisition-and-sync)
10. [Frame Rate Control and Variable Timing](#10-frame-rate-control)
11. [Power-On Initialization Sequence](#11-power-on-initialization-sequence)
12. [Standards and Regulatory References](#12-standards-and-regulatory-references)
13. [Patent Index](#13-patent-index)
14. [Academic Paper Index](#14-academic-paper-index)
15. [FPGA Implementation Insights](#15-fpga-implementation-insights)

---

## 1. Fundamental FPD Architecture

### 1.1 Indirect-Conversion a-Si FPD Structure

The dominant flat panel detector architecture uses hydrogenated amorphous silicon (a-Si:H) as the sensing medium in an indirect conversion configuration:

```
X-ray → Scintillator (CsI:Tl or GOS) → Visible Light
     → a-Si Photodiode Array → Charge
     → TFT Switch (per pixel) → Readout
     → Gate Driver ICs → Column Data Lines
     → Charge-Sensitive Amplifiers (AFE) → ADC → FPGA
```

Each pixel contains:
- A **PIN photodiode** (a-Si:H, 1–2 µm thick) that converts visible photons to electron-hole pairs and stores charge in its internal capacitance
- A **TFT switch** (a-Si:H, P-I-N or N-type) with ON/OFF ratio >10⁷, OFF current <0.1 pA
- A **storage capacitor** (1–50 pF)

**Key physical properties affecting driving sequences:**
- a-Si:H contains metastable bandgap states (charge traps/defects) that fill during X-ray exposure and empty slowly (minutes time scale)
- These traps cause: lag (residual signal), ghosting, and drift — all requiring active management in driving sequences
- Temperature sensitivity: dark current and pixel offset drift significantly with temperature (15–35°C operational range)
- Charge trapping memory requires "reset" protocols before each acquisition to achieve reproducible starting conditions

**Reference:** Siemens Hoheisel et al., "Amorphous Silicon X-Ray Detectors," Proc. ISCMP, 1996 — [PDF](https://www.mhoheisel.de/docs/ISCMP91996112.pdf)

### 1.2 Pixel Array Organization

- **Array size:** Typically 1000×1000 to 3000×3000 pixels
- **Pixel pitch:** 100–200 µm
- **Gate lines:** N horizontal lines, each driven by gate driver IC in sequence
- **Data lines:** M vertical lines, connected to charge-sensitive amplifiers
- **Scanning:** Gate lines activated one row at a time; all columns read simultaneously per row

---

## 2. Panel Stabilization and Initialization

### 2.1 Core Problem: Charge Trap State Management

The fundamental challenge in FPD initialization is that a-Si:H contains metastable charge traps whose fill state determines pixel response characteristics. These traps:
- Fill during X-ray exposure (trap lifetime: milliseconds to minutes)
- Create non-reproducible starting conditions if not managed
- Cause dark current drift when the panel transitions between power states

### 2.2 Refresh and Prepare Cycle (Carestream EP2148500A1)

**Patent:** EP2148500A1 — *Dark Correction for Digital X-ray Detector*, Carestream Health, filed 2009-07-20  
**URL:** https://patents.google.com/patent/EP2148500A1/en

**Refresh Cycle (puts a-Si into reproducible state):**

Method A — Optical Reset:
```
1. Activate backlight refresh panel (LED array behind substrate)
2. Flood a-Si:H imager with visible light
3. Fills large fraction of trapping sites to known state
4. Repeat N times until reproducible output achieved
```

Method B — Electrical Bias Cycling:
```
1. Switch photodiode bias voltage (positive → negative polarity)
2. Switch TFT gate voltages to force charge movement
3. Fill then empty traps predictably
4. One or more cycles may be required
```

**Prepare Cycle (initializes panel for readout):**
```
1. Apply photodiode bias voltage via gate drivers
2. Photodiode enters integration mode (reverse-biased)
3. Charge converts incident light → stored electrical charge
4. Internal pixel capacitance charged to known reference
```

**Timing:** Refresh + Prepare causes ~500 ms exposure delay when transitioning to High power state.

**Power State Machine:**

| State | Condition | Voltages | Timing |
|-------|-----------|----------|--------|
| **Low** | Between studies | Minimal array voltages; embedded controller active | Default state |
| **Medium** | Study in progress | Partial array voltages | Between exposures |
| **High** | Active acquisition | All voltages stable; refresh/prepare executed | During exposure window |

Transitions:
- `Begin Study → Low → Medium`
- `Prep Command → Medium → High` (500 ms stabilization)
- `Post-readout → High → Medium`
- `End Study → Medium → Low`

### 2.3 Thermal Pre-Conditioning Considerations

From EP2148500A1 and AAPM TG-150 practice:

> "Rapid local or global changes in temperature are likely to cause a range of imaging anomalies. DR panel imaging characteristics immediately following a change in operating power can differ measurably from imaging characteristics a few minutes later."

**Protocol elements:**
- Record detector temperature at exposure time (metadata)
- Apply temperature-indexed offset adjustment maps
- Allow minimum warm-up time after power-state transitions (typically 2–15 s prep time)
- Characterize detector over 15–35°C ambient range

### 2.4 Siemens Double-Diode Architecture Optical Reset

**Reference:** Hoheisel et al. (Siemens), ISCMP 1996

The Siemens detector uses double-diode technology (NIP photodiode + PIN switching diode) with explicit optical reset built into each frame cycle:

```
Frame N Sequence:
  1. VP1 (≈4V): Applied to all row lines simultaneously → reverse biases all photodiodes
  2. X-ray exposure window (several ms)
  3. VP2 (≈4.5V): Applied row-by-row for charge readout
     - VP2 > VP1 adds predictable offset charge (subtracted in processing)
  4. Reset light flash: Short back-side illumination through substrate
     - Decharges all diodes; fills deep a-Si traps to equal starting state
     - Intensity tuned to minimize memory effect
  5. → Frame N+1 begins with known trap state
```

**Frame rates:** 12.5 fps (1024×1024) or 25 fps (512×512) in fluoroscopy mode.

---

## 3. Dark Frame (Offset Map) Acquisition Algorithm

### 3.1 Purpose and Physics

Dark frames capture pixel-by-pixel offset signal in the absence of X-ray exposure:
- **Sources of offset:** TFT leakage current, charge retention in switching element, charge redistribution from bias protection circuits, thermal dark current in photodiode
- **Magnitude:** Offset can be larger than the X-ray signal
- **Variability:** Offset drifts with temperature, exposure history, time since last exposure, and power state transitions
- **Noise:** Single dark image is noisy; averaging required

### 3.2 Basic Offset Correction Algorithm (GE — US5452338A)

**Patent:** US5452338A — *Method and System for Real Time Offset Correction in a Large Area Solid State X-ray Detector*, GE (assigned), filed 1994-09-19  
**URL:** https://patents.google.com/patent/US5452338A/en

**Recursive Filter for Continuously Updated Offset Image:**

```
Initialization (first dark image):
    a₀ = p₀   (pixel value of first dark frame)

Recursive update (subsequent dark images):
    aᵢ = (1 - 1/n) × aᵢ₋₁ + (1/n) × pᵢ

Where:
    aᵢ = current offset map pixel value
    pᵢ = incoming dark frame pixel value  
    n  = smoothing parameter (controls noise vs. tracking speed)
    
Noise equivalent to averaging (2n-1) frames
Time constant ≈ n^(1/2) iterations
At n=16, 30 Hz: time constant ≈ 0.52 seconds
```

**Corrected X-ray image:**
```
corrected(x,y) = raw_exposure(x,y) - offset_memory(x,y)
```

**Operating modes:**
- **Scrubbing mode** (no X-rays): Continuously update offset_memory using recursive filter
- **Exposure mode** (X-rays present): Subtract stored offset_memory; do not update

### 3.3 Multi-Frame Dark Capture with Metadata (Carestream EP2148500A1)

**Advanced algorithm with exposure metadata compensation:**

```
Step 1: Acquire exposure E + metadata M (prep time, temperature, time-in-High)
Step 2: Acquire n dark images D₁...Dₙ (post-exposure preferred)
Step 3: Intermediate correction:
           E_c = E - mean(D₁...Dₙ)
Step 4: Retrieve offset adjustment map DD_x from stored factory maps
           indexed by metadata M (or interpolate between maps)
Step 5: Final correction:
           E_final = E_c + DD_x
```

**Stored offset adjustment map selection:**
```
If exact metadata match:    DD_x = DD[M]
If interpolation needed:    DD_x = DD[k] + (M - M_k)/(M_{k+1} - M_k) × (DD[k+1] - DD[k])
Prediction model:           m = x₁ × exp(-x₂ × t_prep + x₃)
```

**Dark capture timing options:**

| Timing | Description | Trade-offs |
|--------|-------------|------------|
| Pre-exposure darks | Acquire dark frames before X-ray | Better temporal match; delays workflow |
| Post-exposure darks | Acquire dark frames after X-ray | No workflow delay; slight temporal mismatch |
| Mixed (pre + post) | Average pre and post frames | Best match; double the dark capture time |

**Embedded controller automation:** The FPGA/embedded controller replicates user workflow timing but replaces X-ray events with dark captures, preserving identical prep-time and power-state transitions.

### 3.4 Periodic Weighted Update Method (Granfors — US2003/0223539)

Referenced in EP2148500A1: "Method and apparatus for acquiring and storing multiple offset corrections for amorphous silicon flat panel detector," by Granfors et al. (GE Healthcare)

**Algorithm:**
```
Periodic single dark capture (between exposures):
    offset_new = α × offset_old + (1-α) × dark_frame_new

Where α = weighting factor (e.g., 0.9 for slow tracking)
```

**Best suited for:** Conventional FPDs running continuously in stable temperature environments.

### 3.5 Dark Frame Averaging Best Practices

From EP2148500A1 characterization studies (72 exposures, 15–35°C):

```
Noise reduction strategy:
    1. Acquire N = 2–8 dark frames per exposure
    2. Average: D_avg = (1/N) × ΣDᵢ
    3. Noise is reduced by factor √N relative to single frame
    
Alternative — Frequency decomposition:
    D_smoothed = LPF(D_avg) + HPF(single_dark)
    (preserves spatial detail from single frame, low-noise average for DC)
```

**Reference:** Brian G. Rodricks et al., "Filtered gain calibration and its effect on DQE and image quality in digital imaging systems," SPIE Vol. 3977, pp. 476-485.

---

## 4. Flat Field / Bright Frame Calibration

### 4.1 Purpose and Procedure

The gain (flat field) calibration maps pixel-to-pixel sensitivity variations from:
- Variations in scintillator thickness and conversion efficiency
- Photodiode responsivity differences
- TFT switch charge transfer non-uniformity
- X-ray beam non-uniformity (heel effect)

**Standard calibration sequence:**

```
Phase 1: Collect dark/offset reference
    1. Block X-ray beam completely
    2. Acquire N_dark dark frames (N_dark = 8–32 typical)
    3. Compute: D_ref(x,y) = mean(dark frames)

Phase 2: Collect bright/flat-field images  
    1. No patient/phantom in beam (flood field)
    2. Use clinical beam parameters (or calibration conditions)
    3. Acquire N_bright flat-field frames at each dose level
    4. Compute: I_raw(x,y) = mean(flat-field frames)
    5. Offset-correct: I_corr(x,y) = I_raw(x,y) - D_ref(x,y)

Phase 3: Compute gain map
    Gain(x,y) = I_corr(x,y) / mean(I_corr)

Phase 4: Correction applied to patient images
    Patient_corr(x,y) = [Patient_raw(x,y) - D_ref(x,y)] / Gain(x,y)
```

**Reference:** Carestream EP2148500A1; AAPM TG-150 report (https://www.aapm.org/pubs/reports/TG-150_final.pdf)

### 4.2 Gain Correction Patent — Siemens (US20050092909A1)

**Patent:** US20050092909A1 — *Method of Calibrating a Digital X-ray Detector*, Siemens AG, filed 2004-09-21  
**URL:** https://patents.google.com/patent/US20050092909A1/en

Key innovation: Apply **smoothing filter** to calibration (gain) image before use:

```
Traditional:    G(x,y) = I_bright(x,y) / mean(I_bright)
                → Contains high-frequency noise → propagates to corrected image

Filtered:       G_filtered(x,y) = smooth_filter(G(x,y))
                → Reduce pixel-to-pixel contrast (fixed pattern noise)
                → Separate 1D filter (row/column dependent) or 2D filter
                
With accumulation module:
                G_filtered_avg = mean(G_filtered_1, ..., G_filtered_k)
```

**Filter types:**
- 1D filter: For systematic row/column brightness differences (array structure)
- 2D filter: For non-directional brightness fluctuations or non-uniform X-ray field

### 4.3 Multi-Angle / Multi-Energy Gain Maps (Siemens US7404673B2)

**Patent:** US7404673B2 — *Method for Generating a Gain-Corrected X-ray Image*, Siemens AG, filed 2006-04-13  
**URL:** https://patents.google.com/patent/US7404673B2/en

**Problem:** Single gain image insufficient when detector angle or kVp changes.

**Algorithm:**
```
Calibration phase:
    Record G(θ₁) at angular position θ₁ = 0°
    Record G(θ₂) at angular position θ₂ = 90°
    Record G(E₁) at kVp = E₁ (e.g., 50 kV)
    Record G(E₂) at kVp = E₂ (e.g., 90 kV)

Clinical use (interpolation):
    G(θ_current) = G(θ₁) + (θ_current - θ₁)/(θ₂ - θ₁) × [G(θ₂) - G(θ₁)]
    G(E_current) = linear or polynomial interpolation between G(E₁), G(E₂)
    
2D interpolation (angle + energy):
    G(θ,E) = bilinear interpolation from {G(θ₁,E₁), G(θ₁,E₂), G(θ₂,E₁), G(θ₂,E₂)}
```

### 4.4 Real-Time Gain Correction (CN109709597B — Chinese Patent)

**Patent:** CN109709597B — *Gain Correction Method for Flat Panel Detector*, filed 2018-11-13  
**URL:** https://patents.google.com/patent/CN109709597B/en

Enables real-time gain correction with minimal exposure data (<5 images, even 1 image):

```
1. Power on, heat engine completes (warm-up)
2. Collect 1–3 empty-field bright-field images L at selected dose point
3. Generate bias correction template from L and original dark field D
4. Divide area-of-interest regions from bias correction template  
5. Filter each region of interest (regional normalization)
6. Generate gain correction template
7. Apply gain correction template to subsequent patient images
```

### 4.5 Practical Gain Calibration Considerations (AAPM)

From AAPM TG-150 (https://www.aapm.org/pubs/reports/TG-150_final.pdf):

```
Carestream DRX-1 system calibration at 80 kVp:
    mAs stations: ~9 mAs (standard), ~18 mAs (high), 2.8 mAs (mid), ~0.71 mAs (low)
    SID: 182 cm
    Apply 20 mm aluminum filter for standard beam quality
    
GE FlashPad system calibration:
    20 mm aluminum filter at 80 kVp
    SID varies: 180 cm (wall stand), 100 cm (table Bucky), 127 cm (cross-table)
    Four flat-field images at four mAs stations
```

---

## 5. Lag Correction and Multi-Frame Averaging

### 5.1 Physics of Detector Lag

Detector lag (residual signal / afterimage) in a-Si FPD is caused by:
1. **Charge trapping in a-Si photodiode** — dominant mechanism; traps fill during illumination and release with long time constants (milliseconds to minutes)
2. **Scintillator persistence** — afterglow in CsI:Tl or GOS scintillator (secondary mechanism)

**Magnitude:** 1st frame lag: 2–8% of original signal; decays over 10–50+ frames

**Clinical impact:** 
- Fluoroscopy: ghosting artifacts, temporal blurring
- CBCT: "radar artifact" (shading artifacts in reconstruction from varying trap history)

### 5.2 Linear Time-Invariant (LTI) Lag Model

**Paper:** Jared Starman et al., "A nonlinear lag correction algorithm for a-Si flat-panel x-ray detectors," *Medical Physics*, 2012  
**URL:** https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/

**Multi-exponential impulse response function (IRF):**

```
h(k) = b₀δ(k) + Σₙ bₙ e^(-aₙk)    [n = 1..N, typically N=4]

Where:
    k  = frame number (discrete time)
    b₀ = fraction of signal unaffected by lag
    bₙ = lag coefficient for nth exponential term  [Σbₙ = 1-b₀]
    aₙ = lag rate for nth term [frames⁻¹]
    δ(k) = impulse function
```

**Detector output model:**
```
y(k) = x(k) * h(k)     [* = convolution]
y(k) = Σₙ bₙ × Σⱼ x(j) × e^(-aₙ(k-j))    [sum over past frames j]
```

**Recursive deconvolution (real-time correction):**
```
State variable update:
    Sₙ,ₖ₊₁ = xₖ + Sₙ,ₖ × e^(-aₙ)

Lag-corrected signal:
    xₖ = y(k) - Σₙ bₙ × Sₙ,ₖ × e^(-aₙ) / Σₙ bₙ

Estimated stored charge:
    qₙ,ₖ = Sₙ,ₖ × bₙ × e^(-aₙ) / (1 - e^(-aₙ))
```

**Calibration procedure for Varian 4030CB (15 fps, dynamic gain):**
1. Acquire falling step-response (FSRF) at mid-exposure (~27% saturation); fit N=4 multiexponential → determine lag rates aₙ
2. Acquire FSRF at 9 exposure levels (2%–92% saturation); compute max stored charge Qₙ(x) per exponential term
3. Global search on rising step-response (RSRF) to determine exposure-dependent rates a₂,ₙ(x)

### 5.3 Nonlinear Consistent Stored Charge (NLCSC) Algorithm — Varian

**From same paper (Starman et al., 2012):**

NLCSC extends LTI to handle exposure-dependent IRF while conserving stored charge estimate:

```
Exposure-dependent IRF:
    h(k, xₖ) = b₀(xₖ)δ(k) + Σₙ bₙ(xₖ) e^(-aₙ(xₖ)k)
    aₙ(x) = a₁,ₙ + a₂,ₙ(x)

Stored charge conservation constraint:
    Sₙ,ₖ* = qₙ,ₖ × (1 - e^(-aₙ(xₖ))) / [bₙ(xₖ) × e^(-aₙ(xₖ))]

Corrected signal update:
    xₖ = y(k) - Σₙ bₙ(xₖ) × Sₙ,ₖ* × e^(-aₙ(xₖ)) / Σₙ bₙ(xₖ)
    Sₙ,ₖ₊₁ = xₖ + Sₙ,ₖ* × e^(-aₙ(xₖ))
```

**Performance comparison (1st frame / 50th frame residual lag):**

| Algorithm | 2% exposure | 27% exposure | 92% exposure |
|-----------|------------|--------------|--------------|
| LTI (calibrated at 27%) | 1.4% / 0.48% | 0.25% / 0.0038% | 0.005% / -0.16% |
| Intensity-weighted non-LTI | 0.33% / 0.10% | 0.30% / 0.051% | 0.15% / 0.003% |
| **NLCSC** | **0.25% / <0.001%** | **0.29% / 0.005%** | **0.16% / 0.003%** |

### 5.4 Direct Lag Charge Measurement Method (US7792251B2)

**Patent:** US7792251B2 — *Method for the Correction of Lag Charge in a Flat-Panel X-ray Detector*  
**Assignee:** (Siemens/GE), filed 2008-06-03  
**URL:** https://patents.google.com/patent/US7792251B2/en

Instead of a correction model, this patent directly measures lag charges before each integration:

```
Frame timing sequence:
    ┌─────────────────────────────────────────────────┐
    │ Initial Read Phase (lag measurement) │ N clock  │ Final Read Phase │
    │  (1 clock period, no X-rays)         │ periods  │  (raw image out) │
    │                                      │ (integ.) │                  │
    └─────────────────────────────────────────────────┘
    
N > 2 clock periods between integrations (ensures lag measurement without X-rays)
```

**Correction formula:**
```
Corrected_image = [Raw_image - Offset(N)] - (N+1) × [Lag_image - Offset(0)]

Where:
    N        = integration time in clock periods
    Lag_image = charges measured at initial read phase
    Offset(0) = short read-phase offset (no X-rays, duration << 1 clock period)
    Offset(N) = offset at integration time N
    (N+1)    = scaling factor for pixel-level integration during read
```

**Noise reduction:** Average multiple initial read phases before integration:
```
Lag_avg = (1/K) × Σᵢ Lag_imageᵢ    [K available read phases before integration]
```

### 5.5 Hardware Lag Reduction Methods

**Method 1 — Forward Bias (FB) — Varian Research**

**Paper:** Starman et al., "A forward bias method for lag correction of an a-Si flat panel detector," *Medical Physics*, 2011  
**URL:** https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/

```
FB Protocol (modified Varian 4030CB):
    Between readout and X-ray exposure:
    1. Apply +4V forward bias across photodiodes (groups of 8 rows)
    2. FB current: 20 pC/photodiode, 100 kHz switching rate
    3. Fills traps uniformly → subsequent lag is uniform offset
    4. Reset to standard reverse bias before integration
    
Timing: ~30 ms per frame (limits X-ray window to 18 ms at 15 fps, 32 ms at 10 fps)
Lag reduction: 93–95% reduction in first-frame lag
Ghost contrast: 88% reduction (frame 2), 70% reduction (frame 100)
CBCT radar artifact: 81% reduction (pelvis), 48% (head)
```

**Method 2 — Empty Frame Flushing (flush-N)**
```
Between acquired frames, insert N empty frames (X-ray off):
    → Lag decays exponentially during empty frames
    → Effective IRF: h_flush-1(k) = b₀δ(k) + Σ bₘ e^(-2aₘk)  [even-sample resampling]
    → Residual lag ~50% of standard; frame rate halved for N=1
```

**Method 3 — LED Illumination**
```
Illuminate a-Si between readout and X-ray with LEDs:
    → Saturates traps uniformly (constant lag offset)
    → First frame residual remains >2% at 250 µs light duration
    → Scintillator lag unaffected
```

**Performance comparison:**

| Method | 1st Frame Lag Reduction | Drawback |
|--------|------------------------|----------|
| Flush-1 (empty frames) | ~50% | Halved frame rate |
| LED illumination | Variable, <1% residual | Scintillator lag unaffected |
| Forward bias | 93–95% | Limits X-ray window; complex firmware |
| NLCSC software | >80% (uniform exposure) | Residual at varying exposure |

---

## 6. Preconditioning / Prep Pulse Sequences

### 6.1 Prep Request / X-ray Enable Handshake

From US20130126742A1 — *X-ray Radiation Detector with Automatic Exposure Control*  
**URL:** https://patents.google.com/patent/US20130126742A1/en

The standard X-ray acquisition protocol involves bidirectional signaling between the generator and FPD:

```
Acquisition Sequence:
┌─────────────────────────────────────────────────────────────────┐
│ 1. Operator presses exposure button/pedal                       │
│ 2. Generator initiates preparation phase (kV ramp up, warm up) │
│ 3. Generator sends PREP REQUEST pulse to FPD                   │
│ 4. FPD:                                                        │
│    a. Transitions to High power state                          │
│    b. Executes refresh cycle(s)                                │
│    c. Executes prepare cycle                                   │
│    d. Stabilization time: 500ms–2s                            │
│ 5. FPD sends X-RAY ENABLE signal to generator                 │
│ 6. Generator turns on X-ray beam                              │
│ 7. FPD starts integration period                              │
│ 8. X-ray exposure completes                                   │
│ 9. FPD reads out image                                        │
│ 10. FPD transmits image to host computer                      │
└─────────────────────────────────────────────────────────────────┘
```

**Prep time range:** 2–15 seconds in Carestream characterization (EP2148500A1)

### 6.2 Dummy Frame Acquisition for Stabilization

From EP2148500A1 (Carestream) and AAPM lecture materials:

```
Protocol for maximum repeatability:
    1. PREP command received → transition to High state
    2. Execute 1–3 refresh cycles (backlight or bias cycling)
    3. Execute 1 prepare cycle (biases ready for integration)
    4. Optionally: acquire 1–3 dummy integration frames (discarded)
       - Dummy frames stabilize pixel bias history
       - Each dummy frame: full gate scan + readout (no X-ray)
    5. After stabilization: start real integration
    6. Acquire exposure image
    7. Immediately acquire 1–2 post-dark frames (same timing)
```

**Why dummy frames are needed:** After power state transition, pixels may have varying initial charge states that take 1–3 frames to equilibrate to steady-state dark current levels. Discarding these initial frames ensures the actual exposure image starts from a known, reproducible state.

### 6.3 Fluoroscopy Preparation Sequence

In fluoroscopy, the panel must be pre-conditioned before the first clinical frame:

```
Pre-fluoroscopy initialization (from AAPM IRGT Fluoroscopic Imaging guide):
    1. Continuous beam mode: beam ON, FPD acquiring at full frame rate
    2. Pulsed mode: beam pulsed at target frame rate, FPD synchronized
    
Frame settling:
    First 3–10 frames after beam-on: discarded (non-steady offset)
    Clinical frames start after detector has reached steady-state
```

**Reference:** Murphy, K., "Fluoroscopic Imaging for IRGT," AAPM 2006 (https://www.aapm.org/meetings/06ss/documents/Murphyfluoro.pdf)

---

## 7. Fluoroscopy Continuous Mode

### 7.1 Acquisition Modes

From AAPM fluoroscopy documentation and Siemens Hoheisel paper:

```
Four possible fluoroscopy configurations:
    1. Continuous beam + continuous readout   (most common)
    2. Continuous beam + pulsed readout
    3. Pulsed beam + continuous readout       (frame-rate control)
    4. Pulsed beam + pulsed readout           (synchronized pulsed fluoro)
```

### 7.2 Frame Rate vs. Pixel Count Trade-offs

The readout time for an entire frame limits achievable frame rate:
```
Frame period = (N_rows × t_row) + t_overhead

t_row = charge-amplifier settling + ADC conversion time per row
      ≈ 10–100 µs per row (depends on ADC speed and array size)

For 1024 rows × 50 µs/row = 51.2 ms → max frame rate ≈ 19 fps
For 1024 rows × 25 µs/row = 25.6 ms → max frame rate ≈ 39 fps
For 512 rows × 25 µs/row = 12.8 ms → max frame rate ≈ 78 fps
```

From Siemens (Hoheisel 1996):
- **12.5 fps** at 1024×1024 full resolution
- **25 fps** at 512×512 binned mode (2×2 pixel binning)

From Varian 4030CB (Starman 2012):
- **15 fps** standard fluoroscopy / CBCT mode (dynamic gain)

From dual-layer FPD (Shi et al., Medical Physics 2020):
- **15 fps** with 2×2 binning (300 µm effective pitch, 43×43 cm² field)

### 7.3 Rolling Reset (Line-by-Line Readout Overlap)

In continuous fluoroscopy, the gate scan can be configured for **rolling reset**:

```
Standard (non-overlapping):
    ────[Gate scan all N rows]────[Integration window]────[Gate scan all N rows]───

Rolling reset (overlapping):
    Row 1 gate: ──[Reset]──[Integrate]──[Readout]──────────────
    Row 2 gate:    ──[Reset]──[Integrate]──[Readout]────────────
    Row 3 gate:       ──[Reset]──[Integrate]──[Readout]──────────
    ...
    → Different rows integrating at slightly different times
    → Creates motion artifacts for moving objects
    → But allows higher effective frame rate
```

**FPGA timing note:** Rolling reset requires precise timing of gate-line enable pulses with constant time offset between consecutive rows. The FPGA generates a shift-register-like pattern with programmable inter-row delay (typically equal to: total_frame_time / N_rows).

### 7.4 CN107874770B — Frame Rate Adjustment Method (China Patent)

**Patent:** CN107874770B — *Frame Rate Adjustment Method and Device for Fluoroscopic Device*  
**URL:** https://patents.google.com/patent/CN107874770B/en

Method for dynamic frame rate adjustment in fluoroscopy:
```
1. Acquire current fluoroscopic frame
2. Measure dose rate at detector surface
3. Compare to target dose range
4. If dose high: reduce frame rate (longer integration, discard frames)
5. If dose low: increase frame rate (shorter integration)
6. Update FPD timing register with new frame period
7. Update generator exposure parameters accordingly
```

---

## 8. Static Radiography Mode

### 8.1 Key Differences from Fluoroscopy

| Parameter | Fluoroscopy | Radiography |
|-----------|------------|-------------|
| Dose per frame | 10–100 nGy | ~1 µGy (10–100× higher) |
| Frame rate | 7.5–30 fps | Single frame |
| Reset before exposure | Rolling or continuous | **Full panel reset required** |
| Readout overlap | May overlap integration | No overlap; sequential |
| Post-readout settling | Short (next frame follows) | Longer (wait for lag decay) |

### 8.2 Radiography Acquisition Sequence

```
Single-Frame Radiography Protocol:
    1. PREP signal received from generator
    2. FPD transitions High power state
    3. Execute refresh cycle(s) (fill traps uniformly)
    4. Execute complete gate scan (reset all rows to known state)
    5. Start integration period (all gate lines OFF → TFTs blocking)
    6. Generator sends X-ray pulse (monitored by FPD sync input)
    7. Integration ends (controlled by FPD firmware or AEC signal)
    8. Execute complete gate scan (readout all rows sequentially)
    9. ADC conversion and digital output to FPGA
    10. Acquire post-dark frame(s) for offset correction
    11. Apply offset + gain + defect corrections in FPGA
    12. Transmit corrected image to host
    13. Transition to Medium or Low power state
```

### 8.3 AEC Integration with FPD (US20130126742A1)

**Patent:** US20130126742A1  
**URL:** https://patents.google.com/patent/US20130126742A1/en

Secondary posterior array for integrated AEC:
```
AEC hardware configuration:
    Primary array: CsI + CMOS APS (clinical image)
    Secondary array: posterior CsI + photodiode array (AEC sensing)
    
AEC state machine logic:
    1. Secondary array starts sampling when X-rays detected
    2. Integrate dose signal in real time
    3. When accumulated dose ≥ threshold:
       a. Trigger generator STOP signal
       b. Signal primary array END INTEGRATION
    4. Primary acquisition time = variable (AEC-controlled)
    
FPD firmware:
    FPD acquisition time always SET LONGER than expected X-ray pulse
    Each acquisition pulse in train is synchronized to corresponding X-ray pulse
    Each acquisition pulse is longer than the X-ray pulse
```

---

## 9. Triggered Acquisition and Sync

### 9.1 FPGA Trigger Architecture

From US20130126742A1 and AAPM lecture materials:

```
FPGA Trigger Logic Block Diagram:

    X-Ray Generator
         │
         ├─── PREP_REQUEST ───────────────────────────────→ FPD FPGA
         │                                                      │
         ├─── X_RAY_ON signal ──────────────────────────→ Sync Input
         │                                                      │
         ←─── X_RAY_ENABLE ─────────────────────────────── FPD FPGA
         │
         ←─── X_RAY_STOP (from AEC) ─────────────────── AEC Circuit
                                                               │
    FPD FPGA                                                   │
         │                                                     │
         ├─── Gate Driver Control ─────────────────────→ Gate ICs
         ├─── Timing Register ─────────────────────────→ Frame timing
         ├─── Integration Window ──────────────────────→ TFT gate control
         └─── ADC Control ──────────────────────────────→ AFE/ADC chips
```

**Trigger modes:**

| Mode | Description |
|------|-------------|
| **AED (Auto-Exposure Detection)** | FPD detects X-ray onset from pixel signal rise; self-triggered |
| **Generator sync** | Generator sends TTL/LVDS signal; FPGA starts integration |
| **Software trigger** | Host computer sends command; FPGA initiates sequence |
| **AEC stop** | AEC circuit sends stop signal; FPGA terminates integration |

### 9.2 Detector Framing Node Patent (GE — JP2003010163A)

**Patent:** JP2003010163A — *Imaging System Including Detector Framing Node*, GE Healthcare (Japan application)  
**URL:** https://patents.google.com/patent/JP2003010163A/en

Programmable **Detector Framing Node (DFN)** — dedicated real-time controller:

```
DFN Architecture:
    - Controls X-ray generation (via generator interface)
    - Controls X-ray detection (via FPD interface)
    - Executes event instruction sequences in real-time
    - Sends acquired image data to host memory via communication bus
    - Works independently of host computer OS (real-time guaranteed)
    
DFN Capabilities:
    - Controls timing of Begin Study, Prep, Expose, and power switching
    - Receives image data from multiple different flat panel detectors
    - Selectively rearranges data based on FPD parameters before host transfer
    - Can be programmed from host computer via control registers
```

### 9.3 X-ray Exposure Timing Window Management

**FPGA exposure window algorithm:**

```
Pre-exposure:
    T_prep_min = time for refresh + prepare + stabilization
    T_generator_prep = generator spin-up time (kV ramp)
    T_sync_margin = safety margin (typical: 10–50 ms)
    
    FPGA gate OPEN time = max(T_prep_min, T_generator_prep) + T_sync_margin

Integration period:
    Mode A (fixed time): T_integration = preset value (e.g., 100 ms)
    Mode B (AEC stop):   T_integration = variable; stop on AEC threshold
    Mode C (generator sync): T_integration = X_RAY_ON to X_RAY_OFF duration
    
Integration window margin:
    T_window = T_X_ray_pulse + T_setup + T_hold
    (T_window > T_X_ray_pulse to guarantee capturing full exposure)

Post-integration:
    T_settle = time for charges to redistribute before readout
    (typically: 1–10 ms; depends on TFT characteristics)
    T_readout = N_rows × t_row_period
```

---

## 10. Frame Rate Control

### 10.1 Variable Frame Rate Architecture

FPGA-based variable frame rate control:

```
Frame Period Register:
    T_frame [register] = programmable 16/32-bit value
    T_frame = T_integration + T_readout + T_settle + T_overhead
    
Minimum frame period:
    T_frame_min = T_readout + T_settle_min
    (limited by gate scan speed and ADC conversion)
    
Maximum frame period:
    T_frame_max = limited by dark current accumulation / offset drift
    (typically < 10 seconds between frames)

Variable rate update:
    Host command → FPGA register write → new T_frame at next frame start
    Or: AEC feedback → dose controller → FPGA frame rate register
```

### 10.2 Dose-Rate Adaptive Frame Rate

From AAPM IRGT documentation:

```
Pulsed fluoroscopy dose rate control:
    Available duty cycles: 2, 5, 10, 15, 30 frames per second
    Lower frame rates used for longer procedure fractions
    
Algorithm:
    1. Measure signal level in current frame
    2. If signal_level < target_low: increase frame rate or dose
    3. If signal_level > target_high: decrease frame rate or dose
    4. Hysteresis band to prevent rapid switching
```

**Reference:** Shirato, IJROBP 60, 2004 (AAPM IRGT documentation)

---

## 11. Power-On Initialization Sequence

### 11.1 Complete Power-On Sequence

Based on EP2148500A1, AAPM practice, and AAPM lecture (Quantum Detector):

```
Power-On Initialization Sequence:

[Phase 0: Hardware Power Sequencing]
    t=0ms:    Apply main power (+3.3V, +5V, +12V, -12V to PCBs)
    t=10ms:   Voltage rails stabilized; verify with ADC monitoring
    t=20ms:   FPGA powers on, loads bitstream from Flash/ROM
    t=100ms:  FPGA firmware initialized; all I/O configured

[Phase 1: Gate Driver IC Initialization]
    t=100ms:  Apply VGL (gate OFF voltage, e.g., -10V) to all gate lines
    t=110ms:  Verify VGL levels on test nodes
    t=120ms:  Apply VGH (gate ON voltage, e.g., +20V) to gate driver ICs
    t=130ms:  Initialize gate driver shift registers (load start pulse)
    t=150ms:  Execute first full gate scan (dummy, all rows, no data read)
    
[Phase 2: Analog Front End (AFE) / Charge Amplifier Initialization]
    t=200ms:  Power on charge-sensitive amplifier array (CSA)
    t=210ms:  Set CSA integration capacitor (Cint) value via SPI/I2C
    t=220ms:  Apply pixel bias voltage (V_pixel_bias, e.g., -5V)
    t=230ms:  Verify bias currents on each readout channel
    t=250ms:  Power on column-readout multiplexer
    t=260ms:  Apply sample-and-hold timing to CSA array
    
[Phase 3: ADC Initialization]
    t=300ms:  Power on ADC array (16-bit, typically 1–4 ADCs)
    t=310ms:  Send ADC configuration (gain, offset, reference)
    t=320ms:  ADC self-calibration (if supported)
    t=340ms:  Verify ADC output with test pattern

[Phase 4: Panel Thermal Stabilization]
    t=350ms:  Read temperature sensors (NTC thermistors on array PCB)
    t=360ms:  Check temperature within operational range (15–35°C)
    t=400ms:  Begin monitoring temperature; log for metadata

[Phase 5: First Refresh Cycle]
    t=500ms:  Execute first optical refresh (backlight flash) or bias cycling
    t=550ms:  Verify pixel reset by reading dummy frames
    t=600ms:  Execute second refresh if required (first frame check)
    
[Phase 6: Offset Map Load and Validation]
    t=700ms:  Load factory offset map from non-volatile storage (Flash)
    t=710ms:  Load factory gain map from non-volatile storage
    t=720ms:  Load defect pixel map from non-volatile storage
    t=730ms:  Validate map checksums
    t=750ms:  Load temperature-indexed maps if applicable

[Phase 7: Dark Frame Acquisition for Initial Offset Update]
    t=800ms:  Execute prepare cycle
    t=850ms:  Acquire N_dark = 4–8 dark frames (no X-ray)
    t=1200ms: Average dark frames → initial offset map update
    t=1250ms: Store updated offset in working memory

[Phase 8: Communication Interface Ready]
    t=1300ms: Ethernet/USB/fiber-optic interface initialized
    t=1350ms: Send "READY" status to host computer
    t=1500ms: Panel in Low power state, ready for first study
    
Total initialization time: ~1.5 seconds (typical)
```

### 11.2 Gate Driver Initialization Order (a-Si TFT Array)

From "Review of Integrated Gate Driver Circuits in Active Matrix Thin-Film Transistors," *Micromachines*, 2024  
**URL:** https://pmc.ncbi.nlm.nih.gov/articles/PMC11279033/

Gate driver initialization sequence:
```
N-type a-Si TFT shift register operation:
    Three phases per stage: Pre-charging → Bootstrapping → Pulling-down

Initialization clock sequence:
    1. Apply CLK1, CLK2 (two-phase, out-of-phase clock signals)
    2. Apply start pulse GSP (gate start pulse) at first stage
    3. GSP propagates through shift register; each stage generates scan pulse
    4. Scan pulse duration = one CLK period
    5. All N rows scanned in N clock periods

VGL (gate-off voltage): Keep all unselected rows OFF (TFTs blocking)
VGH (gate-on voltage): Enable selected row (TFT ON → readout)

Full initialization scan:
    Purpose: Clear all rows to known OFF state
    Sequence: Apply VGH sequentially to each row for t_row_ON
    After full scan: All rows back to VGL
```

### 11.3 AFE Calibration During Initialization

```
Charge amplifier (AFE) initialization and self-calibration:

1. Set integration capacitor Cint (determines gain range):
   - High Cint (e.g., 4 pF): Low gain, high dynamic range (radiography)
   - Low Cint (e.g., 0.5 pF): High gain, low noise (fluoroscopy)

2. Correlated double sampling (CDS) calibration:
   - Sample channel noise floor during reset
   - Sample signal after integration
   - Difference = true pixel signal (eliminates kTC noise)

3. Channel-to-channel gain matching:
   - Inject known test charge via feedback capacitor
   - Measure output ADC code per channel
   - Compute per-channel gain correction coefficients
   - Store in FPGA correction LUT

4. Offset nulling:
   - Read output with zero input signal
   - Adjust DAC offset per channel to bring within ADC mid-range
```

---

## 12. Standards and Regulatory References

### 12.1 IEC Standards

| Standard | Title | Relevance |
|----------|-------|-----------|
| **IEC 62220-1** (2003) | Medical Electrical Equipment – Characteristics of Digital X-ray Imaging Devices – Part 1: DQE | Calibration procedure requirements, beam quality specifications |
| **IEC 62220-1-2** | Part 2: Determination of the DQE for mammography | Mammography-specific protocols |
| **IEC 62220-1-3** | Part 3: Determination of the DQE for CT | CT-specific |
| **IEC 61267** | Medical diagnostic X-ray equipment – Radiation conditions for characteristics | Beam quality specification (RQA5, RQA9 spectra) |

**IEC 62220-1 calibration requirement:**
> "The calibration of the digital X-ray detector shall be carried out prior to any testing, i.e., all operations necessary for corrections according to Clause 5 shall be effected. No re-calibration of the digital X-ray detector shall be allowed between any measurement of the series."

### 12.2 AAPM Reports

| Report | Title | Key Content |
|--------|-------|-------------|
| **TG-150** | Acceptance Testing and QC of Digital Radiographic Imaging Systems | Calibration procedures, flat-field acquisition protocols |
| **TG-116** | Exposure Indicator for Digital Radiography | Standard beam conditions, sensitivity calibration |

**AAPM TG-150 calibration conditions (Carestream DRX-1):**
```
- 80 kVp, four mAs stations: ~9, ~18, 2.8, ~0.71 mAs
- SID: 182 cm
- Lead apron on floor (exclude backscatter)
- Run automatic calibration procedure
- Covers: offset correction, gain correction, defect correction
```

---

## 13. Patent Index

### Priority Patents for FPD Driving Sequences

| Patent Number | Title | Assignee | Filed | Key Technology |
|---------------|-------|----------|-------|----------------|
| **US5452338A** | Method and System for Real Time Offset Correction in a Large Area Solid State X-ray Detector | GE (General Electric) | 1994-09-19 | Recursive filter for continuous offset update; a_i = (1-1/n)a_{i-1} + (1/n)p_i |
| **EP2148500A1** | Dark Correction for Digital X-ray Detector | Carestream Health | 2009-07-20 | Full refresh/prepare cycle; power state machine; metadata-indexed offset maps; embedded controller timing replication |
| **US7792251B2** | Method for the Correction of Lag Charge in a Flat-Panel X-ray Detector | (Siemens/GE) | 2008-06-03 | Direct lag charge measurement; initial/final read phase timing; real-time lag correction formula |
| **US20130126742A1** | X-ray Radiation Detector with Automatic Exposure Control | (Edge Medical / independent) | 2013-05-23 | Prep request / X-ray enable handshake; integrated AEC with FPD; triggered acquisition state machine |
| **US20050092909A1** | Method of Calibrating a Digital X-ray Detector | Siemens AG | 2004-09-21 | Smoothing filter applied to gain calibration image; 1D and 2D filter variants |
| **US7404673B2** | Method for Generating a Gain-Corrected X-ray Image | Siemens AG | 2006-04-13 | Multi-angle, multi-energy gain maps with interpolation |
| **US7963697B2** | Gain Calibration and Correction Technique for Digital Imaging Systems | GE Healthcare | 2009-01-20 | Using dark images with additive channel input for gain calibration; per-channel correction maps |
| **US7402812B2** | Method for Gain Calibration of an X-ray Imaging System | Siemens | 2006-11-03 | Gain calibration with two reset light intensities; pixel brightness threshold for correction feasibility |
| **CN109709597B** | Gain Correction Method for Flat Panel Detector | (Chinese manufacturer) | 2018-11-13 | Real-time gain correction from 1–3 bright-field images; bias correction template; area-of-interest filtering |
| **JP2003010163A** | Imaging System Including Detector Framing Node | GE Medical Systems | 2002-01-08 | Programmable detector framing node (DFN); real-time event sequencing independent of host OS |
| **CN107874770B** | Frame Rate Adjustment Method and Device for Fluoroscopic Device | (Chinese) | (N/A) | Dynamic fluoroscopy frame rate adjustment based on dose rate |
| **US2003/0223539** | Method and Apparatus for Acquiring and Storing Multiple Offset Corrections for Amorphous Silicon Flat Panel Detector | GE Healthcare (Granfors et al.) | 2003 | Periodic weighted offset map update; referenced in EP2148500A1 |
| **US5751783A** | Detector for Automatic Exposure Control on an X-ray Imaging System | GE | 1996-12-20 | AEC detector using partial transmission through a-Si array; AEC field of view selection |

### Additional Relevant Patents (Referenced in Literature)

| Patent Number | Title | Notes |
|---------------|-------|-------|
| **US7113565B2** | Radiological Imaging Apparatus and Method (Tadeo Endo) | Multiple gain-ranging readout for dynamic range extension |
| **US7208717** | Method and Apparatus for Correcting Excess Signals in an Imaging System | Lag correction proportional to exposure; two dark frames at known intervals |
| **WO1998003884A1** | X-ray Imaging Apparatus and Method Using a Flat Amorphous Silicon Imaging Panel | Early a-Si FPD with refresh protocol |
| **US7200201B2** | Flat Panel Detector Based Slot Scanning Configuration | Slot scan mode; sequential area acquisition |
| **US20030191387A1** | Method and Apparatus for Correcting the Offset Induced by FET Photo-Conductive Effects | FET-induced offset in solid-state X-ray detectors |

---

## 14. Academic Paper Index

### Lag Correction

| Paper | Authors | Journal | Year | Key Contribution |
|-------|---------|---------|------|-----------------|
| "A nonlinear lag correction algorithm for a-Si flat-panel x-ray detectors" | Starman, Star-Lack, Virshup, Shapiro, Fahrig | *Medical Physics* | 2012 | NLCSC algorithm; N=4 multiexponential; Varian 4030CB at 15 fps; residual lag <0.29% | [URL](https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/) |
| "A forward bias method for lag correction of an a-Si flat panel detector" | Starman, Tognina, Partain, Fahrig | *Medical Physics* | 2011 | Forward bias hardware method; 93–95% lag reduction; hybrid FB+software mode | [URL](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/) |
| "Lag correction model and ghosting analysis for an indirect-conversion FPI" | (Multiple) | *J. Appl. Clin. Med. Phys.* | 2007 | MV-CBCT lag model; 30 projections at 3.5 fps; weighted subtraction of 10 previous frames | [URL](https://pmc.ncbi.nlm.nih.gov/articles/PMC5722609/) |
| "Nonlinear Lag Correction Based on the AR(1) Model for FPDs" | (Multiple) | *IEEE Transactions* | 2023 | Autoregressive model for lag correction; AR(1) model | [URL](https://ieeexplore.ieee.org/iel7/6287639/10005208/10105275.pdf) |
| "Investigation of the Lag Effect in X-Ray Flat-Panel Detector" | (Multiple) | *Radiology* | N/A | Lag effect up to 3.6% error in flat-field signal averaging | [URL](https://go.gale.com/ps/i.do?id=GALE%7CA680677737) |
| "Clinical introduction of image lag correction for CBCT" | (Multiple) | *Medical Physics* | 2016 | Clinical implementation; calibration requirements | [URL](https://aapm.onlinelibrary.wiley.com/doi/10.1118/1.4941015) |

### Detector Characterization and Calibration

| Paper | Authors | Journal | Year | Key Contribution |
|-------|---------|---------|------|-----------------|
| "The long-term stability of amorphous silicon flat panel imaging detectors" | (Multiple) | *Medical Physics* | 2004 | Temperature effects on dark field; dynamic dark-field correction; 0.5%/year degradation | [URL](https://pubmed.ncbi.nlm.nih.gov/15587651/) |
| "Calibration model of a dual gain flat panel detector for 2D and 3D X-ray imaging" | (Multiple) | (SPIE/PMB) | 2007 | Pixel sensitivity dependencies; calibration procedure for dual-gain mode | [URL](https://pubmed.ncbi.nlm.nih.gov/17926969/) |
| "An in-house protocol for improved flood field calibration" | (Multiple) | *J. Appl. Clin. Med. Phys.* | 2017 | Flood field calibration with uniform attenuation filter; saturation artifact correction | [URL](https://pmc.ncbi.nlm.nih.gov/articles/PMC5689880/) |
| "Gain and offset calibration reduces variation in exposure-dependent SNR" | (Multiple) | *Medical Physics* | 2011 | Calibration reduces inter-system variation; standardized procedure | [URL](https://aapm.onlinelibrary.wiley.com/doi/10.1118/1.3602458) |

### FPD Architecture and Operation

| Paper | Authors | Journal | Year | Key Contribution |
|-------|---------|---------|------|-----------------|
| "Amorphous Silicon X-Ray Detectors" (Siemens) | Hoheisel, Spahn, Spinnler, Vieux | ISCMP Proc. | 1996 | Double-diode architecture; optical reset flash; fluoroscopy (12.5/25 fps) vs. radiography modes; VP1/VP2 driving voltages | [URL](https://www.mhoheisel.de/docs/ISCMP91996112.pdf) |
| "Large area X-ray detectors based on amorphous silicon technology" | (Multiple) | *Thin Solid Films* | 1998 | Early a-Si FPD comprehensive review | [URL](https://www.sciencedirect.com/science/article/abs/pii/S0040609098011791) |
| "Flat-panel x-ray detector using amorphous silicon" | (Multiple) | *Radiology* | 1997 | Clinical evaluation; speed 400 equivalent; dose reduction vs. screen-film | [URL](https://pubmed.ncbi.nlm.nih.gov/9228601/) |
| "Flat-panel detectors: how much better are they?" | (Multiple) | *Pediatric Radiology* | 2006 | Fluoroscopy FPD vs. XRII/TV comparison; pulsed fluoroscopy lower frame rate modes | [URL](https://pmc.ncbi.nlm.nih.gov/articles/PMC2663651/) |
| "Review of Integrated Gate Driver Circuits in Active Matrix TFT Arrays" | (Multiple) | *Micromachines* | 2024 | Gate driver shift register operation; CLK1/CLK2 two-phase clocking; Pre-charge/Bootstrap/Pull-down phases | [URL](https://pmc.ncbi.nlm.nih.gov/articles/PMC11279033/) |
| "Characterization of Flexible Amorphous Silicon TFT-Based Detectors" (VIEWORKS) | Han, Kim, Park, Lee | *Diagnostics* | 2022 | Flexible FPD; 99 µm pixel, 4316×4316 pixel; a-Si TFT characteristics | [URL](https://pmc.ncbi.nlm.nih.gov/articles/PMC9497934/) |

### Dual-Layer and Advanced Detectors

| Paper | Authors | Journal | Year | Key Contribution |
|-------|---------|---------|------|-----------------|
| "Characterization and Potential Applications of a Dual-Layer Flat-Panel Detector" | Shi et al. | *Medical Physics* | 2020 | Dual-layer Varex XRD 4343RF; 15 fps at 2×2 binning; DE radiography/fluoroscopy/CBCT | [URL](https://pmc.ncbi.nlm.nih.gov/articles/PMC7429359/) |

---

## 15. FPGA Implementation Insights

### 15.1 FPGA Architecture for FPD Control

```
FPGA Block Partitioning (conceptual):

┌────────────────────────────────────────────────────────────────┐
│                          FPD FPGA                              │
│                                                                │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────┐  │
│  │ Gate Driver  │  │  AFE/ADC     │  │  Timing &          │  │
│  │ Controller   │  │  Interface   │  │  Synchronization   │  │
│  │              │  │              │  │                    │  │
│  │ - Shift reg. │  │ - CDS timing │  │ - T_frame register │  │
│  │ - VGH/VGL    │  │ - Cint ctrl  │  │ - T_int register   │  │
│  │   timing     │  │ - SPI/I2C    │  │ - Trigger detect   │  │
│  │ - Row select │  │ - Per-ch.    │  │ - State machine    │  │
│  │ - Refresh    │  │   offset DAC │  │ - Generator sync   │  │
│  └──────────────┘  └──────────────┘  └────────────────────┘  │
│                                                                │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────┐  │
│  │ Image        │  │ Correction   │  │ Communication      │  │
│  │ Memory       │  │ Engine       │  │ Interface          │  │
│  │ Controller   │  │              │  │                    │  │
│  │              │  │ - Offset sub │  │ - Gigabit Ethernet │  │
│  │ - Line buf.  │  │ - Gain mult  │  │ - USB3.0           │  │
│  │ - Frame buf. │  │ - Defect     │  │ - Fiber optic      │  │
│  │ - DMA to     │  │   replace    │  │ - Host protocol    │  │
│  │   DDR3       │  │ - LUT-based  │  │                    │  │
│  └──────────────┘  └──────────────┘  └────────────────────┘  │
│                                                                │
│  ┌──────────────┐  ┌──────────────┐                          │
│  │ Power        │  │ Temperature  │                          │
│  │ Management   │  │ Monitor      │                          │
│  │              │  │              │                          │
│  │ - Low/Med/Hi │  │ - ADC input  │                          │
│  │   state ctrl │  │ - Metadata   │                          │
│  │ - Sequence   │  │   logging    │                          │
│  └──────────────┘  └──────────────┘                          │
└────────────────────────────────────────────────────────────────┘
```

### 15.2 Critical Timing Registers

| Register | Typical Range | Description |
|----------|--------------|-------------|
| `T_GATE_ON` | 10–100 µs | Duration of each gate line enable |
| `T_GATE_DELAY` | 1–10 µs | Dead time between consecutive row scans |
| `T_INTEGRATION` | 1 ms – 10 s | X-ray integration window |
| `T_FRAME_PERIOD` | 33–500 ms | Total frame period (1/frame_rate) |
| `T_REFRESH_PULSE` | 1–50 ms | Duration of optical refresh illumination |
| `T_PREPARE` | 100–500 µs | Prepare cycle duration |
| `T_POST_SETTLE` | 1–20 ms | Post-readout settling time |
| `N_REFRESH_CYCLES` | 1–5 | Number of refresh cycles per exposure |
| `N_DARK_FRAMES` | 1–8 | Number of dark frames to average |

### 15.3 State Machine Implementation

```verilog (conceptual pseudocode)
// FPD Acquisition State Machine

typedef enum {
    S_POWER_OFF,
    S_INIT,           // Power-on initialization
    S_LOW_POWER,      // Between studies
    S_MEDIUM,         // Study active, between exposures
    S_REFRESH,        // Executing refresh cycle
    S_PREPARE,        // Executing prepare cycle
    S_WAIT_TRIGGER,   // Waiting for X-ray trigger
    S_INTEGRATING,    // X-ray integration in progress
    S_READOUT,        // Gate scan readout
    S_DARK_CAPTURE,   // Post-exposure dark frame
    S_CORRECTION,     // Applying offset/gain correction
    S_TRANSFER        // Transmitting to host
} fpd_state_t;

// Key transitions:
// S_LOW_POWER → S_REFRESH: on PREP_REQUEST from generator
//                          (after N_REFRESH_CYCLES iterations)
// S_REFRESH → S_PREPARE: refresh complete
// S_PREPARE → S_WAIT_TRIGGER: prepare complete; send X_RAY_ENABLE
// S_WAIT_TRIGGER → S_INTEGRATING: X-ray start detected
// S_INTEGRATING → S_READOUT: X-ray end OR AEC threshold
// S_READOUT → S_DARK_CAPTURE: readout complete (if N_DARK > 0)
// S_DARK_CAPTURE → S_CORRECTION: dark frames acquired
// S_CORRECTION → S_TRANSFER: corrections applied
// S_TRANSFER → S_MEDIUM: host confirmed reception
```

### 15.4 Offset and Gain Correction Pipeline

```
Pipeline (4 stages, pipelined for one pixel per clock cycle):

Stage 1: Dark subtraction
    pixel_dc = pixel_raw - dark_map[row][col]
    
Stage 2: Gain normalization  
    pixel_gc = pixel_dc × (1 / gain_map[row][col])
    (implemented as multiply by pre-computed 1/G table)
    
Stage 3: Defect pixel replacement
    if defect_map[row][col]:
        pixel_corrected = weighted_average(neighbors)
    else:
        pixel_corrected = pixel_gc
        
Stage 4: Output clipping and format
    pixel_out = clamp(pixel_corrected, 0, 65535)  // 16-bit

Memory requirements:
    Dark map:   2 × N_rows × N_cols bytes (16-bit per pixel)
    Gain map:   2 × N_rows × N_cols bytes (16-bit, fixed-point)
    Defect map: 1 bit per pixel (packed)
    
For 3072×3072 panel:
    Dark map:  ~18 MB
    Gain map:  ~18 MB
    Defect:    ~1.2 MB
    Total:     ~37 MB → fits in DDR3 SDRAM
```

### 15.5 Lag Correction in FPGA

```
LTI Lag Correction (N=4 exponentials) in FPGA:

// Per pixel, per frame (pipelined):
// State variables: S[0..3][row][col] stored in DDR3

// Load S values for current pixel from DDR3
for n in 0..3:
    correction += b[n] × S[n][row][col] × exp_n_LUT[n]  
    // exp_n_LUT precomputed: e^(-a_n) for each n
    
// Compute lag-corrected pixel
x_corrected = y_raw - correction / sum_b_n

// Update state variables
for n in 0..3:
    S[n][row][col] = x_corrected + S[n][row][col] × exp_n_LUT[n]
    
// Write back to DDR3

Memory: 4 × 2 × N_rows × N_cols × 4 bytes = 4 state variables × float32
For 3072×3072 × N=4 × 4 bytes/float = 150 MB DDR3 bandwidth per frame
At 15 fps = 2.25 GB/s DDR3 bandwidth (requires DDR3-3200 or faster)
```

### 15.6 Real-Time FPGA Performance Requirements

| Metric | Requirement | Notes |
|--------|------------|-------|
| Pixel clock | 100–500 MHz | For 3072 cols, 50 µs/row → 61 MHz minimum |
| DDR3 bandwidth | 1–10 GB/s | For lag state variables + image buffers |
| FPGA logic | 200K–1M LUTs | Large designs for full pipeline + correction |
| BRAM | 100–500 Mb | For line buffers and LUTs |
| Correction latency | <1 frame period | Must complete before next frame starts |
| Host transfer rate | 300–1000 MB/s | Gigabit Ethernet (~125 MB/s) or PCIe |

**Reference:** "FPGA Based Real-Time Image Manipulation and Advanced Data Acquisition," arXiv:2010.15450, 2020 — [URL](https://arxiv.org/abs/2010.15450)

---

## Summary: Key Algorithm Reference Quick Guide

| Algorithm | Patent/Paper | Formula | Notes |
|-----------|-------------|---------|-------|
| Real-time offset update | US5452338A | `aᵢ = (1-1/n)aᵢ₋₁ + (1/n)pᵢ` | n=8–32; 30 Hz update |
| Metadata-indexed offset | EP2148500A1 | `E_final = [E - avg(D)] + DD_x(metadata)` | Metadata: temp, prep time, power state |
| Direct lag measurement | US7792251B2 | `corrected = [raw - offset(N)] - (N+1)×[lag - offset(0)]` | Pre-integration read phase |
| LTI lag deconvolution | Starman 2012 | `xₖ = y(k) - ΣbₙSₙ,ₖe^{-aₙ}/Σbₙ` | N=4 exp.; Varian 4030CB |
| NLCSC lag correction | Starman 2012 | Exposure-dependent IRF with stored-charge conservation | <0.29% residual lag |
| Forward bias reset | Starman 2011 | Hardware: +4V, 20 pC/pixel, 100 kHz | 93–95% lag reduction |
| Flat-field gain map | IEC 62220-1 | `G(x,y) = [I_bright(x,y) - D(x,y)] / mean(I_bright - D)` | RQA5 beam, calibration SID |
| Gain interpolation | US7404673B2 | `G(θ) = G₁ + (θ-θ₁)/(θ₂-θ₁)×(G₂-G₁)` | Multi-angle/multi-kVp gain maps |
| Recursive filter offset | US5452338A | `corrected = raw - offset_memory` | Continuously updated |

---

*Document compiled from: Google Patents, PubMed/PMC, AAPM publications, IEEE Xplore, ScienceDirect. All patent URLs verified via patents.google.com. Academic paper URLs verified via PubMed and PMC. Generated March 18, 2026.*
