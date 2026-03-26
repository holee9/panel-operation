# SPEC-FPD-GUI-001: Acceptance Criteria

## Core Functionality

### AC-GUI-001: Combo Selection
- GIVEN the application is running
- WHEN the user selects combo C1-C7
- THEN the simulation reconfigures with correct panel size, gate IC, and AFE model
- AND the register editor shows combo-specific defaults
- AND TLINE is clamped to combo minimum (FR-001, FR-011)

### AC-GUI-002: Simulation Control
- GIVEN models are initialized
- WHEN the user clicks Play/Pause/Step/Reset
- THEN the simulation responds within 1 frame (16ms)
- AND Step advances exactly 1 cycle
- AND Reset returns all models to initial state (FR-002, FR-003, FR-004)

### AC-GUI-003: Register Edit
- GIVEN the register editor is visible
- WHEN the user modifies a R/W register value
- THEN the change applies on the next simulation cycle
- AND read-only registers are not editable (FR-005)

### AC-GUI-004: Speed Control
- GIVEN the simulation is running
- WHEN the user adjusts speed from 1x to 1000x
- THEN the simulation rate changes accordingly
- AND the UI maintains 60 FPS (NFR-001)

## Tab A: Panel Row Scan

### AC-GUI-005: Row Visualization
- GIVEN Tab A is active and simulation is in READOUT state
- WHEN rows are being scanned
- THEN each row shows correct color (pending/gate_on/settle/afe_read/scanned)
- AND the current row is auto-scrolled to viewport center (FR-006)

### AC-GUI-006: Gate IC Signals
- GIVEN Tab A signal monitor is visible
- WHEN gate_on_pulse toggles
- THEN NV1047 signals (SD1/SD2/CLK/OE) or NT39565D signals (STV/OE1/OE2) update in mini waveform
- AND break-before-make gap is visible for NV1047 (FR-015)

### AC-GUI-007: Multi-AFE Status (C6/C7)
- GIVEN combo C6 or C7 is selected
- WHEN simulation is in READOUT state
- THEN 12-AFE status grid shows per-AFE indicators (FR-014)

## Tab B: FSM State Diagram

### AC-GUI-008: State Highlight
- GIVEN Tab B is active
- WHEN FSM transitions to a new state
- THEN the new state node is highlighted
- AND the previous state dims (FR-007)

### AC-GUI-009: Transition History
- GIVEN Tab B history panel is visible
- WHEN FSM transitions occur
- THEN each transition is logged with cycle number and condition
- AND state dwell time is displayed (FR-019)

## Tab C: Imaging Cycle Timeline

### AC-GUI-010: Phase Bar
- GIVEN Tab C is active and a full cycle has run
- THEN phase bar shows color-coded sections proportional to time
- AND IDLE/RESET/INTEGRATE/READOUT/DONE phases are labeled (FR-008)

### AC-GUI-011: Timing Diagram
- GIVEN Tab C is active
- WHEN simulation is running
- THEN 6+ signal traces are rendered as waveforms
- AND horizontal scroll and zoom work (FR-008)

### AC-GUI-012: Radiography Handshake
- GIVEN TRIGGERED mode is selected
- WHEN simulation passes through PREP/XRAY states
- THEN PREP_REQ, XRAY_ON, XRAY_OFF signals appear on timeline (FR-012)

### AC-GUI-013: Settle Gap
- GIVEN simulation is in SETTLE state
- THEN settle time is shown as a gap overlay on timeline (FR-013)

### AC-GUI-014: Power Rail Timeline
- GIVEN power sequencing is active
- THEN VGL/VGH/AVDD rails show on timeline with stability indicators (FR-017)

## Spec Coverage

### AC-GUI-015: Dark Frame Panel
- GIVEN DARK_FRAME mode is selected
- WHEN dark frames are captured
- THEN accumulator panel shows frame count and average progress (FR-018)

### AC-GUI-016: CIC Indicator
- GIVEN AFE2256 is selected (C3/C5/C7)
- THEN CIC compensation status is shown (FR-020)

### AC-GUI-017: Requirement Status
- GIVEN the requirement status panel is visible
- THEN R-SIM-041~052 and AC-SIM-035~047 show PASS/FAIL badges (NFR-005)

## Performance

### AC-GUI-018: Large Panel Performance
- GIVEN combo C6 (3072 rows) is selected
- WHEN simulation runs at 1000x speed
- THEN UI maintains 60 FPS (NFR-001)

### AC-GUI-019: Model Accuracy
- GIVEN C# models are initialized with identical inputs as C++ tests
- WHEN the same step sequence is executed
- THEN outputs are bit-identical to C++ golden model outputs (NFR-002)

## Error Handling

### AC-GUI-020: ProtMon Error
- GIVEN simulation is running
- WHEN ProtMon timeout fires
- THEN all tabs show ERROR indication
- AND FSM diagram highlights ERROR state (FR-010)
