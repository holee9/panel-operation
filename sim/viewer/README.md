# FPD Simulation Viewer

X-ray Flat Panel Detector FPGA golden model simulation viewer.
C# WPF (.NET 8) application for visual verification of panel driving algorithms.

## Overview

This viewer ports 12 C++ golden models to C# and provides a real-time GUI
to observe FSM state transitions, row scanning, timing diagrams, and register
values as the simulation runs cycle-by-cycle.

## Prerequisites

- .NET 8 SDK (net8.0-windows)
- Windows 10/11 (x64)
- Visual Studio 2022 (optional, for IDE debugging)

## Build

```powershell
cd sim/viewer
dotnet build FpdSimViewer.sln
```

## Test

```powershell
dotnet test tests/FpdSimViewer.Tests/FpdSimViewer.Tests.csproj
```

Current: **64 tests, 0 failures**

## Run

```powershell
dotnet run --project src/FpdSimViewer/FpdSimViewer.csproj
```

Or open `FpdSimViewer.sln` in Visual Studio and press F5.

## Quick Start

1. Launch the application
2. Select a hardware combo (C1-C7) from the top toolbar
3. Select an operating mode (STATIC by default)
4. Click **Play** (or press Space) to start simulation
5. Observe the three tabs updating in real-time
6. Click **Step** (or press Right arrow) for single-cycle stepping

## Hardware Combinations

| Combo | Panel | Gate IC | AFE | Rows | Cols | AFE Chips |
|-------|-------|---------|-----|------|------|-----------|
| C1 | R1717 (17x17") | NV1047 | AD71124 | 2048 | 2048 | 1 |
| C2 | R1717 | NV1047 | AD71143 | 2048 | 2048 | 1 |
| C3 | R1717 | NV1047 | AFE2256 | 2048 | 2048 | 1 |
| C4 | R1714 (17x14") | NV1047 | AD71124 | 2048 | 1664 | 1 |
| C5 | R1714 | NV1047 | AFE2256 | 2048 | 1664 | 1 |
| C6 | X239AW1 (43x43cm) | NT39565D | AD71124 | 3072 | 3072 | 12 |
| C7 | X239AW1 | NT39565D | AFE2256 | 3072 | 3072 | 12 |

## Operating Modes

| Mode | REG_MODE | Description |
|------|----------|-------------|
| STATIC | 0x000 | Single frame acquisition |
| CONTINUOUS | 0x001 | Auto-repeat readout loop |
| TRIGGERED | 0x002 | Wait for X-ray generator handshake |
| DARK_FRAME | 0x003 | Gate off, AFE readout only (calibration) |
| RESET_ONLY | 0x004 | Panel reset without readout |

## GUI Layout

```
+------------------------------------------------------------------+
| Toolbar: [Combo] [Mode] [Reset|Step|Play/Pause] [Speed]          |
|          [Power: target/vgl/vgh] [Faults] [X-ray handshake]      |
+------------------------------------------------------------------+
| Tab: Panel Scan | Tab: FSM Diagram | Tab: Imaging Cycle          |
|------------------------------------------------------------------|
|                     [Selected Tab Content]                        |
|           RegisterEditor (right dock)                             |
+------------------------------------------------------------------+
| Status: State | Row | Cycle | Elapsed Time                      |
+------------------------------------------------------------------+
```

### Tab A: Panel Scan

Row-by-row visualization of the panel readout process:
- Color-coded rows: pending (gray), gate ON (blue), settle (orange), scanned (green)
- Gate IC output signals (NV1047 or NT39565D depending on combo)
- AFE status indicators per chip

### Tab B: FSM Diagram

State machine visualization with 12 nodes:
- States: IDLE, POWER_CHECK, RESET, WAIT_PREP, BIAS_STAB, XRAY_INTEG, CONFIG_AFE, READOUT, SETTLE, FRAME_DONE, DONE, ERROR
- Current state highlighted, transition history log

### Tab C: Imaging Cycle

Timeline visualization of a full imaging cycle:
- Phase bar showing IDLE/RESET/INTEGRATE/READOUT/DONE proportions
- Signal traces: GateOn, AfeValid, PowerGood, ProtErr
- Row progress bar

## Ported Models (12)

| Model | C++ Source | C# Port |
|-------|-----------|---------|
| RegBankModel | 32-register file with TLINE clamping | Models/RegBankModel.cs |
| PanelFsmModel | 11-state FSM + ERROR state | Models/PanelFsmModel.cs |
| RowScanModel | Row counter with gate timing | Models/RowScanModel.cs |
| GateNv1047Model | NV1047: BBM, SD shift, CLK div | Models/GateNv1047Model.cs |
| GateNt39565dModel | NT39565D: dual STV, split OE | Models/GateNt39565dModel.cs |
| AfeAd711xxModel | AD71124/AD71143: config, sample | Models/AfeAd711xxModel.cs |
| AfeAfe2256Model | AFE2256: CIC filter, pipeline | Models/AfeAfe2256Model.cs |
| ProtMonModel | Dual timeout (500K/3M cycles) | Models/ProtMonModel.cs |
| ClkRstModel | Phase acc ACLK/MCLK, PLL lock | Models/ClkRstModel.cs |
| PowerSeqModel | VGL-before-VGH sequencing | Models/PowerSeqModel.cs |
| EmergencyShutdownModel | 5-fault priority OR | Models/EmergencyShutdownModel.cs |
| RadiogModel | Dark frame averaging FSM | Models/RadiogModel.cs |

## Simulation Engine

The engine executes all 12 models in dependency order each cycle:

```
RegBank -> ClkRst -> PowerSeq -> EmergencyShutdown ->
PanelFsm -> RowScan -> GateDriver -> AfeModel ->
Radiog -> ProtMon -> Snapshot capture
```

Speed control: 1x to 1000x (DispatcherTimer at 60 FPS)

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| Space | Play / Pause |
| Right Arrow | Step (1 cycle) |
| Home | Reset simulation |

## Project Structure

```
sim/viewer/
  FpdSimViewer.sln
  src/FpdSimViewer/
    Models/Core/          SignalTypes, GoldenModelBase, FoundationConstants
    Models/               12 golden model C# ports
    Engine/               SimulationEngine, HardwareComboConfig, TraceCapture
    ViewModels/           MVVM ViewModels (CommunityToolkit.Mvvm)
    Views/Controls/       SimControlBar, RegisterEditor, StatusBar
    Views/Tabs/           PanelScanTab, FsmDiagramTab, ImagingCycleTab
    Views/Drawing/        PanelGridRenderer, FsmGraphRenderer, TimingDiagramRenderer
    Resources/            Colors.xaml, Styles.xaml
    Converters/           FsmStateToColor, BoolToVisibility
  tests/FpdSimViewer.Tests/
    Models/               Per-model unit tests
    Engine/               Engine integration + C++ cross-validation tests
```

## SPEC Reference

- SPEC: `.moai/specs/SPEC-FPD-GUI-001/`
- C++ golden models: `sim/golden_models/models/`
- Architecture: `docs/fpga-design/fpga_module_architecture.md`
- Driving algorithm: `docs/fpga-design/fpga-xray-detector-driving-algorithm.pplx.md`
