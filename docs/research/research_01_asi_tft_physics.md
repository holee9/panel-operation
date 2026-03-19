# a-Si TFT Flat Panel Detector (FPD): Physical Characteristics and Driving Principles

**Research compiled:** 2026-03-18  
**Topic:** Comprehensive technical review of amorphous silicon thin-film transistor flat panel detector physics, for FPGA control system design

---

## Table of Contents

1. [Pixel Architecture and Structure](#1-pixel-architecture-and-structure)
2. [Charge Trapping in a-Si:H — Threshold Voltage Shift](#2-charge-trapping-in-a-sih--threshold-voltage-shift)
3. [Pixel Charge Collection — Integration Mode, Dark Current, Leakage](#3-pixel-charge-collection--integration-mode-dark-current-leakage)
4. [Gate Line Capacitance and RC Delay](#4-gate-line-capacitance-and-rc-delay)
5. [Ghosting / Image Lag Mechanism](#5-ghosting--image-lag-mechanism)
6. [Panel Stabilization Requirements](#6-panel-stabilization-requirements)
7. [Idle Mode Driving and Dummy Scan](#7-idle-mode-driving-and-dummy-scan)
8. [Effect of X-ray Dose on Panel Response](#8-effect-of-x-ray-dose-on-panel-response)
9. [Key Timing Parameters](#9-key-timing-parameters)
10. [State-of-the-Art Pixel Designs — Indirect vs. Direct Conversion](#10-state-of-the-art-pixel-designs--indirect-vs-direct-conversion)
11. [Design Implications for FPGA Control](#11-design-implications-for-fpga-control)
12. [Quantitative Reference Tables](#12-quantitative-reference-tables)
13. [References](#13-references)

---

## 1. Pixel Architecture and Structure

### 1.1 Basic Pixel Circuit

Each pixel in an a-Si:H TFT flat panel detector (FPD) consists of three functional elements:

1. **Photoconversion element** — a PIN (p-i-n) photodiode (indirect conversion) or charge collection electrode (direct conversion)
2. **Storage capacitor** — holds the integrated charge during X-ray exposure
3. **TFT switch** — an n-channel a-Si:H TFT that connects the pixel to the readout data line

The circuit operates in passive charge-accumulation mode: during exposure, the TFT gate is held LOW (OFF state), isolating the photodiode from the data line. Photogenerated charge accumulates on the pixel capacitance. During readout, the gate line is pulsed HIGH (ON state), and the stored charge flows through the TFT to the column charge amplifier.

```
                  Vbias
                    |
                +---+---+
                |  PIN  |
                | Diode |
                +---+---+
                    |
         Gate ---[TFT]--- Data Line
                    |
                   Cs (Storage Capacitor)
                    |
                   GND
```

*Source: [TFT Flat-Panel Array Image Acquisition – Radiology Key](https://radiologykey.com/tft-flat-panel-array-image-acquisition/); [Flat-Panel Imaging Arrays for Digital Radiography, Tredwell 2009](https://www.imagesensors.org/Past%20Workshops/2009%20Workshop/2009%20Papers/065_paper_tredwell_invited.pdf)*

### 1.2 Pixel Dimensions (Typical)

| Parameter | Typical Value | Notes |
|-----------|--------------|-------|
| Pixel pitch | 100–200 µm | Radiography standard |
| Fill factor | 70–85% | Photodiode active area / total pixel area |
| Photodiode capacitance (CPD) | 1.9–5 pF | Per pixel, ~196 µm pitch |
| Storage capacitor (Cs) | 0.5–4 pF | Integration capacitor (gain-selectable) |
| TFT W/L ratio | 10–50 µm / 5–15 µm | n-channel inverted staggered |
| a-Si:H layer thickness | 50 nm (optimal) | Thicker → higher leakage |

*Source: [Hoheisel, Amorphous Silicon X-Ray Detectors, ISCMP 1996](https://www.mhoheisel.de/docs/ISCMP91996112.pdf); [Nathan et al., a-Si detector and TFT technology, Microelectronics Journal 2000](https://www.fisica.unisa.it/antonio.dibartolomeo/MaterialeDidatticoFisicaSemiconduttori/Articoli/A-Si_andTFTtechnology%20for%20Xray%20detection.pdf)*

### 1.3 a-Si:H Material Properties

Amorphous silicon (a-Si:H) differs fundamentally from crystalline silicon due to its **disordered atomic structure**. Key consequences:

- **Electron drift mobility**: ≤ 4 cm²/(V·s) (versus ~1500 cm²/(V·s) in crystalline Si)
- **Hole drift mobility**: ≤ 0.01 cm²/(V·s) — extremely slow; holes are effectively immobile
- **Band gap**: ~1.7–1.8 eV (broader than c-Si 1.1 eV)
- **Density of states (DOS)**: Exponential band tails + midgap defect states from dangling bonds
  - Conduction band tail: Urbach parameter ~50 meV
  - Valence band tail: ~100 meV
  - Midgap defect density: 10¹⁵–10¹⁶ cm⁻³ (equilibrium undoped)

The broad DOS distribution is the root cause of charge trapping, threshold voltage instability, and image lag. The a-Si:H backplane is used not for its electronic performance but for its **large-area manufacturability** at low cost on glass substrates up to 40×30 cm².

*Source: [Starman, Stanford Thesis: Lag Correction in a-Si FPDs, 2013](https://stacks.stanford.edu/file/druid:dj434tf8306/Starman_Jared_thesis_withTitlePage-augmented.pdf); [LinkedIn: Comprehensive Analysis of Modern X-ray Detectors, 2025](https://www.linkedin.com/pulse/comprehensive-analysis-modern-x-ray-detectors-francesco-iacoviello-py1sf)*

### 1.4 Indirect-Conversion Pixel Stack (Standard)

For indirect conversion (dominant architecture):

```
 X-rays
   ↓
┌─────────────────────────────────┐
│  CsI:Tl Scintillator (600 µm)  │  ← X-ray → visible light (550 nm peak)
├─────────────────────────────────┤
│  a-Si:H PIN Photodiode          │  ← visible light → electron-hole pairs
├─────────────────────────────────┤
│  a-Si:H TFT Switch              │  ← charge switch
│  Storage Capacitor (Cs)         │  ← charge integration
├─────────────────────────────────┤
│  Readout Electronics (bottom)   │  ← charge amplifiers, ADC
└─────────────────────────────────┘
```

The CsI:Tl scintillator converts X-ray photons to ~550 nm green light with ~2,000–4,000 optical photons per X-ray. The a-Si:H photodiode has ~70–80% quantum efficiency at 550 nm.

---

## 2. Charge Trapping in a-Si:H — Threshold Voltage Shift

### 2.1 Physical Mechanism

a-Si:H TFTs suffer from **threshold voltage shift (ΔVth)** under sustained gate bias. Two mechanisms operate on different timescales:

**Stage I — Charge trapping in SiNx gate dielectric:**
- Dominant at short stress times (milliseconds to hours)
- Gate-field drives electrons/holes from the channel into trap sites in the SiNx passivation layer
- Reversible via de-trapping (recovery upon removing gate bias)

**Stage II — Defect creation in a-Si:H channel:**
- Dominant at long stress times (hours to years)
- Weak Si–Si bonds break under prolonged field stress, creating new dangling bond defects (Staebler-Wronski effect)
- Much slower to recover — requires thermal annealing

The time evolution follows a **stretched exponential law**:

$$\Delta V_{th}(t) = \Delta V_{th,max} \left[1 - \exp\left(-\left(\frac{t}{\tau_0}\right)^\beta\right)\right]$$

where:
- $\Delta V_{th,max}$ = maximum asymptotic shift
- $\tau_0$ = characteristic time constant
- $\beta$ = dispersion parameter (0 < β < 1 for disordered materials)

### 2.2 Quantitative ΔVth Parameters

| Parameter | Stage I (SiNx trapping) | Stage II (a-Si defects) |
|-----------|------------------------|------------------------|
| ΔVth,max | 0.06–0.31 V (low field) | ~3.8 V (high T, high field) |
| τ₀ at 20°C | 0.7–69.5 s (process-dependent) | 5.2 × 10⁸ s (~16.5 years) |
| β | 0.15–0.52 | 0.25–0.46 |
| Activation energy (Eact) | — | 0.89–0.90 eV |
| Attempt frequency (ν) | — | 4–5 × 10⁶ Hz |

**Example:** At VG = 5V, 20°C, Stage I saturates at ΔVth ≈ 0.08 V  
**Example:** At VG = 7.5V, 120°C, total ΔVth ≈ 3.8 V after 1200 s

*Source: [Liu, Princeton Thesis: Stability of a-Si TFTs, 2013](https://swh.princeton.edu/~sturmlab/theses/Ting_Liu_Thesis_Part1.pdf); [Nathan et al., 2000](https://www.fisica.unisa.it/antonio.dibartolomeo/MaterialeDidatticoFisicaSemiconduttori/Articoli/A-Si_andTFTtechnology%20for%20Xray%20detection.pdf)*

### 2.3 Recovery Behavior

After removing gate bias stress:
- Stage I (SiNx trapped charge) recovers relatively quickly — characteristic time depends on de-trapping rate
- Stage II (dangling bond defects) recovers extremely slowly at room temperature
- Full recovery of Stage II requires thermal annealing at 350°C for ~1 hour

For FPD operation: the relevant practical concern is **short-term Vth drift** during a scanning sequence (gate driver stress), which affects TFT ON-current and therefore charge transfer completeness.

### 2.4 TFT Electrical Parameters (Nominal)

| Parameter | Typical Value | Notes |
|-----------|--------------|-------|
| Field-effect mobility (µFE) | 0.3–1.1 cm²/(V·s) | Process-dependent |
| Threshold voltage (Vth) | 0.7–2.0 V | Fresh device |
| ON/OFF current ratio | 10⁷–10⁹ | Critical for charge retention |
| OFF-state leakage | < 0.1 pA | At VDS = 3V |
| Subthreshold slope | 235–300 mV/decade | — |

*Source: [Liu, Princeton Thesis, 2013](https://swh.princeton.edu/~sturmlab/theses/Ting_Liu_Thesis_Part1.pdf); [Flat-Panel Imaging Arrays, Tredwell 2009](https://www.imagesensors.org/Past%20Workshops/2009%20Workshop/2009%20Papers/065_paper_tredwell_invited.pdf)*

---

## 3. Pixel Charge Collection — Integration Mode, Dark Current, Leakage

### 3.1 Integration Mode Operation

During X-ray exposure (integration phase):
1. Gate line is held at V_GL (typically −5 to −10 V) → TFT is **OFF**
2. The PIN photodiode is reverse-biased (anode at negative Vbias, ~−5 V)
3. X-rays → CsI:Tl → green light → electron-hole pairs in a-Si:H PIN diode
4. Photogenerated holes drift toward negative electrode; electrons drift toward TFT storage node
5. Charge accumulates on Cpix = CPD + Cs; the photodiode voltage decreases from Vbias toward 0 V

The signal charge is:
$$Q_{signal} = C_{pix} \cdot \Delta V_{pix}$$

Pixel saturation occurs when ΔVpix = Vbias (photodiode forward voltage reached).

### 3.2 Dark Current

Dark current flows continuously even without illumination, setting the noise floor:

| Source | Magnitude | Notes |
|--------|-----------|-------|
| Photodiode dark current (reverse bias) | < 1 nA/cm² at −4 V | For ITO/a-Si:H structure |
| Photodiode dark current (ITO/a-Si:H Schottky) | ~7 × 10⁻¹⁰ A/cm² at −2 V | Optimized structure |
| TFT OFF-state leakage | < 0.1 pA (< 10⁻¹³ A) | At VDS = 3V, normal TFT |
| Trap-emission contribution to noise | ~400 e⁻ rms/pixel | For 196 µm pitch |

For a 100 µm pixel with ~0.7 nA/cm² dark current:
- Dark current per pixel ≈ 0.7 × 10⁻⁹ × (100 × 10⁻⁴)² ≈ 0.7 fA per pixel

*Source: [Nathan et al., 2000](https://www.fisica.unisa.it/antonio.dibartolomeo/MaterialeDidatticoFisicaSemiconduttori/Articoli/A-Si_andTFTtechnology%20for%20Xray%20detection.pdf); [Hoheisel, 1996](https://www.mhoheisel.de/docs/ISCMP91996112.pdf)*

### 3.3 Dark Current Mechanisms

Multiple mechanisms contribute:

1. **Thermal generation-recombination** (Shockley-Read-Hall) — dominates at moderate reverse bias. Depends strongly on temperature (doubles ~every 8–10°C for a-Si:H).
2. **Trap-assisted tunneling** — significant at higher reverse bias or high trap density
3. **Surface/edge leakage** — dominant for small pixels; passivation critical
   - [Sensors 2016 paper](https://pmc.ncbi.nlm.nih.gov/articles/PMC5017328/) shows dark current of 0.5 pA/mm² achievable with SiN/SU-8 bilayer passivation
4. **TFT OFF-state leakage into pixel** — small (< 0.1 pA) but non-zero; contributes to charge loss during long integration windows

### 3.4 Charge Sharing (Pixel Crosstalk)

Charge sharing between pixels degrades MTF:
- **Optical spreading in CsI:Tl**: columnar structure confines light laterally; typical 600 µm columns → MTF at Nyquist (2.55 lp/mm for 196 µm pitch) ≈ 18%
- **Carrier diffusion in PIN diode**: a-Si:H has short diffusion length (~100–200 nm) due to high defect density; lateral spreading minimal
- **Capacitive coupling between data lines**: geometry-dependent; minimized by design

---

## 4. Gate Line Capacitance and RC Delay

### 4.1 Gate Line Model

Each gate line in a large panel is a distributed RC transmission line:

```
R_gate_metal (resistance per unit length)
     ─────────────────────────────────────────→
     ├── C_gate_overlap (gate-to-pixel capacitance) × N_columns
     ├── C_gate_data_crossover × N_columns
     └── → Gate driver output
```

For a panel with $N_{col}$ columns and pixel pitch $p$:
- **Total gate line length**: $L_{gate} = N_{col} \times p$
- **Gate line resistance**: $R_{total} = \rho_s \times L_{gate} / W_{metal}$
- **Gate line capacitance**: $C_{total} \approx C_{pix} \times N_{col}$ (dominated by gate-drain overlap capacitance of each pixel TFT)

### 4.2 RC Delay for Large Panels

For a 3072-line panel with 3072 columns and 150 µm pitch:
- Gate line length = 3072 × 150 µm = 460.8 mm ≈ 46 cm
- Assuming aluminum gate metal: sheet resistance ~0.1 Ω/□, line width ~5 µm → $R_{gate} \approx 9 \text{ kΩ}$
- Gate-drain overlap per pixel (Cgd): ~50–190 fF (geometry dependent, from [Nathan 2000](https://www.fisica.unisa.it/antonio.dibartolomeo/MaterialeDidatticoFisicaSemiconduttori/Articoli/A-Si_andTFTtechnology%20for%20Xray%20detection.pdf))
- Total $C_{gate} \approx 100 \text{ fF} \times 3072 \approx 307 \text{ pF}$
- $\tau_{RC} = R \times C / 3 \approx 9 \text{ kΩ} \times 307 \text{ pF} / 3 \approx 0.92 \text{ µs}$

**Practical implication**: The gate pulse must be long enough to allow the signal to settle at the far end of the line. The settling time is typically 3–5 × τ_RC, requiring a gate pulse width of **several microseconds to tens of microseconds** for large panels.

For displays, the a-Si:H shift register outputs over ~2 ms timescale per frame (from [Tredwell 2009](https://www.imagesensors.org/Past%20Workshops/2009%20Workshop/2009%20Papers/065_paper_tredwell_invited.pdf)), indicating that a single gate line pulse width may be on the order of 20–100 µs for large-format panels.

**Gate driver on-glass (GOG) timing:**
- SiOG (Silicon-on-Glass) inverter: propagation delay ~5 ns per stage
- ELA poly-Si inverter: ~29 ns per stage
- a-Si:H shift register: outputs in ~2 ms time frame

### 4.3 Gate Pulse Width Requirements

The gate must remain ON long enough for:
1. Charge transfer from pixel to data line to reach steady state (requires 3–5 × τ_RC of the data line/TFT network)
2. Signal settling at the input of the charge amplifier

Gate duration requirement scales roughly as:
$$T_{gate\_on} \approx 5 \times R_{TFT,on} \times C_{data\_line}$$

Where $R_{TFT,on} = L / (W \mu C_{ox} (V_{GS} - V_{th}))$ and $C_{data\_line}$ is the total parasitic capacitance of the data line (all pixel TFT source junctions + metal line capacitance).

For typical parameters: $R_{TFT,on} \approx 0.1–1 \text{ MΩ}$, $C_{data\_line} \approx 10–50 \text{ pF}$ → $T_{gate\_on} \approx 5–250 \text{ µs}$

The literature reports gate on-times of **10–50 µs** for 1024-line displays, scaling to **100+ µs** for 3072-line medical X-ray panels.

*Source: [IntechOpen, AMLCD Fundamentals](https://www.intechopen.com/chapters/11268); [Tredwell 2009](https://www.imagesensors.org/Past%20Workshops/2009%20Workshop/2009%20Papers/065_paper_tredwell_invited.pdf); [AIP two-color a-Si sensor, 2001](https://pubs.aip.org/aip/jap/article-pdf/90/3/1589/10614092/1589_1_online.pdf)*

---

## 5. Ghosting / Image Lag Mechanism

### 5.1 Definition and Manifestation

**Image lag** (ghosting) is the persistence of signal from a previous frame into subsequent frames. For a-Si FPDs:
- **First frame lag**: Typically 2–7% of the preceding frame signal (at 15 fps)
- **50th frame lag**: ~0.1–1% (decays as multi-exponential, long tail)
- **100th frame lag**: Still detectable contrast in ghost images

Lag is distinguished from:
- **Scintillator afterglow** (CsI:Tl phosphorescence): exponential decay ~0.7% over 600 frames, independent of electrical state
- **Electrical lag**: due to trapped charge in a-Si:H (dominant source)

### 5.2 Physical Mechanism — Charge Trapping

The primary mechanism is **charge trapping in the a-Si:H photodiode**:

1. During X-ray illumination, photogenerated charge fills trap states in the a-Si:H bulk and at interfaces
2. Trap density: **10¹⁴–10¹⁹ cm⁻³ eV⁻¹** spanning 0.1–0.8 eV below the conduction band
3. After illumination ceases, trapped charge is slowly re-emitted into the conduction band
4. Re-emitted charge flows through the TFT during subsequent readout frames — appearing as "ghost" signal

The trap release rate follows a thermally activated process:
$$a_{release}(E_t) = \nu_0 \cdot \exp\left(-\frac{E_t}{kT}\right)$$

where $E_t$ is trap depth from conduction band and $\nu_0$ is attempt frequency. This gives a broad spectrum of time constants from microseconds (shallow traps) to years (deep traps).

### 5.3 Multi-Exponential Lag Model

The detector falling step-response function (FSRF) is modeled as a sum of exponentials:
$$S_{lag}(t) = \sum_{n=1}^{N} b_n \cdot S_0 \cdot \exp(-a_n \cdot t)$$

where $a_n$ are lag rates (frames⁻¹) and $b_n$ are lag coefficients.

**Calibrated parameters for Varian 4030CB at 27% saturation (15 fps):**

| Component | Lag rate $a_n$ (frames⁻¹) | Lag coefficient $b_n$ | Physical time constant |
|-----------|--------------------------|----------------------|------------------------|
| n=1 (slow) | 2.5 × 10⁻³ | 7.1 × 10⁻⁶ | ~667 frames = 44 s |
| n=2 | 2.1 × 10⁻² | 1.1 × 10⁻⁴ | ~48 frames = 3.2 s |
| n=3 | 1.6 × 10⁻¹ | 1.7 × 10⁻³ | ~6 frames = 0.4 s |
| n=4 (fast) | 7.6 × 10⁻¹ | 1.8 × 10⁻² | ~1.3 frames = 87 ms |

*Source: [Starman et al., Nonlinear Lag Correction, Medical Physics 2012](https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/)*

### 5.4 Lag Magnitudes

| Frame | Uncorrected Lag (% of exposure signal) | After best correction |
|-------|-----------------------------------------|----------------------|
| 1st lag frame | 2.4–3.7% | 0.25–0.29% |
| 50th lag frame | 0.28–0.96% | 0.003–0.01% |
| 100th lag frame | Still visible contrast | ~negligible |

**Important non-linearity**: Lag rates are **exposure-dependent** — a linear, time-invariant (LTI) model is inadequate. Higher exposure → different IRF because trap occupation changes fill levels and alter trap dynamics.

### 5.5 TFT Off-State Leakage Contribution

Secondary contribution to lag from TFT OFF-state leakage:
- Typical: < 0.1 pA per pixel → 100 fC loss per second
- For a 200 fF pixel capacitor holding 1 pC signal → leaks ~10% per second
- This is why gate-OFF voltage must be sufficiently negative (V_GL ≈ −5 to −10 V) to ensure $I_{off}$ < 10⁻¹³ A

*Source: [Tredwell 2009](https://www.imagesensors.org/Past%20Workshops/2009%20Workshop/2009%20Papers/065_paper_tredwell_invited.pdf); [Starman Stanford Thesis, 2013](https://stacks.stanford.edu/file/druid:dj434tf8306/Starman_Jared_thesis_withTitlePage-augmented.pdf)*

### 5.6 Mitigation Techniques

1. **Forward bias (FB) method**: Briefly forward-bias the photodiode to fill all trap states with charge before each frame
   - 5.8 pC/diode → 95% first-frame lag reduction
   - 20 pC/diode used in practice (above saturation)
   - Forward bias time: ~40 µs per pixel (if simultaneous), or ~30 ms for row-by-row
   - Residual first-frame lag after FB: < 0.3% (vs. ~2.5% standard)

2. **LED illumination saturation**: Flash LEDs to uniformly fill traps before each frame
   - Creates uniform lag offset (subtracted as offset correction)
   - Residual first-frame lag > 2% (less effective than FB method)

3. **Empty frame flushing (Flush-N)**: Insert N blank frames between acquisition frames
   - 1 empty frame removes ~50% of lag signal
   - Reduces frame rate by (N+1)× — impractical for fluoroscopy

4. **Software LTI deconvolution**: Works for small signals; fails at high exposures due to system non-linearity

*Source: [Starman et al., Forward Bias Method, Medical Physics 2011](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/); [Starman Nonlinear Lag Correction 2012](https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/)*

---

## 6. Panel Stabilization Requirements

### 6.1 Thermal Stabilization

Temperature affects:
- **Dark current**: Increases with temperature (thermal generation)
- **Trap emission rates**: Faster at higher T → shorter lag time constants but larger initial lag
- **TFT threshold voltage**: Vth shifts more rapidly at elevated temperature
- **Scintillator response**: CsI:Tl has temperature-dependent light yield

**Recommended stabilization procedure:**
- Allow panel to operate at idle for **minimum 30 minutes** before calibration
- Full trap equilibrium: **> 1 hour** warm-up time for deep trap states to reach steady state
- Temperature stability: ±1°C or better during calibration sequences

*Source: [Starman Forward Bias Method 2011](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/): "The warm-up time for the detector is greater than 1 hour" for deep trap levels to fill.*

### 6.2 Bias-Stress Recovery

After power-up or following extended periods without scanning:
- Trap states equilibrate to a new (empty) state
- First exposures show anomalously high lag (traps depopulated)
- Steady-state operation requires **hundreds of frames** of imaging or preconditioning

**Preconditioning scan protocol:**
1. Apply gate scan sequence at operating frequency (idle mode) for several hundred frames
2. Expose panel to flat-field X-ray or flood light illumination to partially fill traps
3. Wait for lag signal to reach steady-state plateau
4. Perform offset and gain calibrations

### 6.3 Calibration Requirements

Three primary calibrations are required periodically:

| Calibration | Purpose | Frequency |
|-------------|---------|-----------|
| **Offset (dark) correction** | Remove TFT/diode dark signal and fixed pattern noise | Every ~1 hr or after temperature change |
| **Gain correction** | Normalize pixel sensitivity variations | Daily or after X-ray system changes |
| **Defect pixel correction** | Map and interpolate dead/hot pixels | Periodic (panel aging) |

**Critical timing constraint for offset correction:** The offset calibration image must be collected AFTER the panel has reached lag equilibrium. If collected too early (insufficient warm-up), the stored offset will include residual lag signal → negative ghosting artifacts in subsequent images.

*Source: [AAPM Digital Radiography Review](https://www.aapm.org/meetings/03am/pdf/9877-37003.pdf); [X-ray FPD use and maintenance, xraydr.com](https://www.xraydr.com/use-and-maintenance-of-flat-panel-detector/)*

---

## 7. Idle Mode Driving and Dummy Scan

### 7.1 Purpose of Idle Mode

When the FPD is powered but not actively imaging, a continuous idle scan sequence is required to:

1. **Maintain trap equilibrium**: Periodic gate pulses keep the TFTs exercised and charge traps partially filled, preventing the panel from "resetting" to a cold (untrapped) state
2. **Maintain dark current equilibrium**: Prevents photodiode charge from accumulating toward forward bias during long idle periods
3. **Prevent Vth drift accumulation**: Alternating bias states reduces sustained-stress bias on gate driver TFTs
4. **Enable rapid acquisition readiness**: Panel reaches a predictable pre-exposure state

### 7.2 Idle Scan Sequence

Typical idle mode operation:
- **Scan rate**: Same as acquisition frame rate (e.g., 7.5, 10, or 15 fps)
- **Gate voltage**: Normal V_GH/V_GL swing applied to each row sequentially
- **Data line state**: Column amplifiers in reset/drain mode (charge discarded)
- **Photodiode bias**: Maintained at V_bias (same as during acquisition)

Without idle scanning, re-engaging the panel after a 10-second idle produces anomalous first-frame response due to charge accumulation and trap depopulation.

### 7.3 Dummy Scan for Pre-Exposure Conditioning

Before each acquisition (especially for CBCT or high-lag fluoroscopy):
- Apply **N_dummy frames** of scanning (without X-ray) to stabilize panel state
- Typically 3–10 dummy frames sufficient to reach steady-state for short idle periods
- After extended power-off (>30 min), may require 50–100+ dummy frames

```
FPGA control sequence:
  1. Enable idle scan (continuous)
  2. On trigger: begin dummy_scan[N=5] 
  3. Gate X-ray source
  4. Begin acquisition (actual frames)
  5. Return to idle scan
```

---

## 8. Effect of X-ray Dose on Panel Response

### 8.1 Linearity

a-Si:H FPDs exhibit **excellent linearity** over most of their dynamic range:

- Linearity error < **0.3%** over the design operating range ([Hoheisel 1996](https://www.mhoheisel.de/docs/ISCMP91996112.pdf))
- [AMFPI dosimetry study (Medical Physics 1999)](https://pubmed.ncbi.nlm.nih.gov/10501053/) reports linear response better than **99%** for signals within the first 25% of pixel charge capacity
- Signal deviates from linearity near saturation (within ~10% of full-scale)

### 8.2 Dynamic Range

| Parameter | Typical Value |
|-----------|--------------|
| ADC resolution | 12–16 bit |
| Dynamic range (ratio of saturation to noise floor) | 76.9:1 (single exposure) up to 166.7:1 (dual exposure) |
| Maximum linear dose | ~88 µGy (per PRORAD spec) |
| Sensitivity (typical CsI) | 574 LSB/µGy |
| Dark noise | 2.7 LSB (CsI) |

*Source: [PRORAD FPD Manual](https://fcc.report/FCC-ID/2A7E500001/6084598.pdf); [Dual-exposure dynamic range extension, Medical Physics 2014](https://pubmed.ncbi.nlm.nih.gov/24352046/)*

### 8.3 Dose-Rate Effects (Ghosting)

The lag signal is dose-history-dependent — not just the most recent frame:
- Higher accumulated dose → more traps filled → stronger (but also more uniform) lag
- After a high-dose radiographic exposure (1 µGy) followed by low-dose fluoroscopy (15 nGy/frame), the ghost contrast can be up to **355 detector counts** in the first lag frame
- EPID response varies by up to **8%** over large dose-per-pulse and PRF ranges ([PubMed 2004](https://pubmed.ncbi.nlm.nih.gov/15000614/))

### 8.4 X-ray Dose Effect on TFT (Vth Shift)

Under high cumulative X-ray dose, the TFT itself can shift:
- For a-IGZO TFTs: Vth shifts **−6.2 V** after 100 Gy X-ray irradiation (negative shift due to hydrogen incorporation and oxygen vacancy ionization) ([RSC Advances 2019](https://pmc.ncbi.nlm.nih.gov/articles/PMC9065737/))
- For a-Si:H TFTs: More radiation-hard than oxide TFTs; tested up to ~80 Gy without failure ([Hoheisel 1996](https://www.mhoheisel.de/docs/ISCMP91996112.pdf))
- At clinical doses (mGy per exposure, total panel lifetime ~several Gy), a-Si:H TFT degradation is not significant

### 8.5 Sensitivity and Signal Level

| Parameter | Value |
|-----------|-------|
| Electrons per X-ray quantum absorbed (CsI) | 1,150–2,400 e⁻/X-ray |
| Electronic noise floor | 1,000–10,000 e⁻ rms |
| Minimum detectable signal (SNR=5) | 5,000–50,000 e⁻ |
| Fluoroscopy frame dose | ~15 nGy/image at 12.5 fps |
| Radiography dose | ~1 µGy per image |

---

## 9. Key Timing Parameters

### 9.1 Integration Mode Timing

```
Frame timing (e.g., 15 fps = 66.7 ms/frame):

  T_frame = 66.7 ms
  ├── T_exposure = 0–50 ms  (X-ray on, TFT gate OFF, charge integrating)
  ├── T_readout = N_rows × T_line  (TFT gates fired row by row)
  └── T_settling = post-readout pause (reset, idle, next frame prep)

  T_line (per row time):
  = T_gate_on + T_gate_off_guard
  
  For 3072 rows at 15 fps: T_readout < 66.7 ms
  → T_line (max) = 66.7 ms / 3072 ≈ 21.7 µs per line
  
  For 30 fps acquisition:
  → T_line (max) = 33.3 ms / 3072 ≈ 10.8 µs per line
```

### 9.2 T_gate_on — Gate Pulse Width

The gate must remain active long enough to transfer ≥99.9% of pixel charge:

$$T_{gate\_on} \geq 5 \times R_{TFT,on} \times (C_{data} + C_{parasitic})$$

Practical values:
- **Minimum** (small panel, fast TFT): ~5 µs
- **Typical** (large panel, 150 µm pitch): **10–20 µs**
- **Conservative** (large panel with high data line capacitance): **20–50 µs**

For charge transfer >99.9%: $T_{gate\_on} > \ln(1000) \times \tau_{RC} \approx 6.9 \times \tau_{RC}$

The gate driver output pulse for LCD/medical FPD applications is reported as **10–50 µs** depending on panel size and resolution.

*Source: [IntechOpen AMLCD, 2009](https://www.intechopen.com/chapters/11268): "The duration of the gate pulses is about 10–50 µs"*

### 9.3 T_line — Minimum Line Time

$$T_{line,min} = T_{gate\_on} + T_{gate\_settle} + T_{data\_transfer} + T_{guard}$$

| Component | Typical Value |
|-----------|--------------|
| T_gate_on | 10–50 µs |
| T_gate settle (turn-off) | 1–5 µs |
| T_data transfer to ADC | 1–10 µs |
| Guard time | 1–5 µs |
| **T_line total** | **~20–70 µs** |

For a 3072-row panel at 15 fps: T_line ≤ 21.7 µs → very tight; requires optimized gate driver and fast ADC.

### 9.4 T_reset — Panel Reset Time

Full panel reset (clearing all pixel charge):
- Requires one complete scan of all gate lines with V_GH applied
- At $T_{line}$ = 20 µs: $T_{reset} = 3072 \times 20 µs ≈ 61.4 ms$
- Multiple reset scans improve charge clearance

For lag suppression after high-dose exposure:
- 3–5 reset scans before calibration acquisition recommended
- Total reset time: 3 × 61.4 ms ≈ 184 ms minimum

### 9.5 Key Forward-Bias Timing (Lag Reduction)

| Parameter | Value |
|-----------|-------|
| Forward bias time per pixel (if simultaneous) | ~40 µs |
| Forward bias voltage | +4 V (forward direction) |
| Charge injected per pixel | 5.8–20 pC/diode |
| First-frame lag reduction (FB method) | 70–93% |
| Current implementation (row-by-row groups of 8) | ~30 ms total overhead |
| Future implementation (all-simultaneous with stronger amps) | ~40 µs total |

*Source: [Starman et al., Forward Bias Method, Medical Physics 2011](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/)*

### 9.6 Summary Timing Table

| Parameter | Symbol | Typical Value | Notes |
|-----------|--------|--------------|-------|
| Gate pulse width | T_gate_on | 10–50 µs | Depends on panel size |
| Line time (per row) | T_line | 20–70 µs | T_gate_on + settling |
| Frame time (15 fps) | T_frame | 66.7 ms | — |
| Full panel readout (3072 rows) | T_readout | 61–215 ms | 3072 × T_line |
| Panel reset (full scan) | T_reset | 61–215 ms | Same as readout |
| Dummy frames before acquisition | N_dummy | 3–10 frames | For lag equilibration |
| Warm-up time (cold start) | T_warmup | 30–60 min | For full trap filling |
| First-frame lag (uncorrected) | Lag_1 | 2–7% | Of preceding signal |
| First-frame lag (FB corrected) | Lag_1_FB | < 0.3% | With forward bias |
| Lag time constant (fastest) | τ_fast | ~87 ms | 4th exponential component |
| Lag time constant (slowest) | τ_slow | ~44 s | 1st exponential component |
| Forward bias time per pixel | T_FB | 40 µs | For lag suppression |

---

## 10. State-of-the-Art Pixel Designs — Indirect vs. Direct Conversion

### 10.1 Indirect Conversion (CsI:Tl + a-Si:H Photodiode)

The dominant architecture for general radiography and fluoroscopy:

**Two-step conversion:**
1. X-ray → visible light (CsI:Tl scintillator)
2. Visible light → charge (a-Si:H PIN photodiode)

**CsI:Tl Scintillator Properties:**
- Crystal structure: **columnar/needle-like** — minimizes lateral light spread
- Typical thickness: 600–1000 µm for standard radiography
- Conversion gain: ~2,000–4,000 visible photons per X-ray
- Peak emission: ~550 nm (well-matched to a-Si:H response)
- Columnar structure achieved by vapor deposition on Al substrate
- Intrinsic phosphorescence (afterglow): ~0.7% over 600 frames

**Performance (CsI:Tl at 120 kVp, RQA9 beam):**

| Parameter | CsI:Tl (600 µm) | CsI:Tl (1000 µm) | GOS powder |
|-----------|-----------------|------------------|------------|
| DQE(0) | ~50% | ~60–70% | ~40% |
| MTF at Nyquist | Moderate | Lower | Lower |
| Light spread | Minimal | Some | Significant |
| Spatial resolution | High | Moderate | Moderate |
| Cost | High | High | Low |

*Source: [CsI:Tl vs GOS comparison, Medical Physics 2019](https://pmc.ncbi.nlm.nih.gov/articles/PMC6842040/)*

**Back-irradiation (BI) geometry improvement:**
- Irradiating through the TFT side (BI) increases DQE and MTF vs. standard front-irradiation
- X-rays are preferentially absorbed near the scintillator-photodiode interface
- BI geometry improvement: +17% sensitivity, higher spatial frequency DQE for 1 mm CsI:Tl

### 10.2 Direct Conversion (a-Se)

**Single-step conversion:** X-ray → electron-hole pairs directly in amorphous selenium

**a-Se layer properties:**
- Typical thickness: 200–500 µm (200–250 µm for mammography, 500 µm for chest)
- Applied electric field: ~10 V/µm (high voltage: 5–20 kV)
- Absorption at 40 keV: >95% in 250 µm (mammography range)
- No lateral charge spread → excellent spatial resolution

**Key advantage**: No optical spreading step → **inherently higher spatial resolution** than indirect. No scintillator afterglow.

**Key limitations:**
- High-voltage operation complexity (5–20 kV bias circuits)
- Bulk charge trapping in a-Se → ghosting (from electrode charge buildup)
- Poor performance at higher X-ray energies (>60 keV) compared to CsI
- Dark current mechanisms: Schottky emission from electrode + bulk thermal generation

*Source: [Direct-conversion a-Se detectors, Journal of X-ray Science and Technology 2002](https://journals.sagepub.com/doi/pdf/10.3233/XST-2002-00055)*

### 10.3 Comparative Table: Indirect vs. Direct Conversion

| Parameter | Indirect (CsI:Tl) | Direct (a-Se) |
|-----------|-------------------|---------------|
| Conversion steps | 2 (X-ray→light→charge) | 1 (X-ray→charge) |
| Scintillator needed | Yes (CsI:Tl, GOS) | No |
| Spatial resolution | High (columnar CsI) | Highest |
| DQE at low frequency | Excellent (>60%) | Good (40–50%) |
| DQE at high frequency | Moderate | Excellent |
| Energy range | Broad (40–140 kVp) | Best < 40 keV |
| Operating voltage | Low (~−5 V bias) | High (5–20 kV) |
| Image lag (electrical) | Moderate (a-Si:H trapping) | Low (a-Se good charge transport) |
| Scintillator afterglow | ~0.7% (CsI:Tl) | None |
| Main lag source | a-Si:H photodiode traps | Electrode charge accumulation in a-Se |
| Best application | General radiography, fluoroscopy | Mammography, high-res applications |
| Cost (2025) | $70k–$120k | $80k–$150k |
| Radiation hardness (TFT) | Excellent (a-Si:H) | Same TFT backplane |

*Source: [Spectrum XRay indirect vs direct comparison](https://spectrumxray.com/indirect-vs-direct-conversion-dr-detectors-which-technology-delivers-better-image-quality/); [How Radiology Works comparison](https://howradiologyworks.com/direct-vs-indirect-digital-radiography/)*

### 10.4 Emerging Technologies

**a-IGZO TFT backplane (replacing a-Si:H):**
- Electron mobility: 10–30 cm²/(V·s) — 10× higher than a-Si:H
- Lower OFF current: ~10⁻¹⁵ A (vs. 10⁻¹³ A for a-Si:H)
- Faster readout → shorter T_gate_on → higher frame rates possible
- Better for dynamic imaging at 100 µm pixel pitch
- **Radiation vulnerability**: a-IGZO TFTs show significant Vth shift under X-ray irradiation (−6.2 V at 100 Gy); requires shielding or design mitigation

**CMOS Active Pixel Sensor (APS) backplane:**
- On-pixel amplification → lower readout noise (~10 e⁻ vs. ~1000 e⁻ for passive a-Si:H)
- Higher fill factor with advanced processes
- Limited to smaller panel sizes (expensive crystalline Si wafers)
- Best DQE at low dose → pediatric applications

**Pixelated CsI:Tl scintillator:**
- Laser-machined grooves to create pixel-matched columns (100 µm pitch, 700 µm deep)
- 77% gain in spatial resolution at 2 lp/mm vs. standard columnar CsI
- Enables thicker scintillator (higher absorption) without MTF penalty

*Source: [Pixelated CsI:Tl, Johns Hopkins 2020](https://pure.johnshopkins.edu/en/publications/pixelated-columnar-csitl-scintillator-for-high-resolution-radiogr); [IGZO TFT X-ray effect, RSC Advances 2019](https://pmc.ncbi.nlm.nih.gov/articles/PMC9065737/)*

---

## 11. Design Implications for FPGA Control

### 11.1 Gate Driver Sequencing

**Requirements:**
1. **Line-by-line scanning**: Gate lines activated sequentially, one at a time
2. **Gate ON voltage**: V_GH = +15 to +25 V (sufficient to saturate TFT channel, overcoming Vth shift)
3. **Gate OFF voltage**: V_GL = −5 to −10 V (ensure sub-pA TFT off-current for charge retention)
4. **Gate pulse width**: Programmable 10–100 µs; choose based on panel size and frame rate requirement
5. **Timing precision**: Gate pulse edges should be aligned with data line settling within ±0.5 µs

**FPGA design considerations:**
- Gate pulse width counter: N_clk = T_gate_on × f_clk (e.g., 20 µs × 100 MHz = 2000 counts)
- Line advance: Increment gate shift register after each T_line period
- For 3072 lines: Line counter range 0–3071; full scan at 21.7 µs/line → 66.6 ms

### 11.2 Frame Timing State Machine

```
States:
  IDLE        → Continuous scan at frame rate, discard data
  PRE_ACQ     → N_dummy dummy scans (flush, establish equilibrium)
  INTEGRATE   → TFT gates held OFF, X-ray on, charge accumulating
  READOUT     → Sequential gate pulses, data capture
  RESET       → Optional full scan to clear residual charge
  CALIBRATE   → Dark frame acquisition (no X-ray)
```

**Timing for 3072-line panel at 15 fps (66.7 ms frame):**
- T_line = 21.7 µs max
- T_readout = 3072 × 21.7 µs = 66.6 ms ≈ entire frame time
- T_gate_on must be < 15 µs for clean fit, with 6.7 µs remaining for guard/reset
- Alternatively: reduce scan lines (binning), use faster line time, or accept lower frame rate

### 11.3 Lag Compensation Strategy

**Software approach (for FPGA with DSP capability):**
1. Store previous frame: $S_{prev}[i,j]$
2. Estimate trapped charge: $Q_{trap,n}(t) = b_n \cdot S_{prev} \cdot \exp(-a_n \cdot t_{frame})$
3. Subtract from current frame: $S_{corrected} = S_{current} - \sum_n Q_{trap,n}$
4. Update trap state recursively for next frame

**Hardware approach (forward bias):**
1. After readout, FPGA commands forward bias scan sequence
2. Forward bias pulse: +4V on photodiode anode (specific pin control)
3. Forward bias duration per pixel: ≥40 µs (if simultaneous)
4. Overhead: 40 µs (simultaneous) or 30 ms (row-by-row)

### 11.4 Idle Mode Implementation

```
Idle mode FPGA loop:
  while (no_trigger):
    for row in range(3072):
      assert gate_VGH[row]
      wait(T_gate_on)
      assert gate_VGL[row]
      wait(T_guard)
    discard_data()
  enable_dummy_frames(N=5)
  wait_for_exposure_trigger()
```

### 11.5 Critical FPGA Design Parameters

| Parameter | Value | Notes |
|-----------|-------|-------|
| Gate pulse width | 15–50 µs | Programmable register |
| Gate line count | 3072 | Full panel |
| V_GH drive voltage | +20–25 V | Level shifter required |
| V_GL drive voltage | −5 to −10 V | Negative supply required |
| Data sampling clock | ≥10 MHz | For 1 µs settling resolution |
| ADC bits | 14–16 | Per data line |
| Dummy frames pre-exposure | 5–10 | Programmable |
| Dark frame average | 8–16 frames | For offset correction |
| Lag correction coefficients | 4 × 2 per frame | b_n, a_n stored in LUT |

---

## 12. Quantitative Reference Tables

### Table 1: a-Si:H TFT Electrical Parameters

| Parameter | Symbol | Value | Source |
|-----------|--------|-------|--------|
| Field-effect mobility | µFE | 0.3–1.1 cm²/(V·s) | [Liu 2013] |
| Threshold voltage | Vth | 0.7–2.0 V | [Liu 2013] |
| ON/OFF current ratio | ION/IOFF | 10⁷–10⁹ | [Liu 2013], [Nathan 2000] |
| OFF-state leakage | IOFF | < 0.1 pA | [Nathan 2000] |
| Subthreshold slope | SS | 235–300 mV/dec | [Liu 2013] |
| Gate dielectric (SiNx) | εr | ~6–7 | [Nathan 2000] |
| Optimal a-Si:H thickness | — | 50 nm | [Nathan 2000] |
| Band mobility (electrons) | µband | 1–10 cm²/(V·s) | [Liu 2013] |
| Drift mobility (electrons) | µdrift | ≤ 4 cm²/(V·s) | [Liu 2013] |

### Table 2: Pixel Electrical Parameters

| Parameter | Symbol | Typical Value | Notes |
|-----------|--------|--------------|-------|
| Photodiode capacitance | CPD | 1.9–5 pF | 196 µm pitch |
| Storage capacitor | Cs | 0.5–4 pF | Selectable gain |
| Dark current density (PIN) | Jdark | < 1 nA/cm² | At −4 V reverse bias |
| Dark current (ITO/Schottky) | Jdark | ~0.7 nA/cm² | At −2 V |
| Fill factor | FF | 70–85% | Active/total pixel area |
| Pixel noise (reset) | — | ~750 e⁻ rms | [Hoheisel 1996] |
| Readout circuit noise | — | ~1010 e⁻ rms | At 115 pF data line |
| Total electronic noise | — | ~1450 e⁻ rms | [Hoheisel 1996] |

### Table 3: Lag / Charge Trapping Parameters

| Parameter | Value | Source |
|-----------|-------|--------|
| First-frame lag (uncorrected, 15 fps) | 2–7% | [Starman 2011/2012] |
| 50th-frame lag (uncorrected) | 0.28–0.96% | [Starman 2012] |
| First-frame lag (FB corrected) | < 0.3% | [Starman 2011] |
| Lag rate n=1 (slowest) | 2.5 × 10⁻³ frames⁻¹ (τ ≈ 44 s) | [Starman 2012] |
| Lag rate n=4 (fastest) | 7.6 × 10⁻¹ frames⁻¹ (τ ≈ 87 ms) | [Starman 2012] |
| Trap density in a-Si:H | 10¹⁴–10¹⁹ cm⁻³eV⁻¹ | [Starman 2011] |
| Deep trap concentration (~0.45 eV) | ~4 × 10¹⁷ cm⁻³eV⁻¹ | [Starman 2011] |
| Forward bias charge (for 95% lag removal) | ≥ 5.8 pC/diode | [Starman 2011] |

### Table 4: Threshold Voltage Shift Parameters

| Parameter | Stage I (SiNx) | Stage II (a-Si defects) | Source |
|-----------|----------------|------------------------|--------|
| ΔVth,max | 0.06–0.31 V | Up to 3.8 V | [Liu 2013] |
| τ₀ at 20°C | 0.7–69.5 s | 5.2 × 10⁸ s | [Liu 2013] |
| Dispersion β | 0.15–0.52 | 0.25–0.46 | [Liu 2013] |
| Activation energy | — | 0.89–0.90 eV | [Liu 2013] |
| Recovery: Stage I | Fast (minutes) | Slow (years) | [Liu 2013] |
| ΔVth (X-ray, 100 Gy, a-IGZO) | −6.2 V | — | [RSC Adv 2019] |

### Table 5: Timing Parameters

| Parameter | Symbol | Value | Notes |
|-----------|--------|-------|-------|
| Gate pulse width | T_gate_on | 10–50 µs | Panel size-dependent |
| Minimum line time | T_line | 20–70 µs | — |
| Full frame readout (3072 lines) | T_readout | 61–215 ms | — |
| Panel reset (full scan) | T_reset | 61–215 ms | — |
| Warm-up time | T_warmup | 30–60 min | Deep trap equilibration |
| Dummy frames pre-acquisition | N_dummy | 5–10 | — |
| Forward bias time (all-simultaneous) | T_FB | ~40 µs | Future implementation |
| Forward bias time (row-by-row) | T_FB,row | ~30 ms | Current Varian 4030CB |
| CsI:Tl afterglow 63% time constant | — | ~50 ms | Scintillator lag |

### Table 6: CsI:Tl Scintillator Properties

| Property | Value |
|----------|-------|
| Crystal structure | Columnar/needle-like |
| Typical thickness | 600–1000 µm |
| Conversion gain | 2,000–4,000 optical photons per X-ray |
| Peak emission wavelength | ~550 nm |
| a-Si:H photodiode quantum efficiency at 550 nm | ~70–80% |
| Afterglow (scintillator intrinsic) | ~0.7% over 600 frames |
| X-ray absorption at 40 keV (600 µm) | ~70% |

---

## 13. References

1. **Starman, J. et al.** "A forward bias method for lag correction of an a-Si flat panel detector." *Medical Physics*, 2011. [https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/)

2. **Starman, J. et al.** "A nonlinear lag correction algorithm for a-Si flat-panel x-ray detectors." *Medical Physics*, 2012. [https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/](https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/)

3. **Starman, J.** "Lag Correction in Amorphous Silicon Flat-Panel X-ray Detectors." *Stanford University PhD Thesis*, 2013. [https://stacks.stanford.edu/file/druid:dj434tf8306/Starman_Jared_thesis_withTitlePage-augmented.pdf](https://stacks.stanford.edu/file/druid:dj434tf8306/Starman_Jared_thesis_withTitlePage-augmented.pdf)

4. **Nathan, A. et al.** "Amorphous silicon detector and thin film transistor technology for large-area imaging of X-rays." *Microelectronics Journal*, 2000. [https://www.fisica.unisa.it/antonio.dibartolomeo/MaterialeDidatticoFisicaSemiconduttori/Articoli/A-Si_andTFTtechnology%20for%20Xray%20detection.pdf](https://www.fisica.unisa.it/antonio.dibartolomeo/MaterialeDidatticoFisicaSemiconduttori/Articoli/A-Si_andTFTtechnology%20for%20Xray%20detection.pdf)

5. **Liu, T.** "Stability of Amorphous Silicon Thin Film Transistors." *Princeton University PhD Thesis*, 2013. [https://swh.princeton.edu/~sturmlab/theses/Ting_Liu_Thesis_Part1.pdf](https://swh.princeton.edu/~sturmlab/theses/Ting_Liu_Thesis_Part1.pdf)

6. **Hoheisel, M. et al.** "Amorphous Silicon X-Ray Detectors." *ISCMP Proceedings*, 1996. [https://www.mhoheisel.de/docs/ISCMP91996112.pdf](https://www.mhoheisel.de/docs/ISCMP91996112.pdf)

7. **Tredwell, T.** "Flat-Panel Imaging Arrays for Digital Radiography." *13th International Workshop on Image Sensors*, 2009. [https://www.imagesensors.org/Past%20Workshops/2009%20Workshop/2009%20Papers/065_paper_tredwell_invited.pdf](https://www.imagesensors.org/Past%20Workshops/2009%20Workshop/2009%20Papers/065_paper_tredwell_invited.pdf)

8. **Antonuk, L.E. et al.** "Relative dosimetry using active matrix flat-panel imager (AMFPI) technology." *Medical Physics*, 1999. [https://pubmed.ncbi.nlm.nih.gov/10501053/](https://pubmed.ncbi.nlm.nih.gov/10501053/)

9. **Jee, Y.S. et al.** "Negative threshold voltage shift in an a-IGZO TFT under X-ray irradiation." *RSC Advances*, 2019. [https://pmc.ncbi.nlm.nih.gov/articles/PMC9065737/](https://pmc.ncbi.nlm.nih.gov/articles/PMC9065737/)

10. **Badal, A. et al.** "Comparison of CsI:Tl and Gd₂O₂S:Tb indirect flat panel detector x-ray imaging." *Medical Physics*, 2019. [https://pmc.ncbi.nlm.nih.gov/articles/PMC6842040/](https://pmc.ncbi.nlm.nih.gov/articles/PMC6842040/)

11. **Zhao, W. et al.** "Direct-conversion x-ray detectors using amorphous selenium." *Journal of X-ray Science and Technology*, 2002. [https://journals.sagepub.com/doi/pdf/10.3233/XST-2002-00055](https://journals.sagepub.com/doi/pdf/10.3233/XST-2002-00055)

12. **Iacoviello, F.** "A comprehensive analysis of modern X-ray detectors." *LinkedIn*, 2025. [https://www.linkedin.com/pulse/comprehensive-analysis-modern-x-ray-detectors-francesco-iacoviello-py1sf](https://www.linkedin.com/pulse/comprehensive-analysis-modern-x-ray-detectors-francesco-iacoviello-py1sf)

13. **Substrate and Passivation Techniques for Flexible a-Si PIN Photodiodes.** *Sensors*, 2016. [https://pmc.ncbi.nlm.nih.gov/articles/PMC5017328/](https://pmc.ncbi.nlm.nih.gov/articles/PMC5017328/)

14. **Active pixel imagers incorporating pixel-level amplifiers.** *Medical Physics*, 2009. [https://pmc.ncbi.nlm.nih.gov/articles/PMC2805355/](https://pmc.ncbi.nlm.nih.gov/articles/PMC2805355/)

15. **Chang, M-K. et al.** "Review of Integrated Gate Driver Circuits in Active Matrix TFT Display Panels." *Micromachines*, 2024. [https://pmc.ncbi.nlm.nih.gov/articles/PMC11279033/](https://pmc.ncbi.nlm.nih.gov/articles/PMC11279033/)

16. **Modeling dark current conduction mechanisms in a-Se.** *ACS Applied Electronic Materials*, 2022. [https://pmc.ncbi.nlm.nih.gov/articles/PMC9119575/](https://pmc.ncbi.nlm.nih.gov/articles/PMC9119575/)

17. **Dose-response and ghosting effects of an a-Si EPID.** *Medical Physics*, 2004. [https://pubmed.ncbi.nlm.nih.gov/15000614/](https://pubmed.ncbi.nlm.nih.gov/15000614/)

18. **Dual-exposure technique for extending the dynamic range of x-ray flat panel detectors.** *Medical Physics*, 2014. [https://pubmed.ncbi.nlm.nih.gov/24352046/](https://pubmed.ncbi.nlm.nih.gov/24352046/)

19. **Radiopaedia: Flat Panel Detector.** [https://radiopaedia.org/articles/flat-panel-detector](https://radiopaedia.org/articles/flat-panel-detector)

20. **Spectrum X-Ray: Indirect vs. Direct Conversion DR Detectors.** [https://spectrumxray.com/indirect-vs-direct-conversion-dr-detectors-which-technology-delivers-better-image-quality/](https://spectrumxray.com/indirect-vs-direct-conversion-dr-detectors-which-technology-delivers-better-image-quality/)

21. **Enhancing Discharge Performance and Image Lag in IGZO-TFT FPDs.** *Sensors*, 2026. [https://pmc.ncbi.nlm.nih.gov/articles/PMC12899452/](https://pmc.ncbi.nlm.nih.gov/articles/PMC12899452/)

---

*End of research document.*  
*File: /home/user/workspace/research_01_asi_tft_physics.md*
