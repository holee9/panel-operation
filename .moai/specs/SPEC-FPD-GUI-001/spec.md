# SPEC-FPD-GUI-001: FPD Simulation Viewer

> **Version**: 1.0.0
> **Status**: Draft
> **Created**: 2026-03-24
> **Target**: C# 12 / .NET 8 WPF, Visual Studio 2022 Professional
> **Location**: `sim/viewer/`
> **Related**: SPEC-FPD-SIM-001 (golden models), ECR-001 (v1.2.0 spec revision)

---

## 1. Overview

### 1.1 Purpose

X-ray Flat Panel Detector FPGA project golden model operation visual verification GUI application.

### 1.2 Problem Statement

Current simulation verification relies solely on CTest PASS/FAIL and console output. There is no way to visually confirm panel driving algorithm timing, state transitions, and row scan sequences.

### 1.3 Solution

Port 12 C++ golden models to C# and provide real-time simulation visualization via WPF MVVM 3-tab GUI.

### 1.4 Scope

- **In Scope**: Panel driving algorithm visualization, FSM state verification, timing diagrams, register editing, combo validation, radiography timeline, gate cascade, multi-AFE status, power rail sequence, dark-frame/CIC comparison
- **Out of Scope**: RTL synthesis, physical layer simulation (LVDS, CSI-2 packets), actual hardware connection

### 1.5 Technology Decision

C# WPF was chosen over C++ Dear ImGui because:
1. VS2022 Professional is already installed
2. WPF data binding is optimal for real-time register display
3. MVVM pattern provides clean separation of model logic and visualization
4. Native Windows rendering with hardware acceleration
5. .exe deployment without additional dependencies

---

## 2. Requirements (EARS Format)

### 2.1 Core Functional Requirements

**FR-001**: When the user selects a hardware combo (C1-C7), the system shall configure the simulation with the corresponding panel size, gate IC driver, and AFE model.

**FR-002**: When the user clicks Play, the system shall execute the simulation loop at the selected speed (1x~1000x) and update all visual elements in real-time.

**FR-003**: When the user clicks Step, the system shall advance exactly 1 clock cycle and update all visual elements.

**FR-004**: When the user clicks Reset, the system shall reset all models to initial state.

**FR-005**: When the user modifies a register value in the Register Editor, the system shall apply the change to RegBankModel on the next cycle.

**FR-006** (Tab A - Panel Row Scan): The system shall display:
- Panel grid with per-row color coding (pending/gate_on/settle/afe_read/scanned)
- Current active row highlighted and auto-scrolled to viewport center
- Gate IC output signals as mini waveform (last 32 cycles)
- AFE status indicators (Ready/Converting/Valid)

**FR-007** (Tab B - FSM State Diagram): The system shall display:
- All FSM states as nodes in flowchart layout
- Current state highlighted with distinct color/border
- Transition arrows with condition labels
- State transition history log with cycle numbers

**FR-008** (Tab C - Imaging Cycle Timeline): The system shall display:
- Phase bar with color-coded sections (IDLE/RESET/INTEGRATE/READOUT/DONE)
- Timing diagram with 6+ signal traces
- Horizontal scroll and zoom controls
- Progress bar (current_row / total_rows)

**FR-009**: The system shall support 5 operating modes: STATIC, CONTINUOUS, TRIGGERED, DARK_FRAME, RESET_ONLY.

**FR-010**: When ProtMon detects over-exposure timeout, the system shall visually indicate ERROR state in all tabs.

### 2.2 SPEC v1.2.0 Coverage Requirements (R-SIM-041~052)

These requirements from SPEC-FPD-SIM-001 ECR-001 drive additional GUI features:

**FR-011** (R-SIM-041, AC-SIM-035/036): The system shall display combo-specific TLINE_MIN and NCOLS constraints with validation badges and auto-correction indicators in the register editor.

**FR-012** (R-SIM-042, AC-SIM-037): The system shall display radiography generator handshake timeline with PREP_REQ, XRAY_ON, XRAY_OFF signals and timeout counters.

**FR-013** (R-SIM-043, AC-SIM-038): The system shall display settle time delay as a gap overlay in the imaging cycle timeline.

**FR-014** (R-SIM-044, AC-SIM-039/021): For C6/C7 combos, the system shall display 12-AFE status grid with per-AFE status indicators and line assembly progress.

**FR-015** (R-SIM-046, AC-SIM-040): The system shall display NV1047 shift register state and break-before-make timing in the gate waveform pane.

**FR-016** (R-SIM-047, AC-SIM-041/022): The system shall display NT39565D dual-STV cascade view with 6-chip propagation strip and odd/even row tracking.

**FR-017** (R-SIM-049, AC-SIM-043): The system shall display VGL/VGH power rail timeline with sequencing and stability indicators.

**FR-018** (R-SIM-050, AC-SIM-044): The system shall display dark frame accumulator panel with frame count and offset-map preview.

**FR-019** (R-SIM-051, AC-SIM-045/047): The system shall display extended FSM state dwell counters and config-driven duration table.

**FR-020** (R-SIM-052, AC-SIM-046): The system shall display CIC compensation profile selector with pre/post compensation indicators.

### 2.3 Non-Functional Requirements

**NFR-001**: The GUI shall maintain 60 FPS at 1000x simulation speed with C6 combo (3072 rows).

**NFR-002**: C# model outputs shall be bit-accurate to the C++ golden model outputs for identical input sequences.

**NFR-003**: The application shall start within 3 seconds on a typical development machine.

**NFR-004**: The application shall support Windows 10/11 (x64).

**NFR-005**: The application shall track SPEC requirement status with per-requirement PASS/FAIL badges.

---

## 3. Architecture

### 3.1 Technology Stack

| Component | Technology |
|-----------|-----------|
| Language | C# 12 |
| Framework | .NET 8 WPF |
| IDE | Visual Studio 2022 Professional |
| Pattern | MVVM (CommunityToolkit.Mvvm 8.x) |
| Testing | xUnit + FluentAssertions |
| Rendering | WriteableBitmap, DrawingVisual, StreamGeometry |

### 3.2 Layer Architecture

```
+--------------------------------------+
|           Views (XAML)               |  UI Layer
|  MainWindow, 3 Tabs, Controls       |
+--------------------------------------+
|         ViewModels (C#)              |  Presentation Layer
|  MVVM bindings, commands, timer      |
+--------------------------------------+
|      Engine (SimulationEngine)       |  Orchestration Layer
|  Model wiring, step loop, snapshot   |
+--------------------------------------+
|         Models (C# ports)            |  Domain Layer
|  12 golden models, SignalTypes       |
+--------------------------------------+
```

### 3.3 Solution Structure

```
sim/viewer/
  FpdSimViewer.sln

  src/FpdSimViewer/                        (.NET 8 WPF App)
    FpdSimViewer.csproj
    App.xaml / App.xaml.cs
    MainWindow.xaml / MainWindow.xaml.cs

    Models/
      Core/
        SignalTypes.cs                      SignalValue variant, SignalMap dictionary
        GoldenModelBase.cs                  abstract Reset/Step/SetInputs/GetOutputs
        FoundationConstants.cs              kReg* addresses, combo defaults, MinTLine
      RegBankModel.cs                       32 registers, R/W, TLINE clamping
      PanelFsmModel.cs                      11-state FSM + ERROR (state 15)
      RowScanModel.cs                       Row counter, gate ON/settle/done pulses
      GateNv1047Model.cs                    SD1/SD2 shift, CLK, OE, BBM timing
      GateNt39565dModel.cs                  Dual STV, split OE1/OE2, cascade
      AfeAd711xxModel.cs                    AD71124/AD71143 config + convert
      AfeAfe2256Model.cs                    AFE2256 CIC filter + pipeline
      ProtMonModel.cs                       Timeout 500K/3M cycles, error flags
      ClkRstModel.cs                        Phase acc ACLK/MCLK, PLL lock
      PowerSeqModel.cs                      VGL->VGH power sequencing
      EmergencyShutdownModel.cs             Fault OR, shutdown codes
      RadiogModel.cs                        Dark frame capture + averaging

    Engine/
      SimulationEngine.cs                   Model orchestrator, dependency-ordered step
      HardwareComboConfig.cs                C1-C7 factory (gate + AFE selection)
      SimulationSnapshot.cs                 Per-cycle immutable state capture
      TraceCapture.cs                       Cycle-by-cycle signal history buffer
      RequirementTracker.cs                 R-SIM/AC-SIM status tracking

    ViewModels/
      MainViewModel.cs                      Tab coordination, global state
      SimControlViewModel.cs                Play/Pause/Step/Reset, speed slider
      RegisterEditorViewModel.cs            Register grid, edit command
      PanelScanViewModel.cs                 Tab A: row states, gate/AFE signals
      FsmDiagramViewModel.cs                Tab B: current state, transition history
      ImagingCycleViewModel.cs              Tab C: signal traces, phase bars

    Views/
      Controls/
        SimControlBar.xaml                  Transport buttons + speed slider
        RegisterEditorPanel.xaml             Editable DataGrid (collapsible)
        ComboModeSelector.xaml              C1-C7 dropdown + mode dropdown
        StatusBar.xaml                       FSM state, row, cycle, time
        RequirementStatusPanel.xaml          R-SIM/AC-SIM PASS/FAIL badges
      Tabs/
        PanelScanTab.xaml                   Tab A: panel grid + signal monitor
        FsmDiagramTab.xaml                  Tab B: state diagram + history
        ImagingCycleTab.xaml                Tab C: phase bar + timing diagram
      Drawing/
        PanelGridRenderer.cs                WriteableBitmap row visualization
        FsmGraphRenderer.cs                 Canvas state node/edge rendering
        TimingDiagramRenderer.cs            StreamGeometry waveform rendering

    Converters/
      FsmStateToColorConverter.cs
      BoolToVisibilityConverter.cs
      SignalValueToStringConverter.cs

    Resources/
      Colors.xaml                           Color palette
      Styles.xaml                           Global styles

  tests/FpdSimViewer.Tests/                 (xUnit)
    FpdSimViewer.Tests.csproj
    Models/
      RegBankModelTests.cs
      PanelFsmModelTests.cs
      GateNv1047ModelTests.cs
      SimulationEngineTests.cs
```

---

## 4. Model Porting Specification

### 4.1 Porting Scope

12 models ported (algorithm visualization only):

| # | C# Model | C++ Source | Role |
|---|----------|-----------|------|
| 1 | RegBankModel | sim/golden_models/models/RegBankModel.h/cpp | 32-register file, combo defaults, TLINE clamp |
| 2 | PanelFsmModel | sim/golden_models/models/PanelFsmModel.h/cpp | 11-state FSM, 5 operating modes |
| 3 | RowScanModel | sim/golden_models/models/RowScanModel.h/cpp | Row index, gate_on_pulse, row_done |
| 4 | GateNv1047Model | sim/golden_models/models/GateNv1047Model.h/cpp | BBM, SD1/SD2, CLK div, OE |
| 5 | GateNt39565dModel | sim/golden_models/models/GateNt39565dModel.h/cpp | Dual STV, OE1/OE2 split |
| 6 | AfeAd711xxModel | sim/golden_models/models/AfeAd711xxModel.h/cpp | Config handshake, dout_valid |
| 7 | AfeAfe2256Model | sim/golden_models/models/AfeAfe2256Model.h/cpp | CIC filter, pipeline latency |
| 8 | ProtMonModel | sim/golden_models/models/ProtMonModel.h/cpp | Timeout 5s/30s, force_gate_off |
| 9 | ClkRstModel | sim/golden_models/models/ClkRstModel.h/cpp | Phase acc, PLL lock detect |
| 10 | PowerSeqModel | sim/golden_models/models/PowerSeqModel.h/cpp | VGL->VGH rail sequencing |
| 11 | EmergencyShutdownModel | sim/golden_models/models/EmergencyShutdownModel.h/cpp | Fault OR, shutdown_req |
| 12 | RadiogModel | sim/golden_models/models/RadiogModel.h/cpp | Dark frame capture, averaging |

### 4.2 Excluded Models

SpiSlaveModel, LvdsRxModel, McuDataIfModel, Csi2PacketModel, Csi2LaneDistModel, DataOutMuxModel, LineBufModel, AfeSpiMasterModel (physical layer, not needed for algorithm visualization)

### 4.3 Core Type Mapping

| C++ | C# |
|-----|-----|
| variant<uint32_t, vector<uint16_t>> | readonly record struct SignalValue |
| map<string, SignalValue> | Dictionary<string, SignalValue> |
| GoldenModelBase (abstract) | abstract class GoldenModelBase |
| struct Mismatch | record Mismatch(ulong Cycle, string Signal, uint Expected, uint Actual) |

### 4.4 Porting Rules

1. step() logic ported 1:1 from C++ (bit-accurate)
2. compare(), generate_vectors() excluded (test-only, not needed for viewer)
3. C++ uint32_t -> C# uint, uint16_t -> ushort, uint64_t -> ulong
4. C++ std::vector<uint16_t> -> C# ushort[]
5. Namespace: fpd::sim -> FpdSimViewer.Models

---

## 5. Hardware Combo Configuration

| Combo | Panel | Rows | Cols | Gate IC | AFE | AFE Chips | Min TLINE |
|-------|-------|------|------|---------|-----|-----------|-----------|
| C1 | R1717 | 2048 | 2048 | NV1047 | AD71124 | 1 | 2200 (22us) |
| C2 | R1717 | 2048 | 2048 | NV1047 | AD71143 | 1 | 6000 (60us) |
| C3 | R1717 | 2048 | 2048 | NV1047 | AFE2256 | 1 | 2200 (22us) |
| C4 | R1714 | 2048 | 1664 | NV1047 | AD71124 | 1 | 2200 (22us) |
| C5 | R1714 | 2048 | 1664 | NV1047 | AFE2256 | 1 | 2200 (22us) |
| C6 | X239AW1 | 3072 | 3072 | NT39565D | AD71124 | 12 | 2200 (22us) |
| C7 | X239AW1 | 3072 | 3072 | NT39565D | AFE2256 | 12 | 2200 (22us) |

---

## 6. Simulation Engine

### 6.1 Step Execution Order (per cycle)

```
 1. RegBankModel.Step()           -> config register outputs
 2. ClkRstModel.Step()            -> clock enables, PLL lock
 3. PowerSeqModel.Step()          -> power_good, rail enables
 4. EmergencyShutdownModel.Step() -> shutdown_req check
 5. PanelFsmModel.SetInputs(config + feedback)
 6. PanelFsmModel.Step()          -> FSM state, control signals
 7. RowScanModel.SetInputs(from FSM)
 8. RowScanModel.Step()           -> row_index, gate_on_pulse, row_done
 9. GateDriver.SetInputs(from RowScan)
10. GateDriver.Step()             -> IC output signals
11. AfeModel.SetInputs(from FSM + config)
12. AfeModel.Step()               -> ready, dout_valid, line_count
13. ProtMonModel.SetInputs(fsm + xray)
14. ProtMonModel.Step()           -> timeout, force_gate_off
15. TraceCapture.Record()         -> append to signal history
16. RequirementTracker.Evaluate() -> update PASS/FAIL status
17. SimulationSnapshot.Capture()  -> immutable state for UI binding
```

### 6.2 Speed Control

DispatcherTimer interval: 16ms (~60 FPS)

| Speed | Steps/tick | Effective rate |
|-------|-----------|----------------|
| 1x | 1 | 60 steps/s |
| 10x | 10 | 600 steps/s |
| 100x | 100 | 6K steps/s |
| 1000x | 1000 | 60K steps/s |

---

## 7. FSM State Definition

| State | ID | Name | Description |
|-------|----|------|-------------|
| S0 | 0 | IDLE | Waiting for start command |
| S1 | 1 | POWER_CHECK | Verify power rails stable |
| S2 | 2 | RESET | Dummy scans (REG_NRESET times) |
| S3 | 3 | WAIT_PREP | Wait for X-ray PREP_REQ |
| S4 | 4 | BIAS_STAB | Forward bias stabilization |
| S5 | 5 | XRAY_ENABLE | Signal generator ready |
| S6 | 6 | INTEGRATION | X-ray exposure window |
| S7 | 7 | SETTLE | Charge redistribution |
| S8 | 8 | READOUT | Sequential row scan + AFE read |
| S9 | 9 | POST | Post-stabilization |
| S10 | 10 | DONE | Frame complete |
| S15 | 15 | ERROR | Protection triggered |

---

## 8. Implementation Phases

### Phase 1: Core Models
- Solution scaffolding (sim/viewer/)
- SignalTypes.cs, GoldenModelBase.cs, FoundationConstants.cs
- RegBankModel.cs, PanelFsmModel.cs, RowScanModel.cs
- xUnit tests vs C++ test vectors

### Phase 2: Peripheral Models
- Gate models (NV1047, NT39565D)
- AFE models (AD711xx, AFE2256)
- Safety models (ProtMon, ClkRst, PowerSeq, EmergencyShutdown, Radiog)

### Phase 3: Engine + UI Shell
- SimulationEngine, HardwareComboConfig, SimulationSnapshot
- TraceCapture, RequirementTracker
- MainWindow shell, SimControlBar, StatusBar

### Phase 4: Tab A - Panel Scan Animation
- PanelGridRenderer (WriteableBitmap)
- Gate IC mini waveform, AFE LED indicators
- Multi-AFE grid for C6/C7 (FR-014)

### Phase 5: Tab B - FSM State Diagram
- FsmGraphRenderer (node layout)
- State transition history, dwell counters (FR-019)
- Animation on state change

### Phase 6: Tab C - Imaging Cycle Timeline
- TimingDiagramRenderer (StreamGeometry)
- Phase bar, scroll/zoom
- Radiography handshake timeline (FR-012)
- Settle gap overlay (FR-013)
- Power rail timeline (FR-017)

### Phase 7: Spec Coverage Views
- RegisterEditorPanel with validation badges (FR-011)
- RequirementStatusPanel (FR-020, NFR-005)
- CIC profile selector (FR-020)
- Dark frame accumulator panel (FR-018)

### Phase 8: Validation
- C# vs C++ output cross-validation
- Full STATIC cycle (2048 rows)
- C6/C7 performance test (3072 rows, 60 FPS @ 1000x)

---

## 9. Critical Reference Files

| File | Purpose |
|------|---------|
| sim/golden_models/core/SignalTypes.h | Type system reference |
| sim/golden_models/core/GoldenModelBase.h | Base class interface |
| sim/golden_models/models/FoundationConstants.h | Register map, combo constants |
| sim/golden_models/models/PanelFsmModel.h/cpp | FSM logic (most critical) |
| sim/golden_models/models/RegBankModel.h/cpp | Register file logic |
| sim/golden_models/models/RowScanModel.h/cpp | Row scan engine |
| sim/golden_models/models/GateNv1047Model.h/cpp | NV1047 driver |
| sim/golden_models/models/GateNt39565dModel.h/cpp | NT39565D driver |
| sim/golden_models/models/AfeAd711xxModel.h/cpp | ADI AFE |
| sim/golden_models/models/AfeAfe2256Model.h/cpp | TI AFE |
| sim/golden_models/models/ProtMonModel.h/cpp | Protection monitor |
| sim/tests/test_panel_fsm.cpp | FSM test reference |
| sim/tests/test_reg_bank.cpp | RegBank test reference |
| .moai/specs/SPEC-FPD-SIM-001/spec.md | Simulation spec |
| .moai/specs/SPEC-FPD-SIM-001/ECR-001.md | v1.2.0 revision |
| docs/review/sw-sim-gui-coding-prep.md | Codex coding prep |

---

## 10. Dependencies

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
<!-- Test project -->
<PackageReference Include="xunit" Version="2.*" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
<PackageReference Include="FluentAssertions" Version="7.*" />
```
