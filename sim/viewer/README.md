# FPD Simulation Viewer

X-ray Flat Panel Detector FPGA golden model simulation viewer.
C# WPF (`.NET 8`) application for real-time visual verification of panel driving, signal timing, data path state, and exported traces.

## Overview

The viewer ports 12 C++ golden models to C# and exposes a unified monitoring workflow.
Phase 2 to Phase 6 functionality is now reflected in the current UI:

- Operation Monitor with FSM pipeline, panel scan, and live signal scope on one screen
- Signal Scope with combo-aware channels, physical voltage rendering, cursor `A/B`, time zoom, and basic spec checks
- HW Setup tab for combo, panel, gate IC, AFE, and mode summary
- Parameters tab for register-backed physical timing and AFE controls
- Data Path tab for AFE -> LVDS -> ISERDES -> Line Buffer -> CSI-2 flow and pixel preview
- Verification tab for timing checks, FSM transition log, and CSV/VCD export

## Prerequisites

- .NET 8 SDK (`net8.0-windows`)
- Windows 10/11 (x64)
- Visual Studio 2022 (optional)

## Build

```powershell
cd sim/viewer
dotnet build FpdSimViewer.sln
```

## Test

```powershell
dotnet test tests/FpdSimViewer.Tests/FpdSimViewer.Tests.csproj
```

Current: **77 tests, 0 failures**

## Run

```powershell
dotnet run --project src/FpdSimViewer/FpdSimViewer.csproj
```

Or open `FpdSimViewer.sln` in Visual Studio and press `F5`.

## Quick Start

1. Launch the application.
2. Select hardware combo `C1` to `C7` from the top toolbar.
3. Select an operating mode.
4. Click `Play` or press `Space`.
5. Watch the unified monitor update in real time while using the right-side tabs for setup, parameters, registers, data path, and verification.
6. Use `Step` or `Right Arrow` for cycle-by-cycle inspection.

## Hardware Combinations

| Combo | Panel | Gate IC | AFE | Rows | Cols | AFE Chips |
|-------|-------|---------|-----|------|------|-----------|
| C1 | 2048 x 2048 Panel | NV1047 | AD71124 | 2048 | 2048 | 1 |
| C2 | 2048 x 2048 Panel | NV1047 | AD71143 | 2048 | 2048 | 1 |
| C3 | 2048 x 2048 Panel | NV1047 | AFE2256 | 2048 | 2048 | 1 |
| C4 | 2048 x 1664 Panel | NV1047 | AD71124 | 2048 | 1664 | 1 |
| C5 | 2048 x 1664 Panel | NV1047 | AFE2256 | 2048 | 1664 | 1 |
| C6 | 3072 x 3072 Panel | NT39565D | AD71124 | 3072 | 3072 | 12 |
| C7 | 3072 x 3072 Panel | NT39565D | AFE2256 | 3072 | 3072 | 12 |

## Operating Modes

| Mode | REG_MODE | Description |
|------|----------|-------------|
| STATIC | `0x000` | Single frame acquisition |
| CONTINUOUS | `0x001` | Auto-repeat readout loop |
| TRIGGERED | `0x002` | Wait for X-ray handshake |
| DARK_FRAME | `0x003` | Gate off, AFE readout only |
| RESET_ONLY | `0x004` | Panel reset without readout |

## Current Layout

```text
+----------------------------------------------------------------------------------+
| Top: SimControlBar                                                               |
+----------------------------------------------------------------------------------+
| Left 65%: Operation Monitor                        | Right 35%: Config Tabs      |
|  - FSM State Pipeline                              |  - HW Setup                  |
|  - Panel Scan + Pixel Heatmap                      |  - Parameters                |
|  - Signal Scope                                    |  - Registers                 |
|                                                    |  - Data Path                 |
|                                                    |  - Verification              |
+----------------------------------------------------------------------------------+
| Bottom: StatusBarControl                                                          |
+----------------------------------------------------------------------------------+
```

## Key Features

### Operation Monitor

- FSM pipeline with current, completed, and pending states
- Panel cross-section and row progress summary
- Current-row heatmap and pixel statistics
- Oscilloscope-style scope with actual voltages instead of boolean traces

### Signal Scope

- Combo-aware channel sets for `NV1047` and `NT39565D`
- AFE-aware channel switching for `AD711xx` and `AFE2256`
- Mouse-wheel time zoom from `1 us/div` to `10 ms/div`
- Cursor `A/B` delta-time measurement
- Per-channel visibility toggles and basic PASS/FAIL checks

### Right-side Tabs

- `HW Setup`: combo and hardware summary
- `Parameters`: physical timing controls mapped back to registers
- `Registers`: existing register editor panel
- `Data Path`: staged AFE/LVDS/ISERDES/line buffer/CSI-2 view
- `Verification`: timing checks, FSM event log, CSV/VCD export

## Export

- `Export CSV`: writes recorded trace snapshots with key timing and voltage fields
- `Export VCD`: writes a VCD trace for core digital state transitions captured in `TraceCapture`

## Ported Models

The simulation engine executes 12 golden-model ports in dependency order:

```text
RegBank -> ClkRst -> PowerSeq -> EmergencyShutdown ->
PanelFsm -> RowScan -> GateDriver -> AfeModel ->
Radiog -> ProtMon -> Snapshot capture
```

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Space` | Play / Pause |
| `Right Arrow` | Step 1 cycle |
| `Home` | Reset simulation |

## Project Structure

```text
sim/viewer/
  FpdSimViewer.sln
  src/FpdSimViewer/
    Engine/               SimulationEngine, snapshots, trace/export helpers
    Models/               12 golden model C# ports
    ViewModels/           Operation monitor, setup, parameters, data path, verification
    Views/Controls/       Shared controls
    Views/Tabs/           HW Setup, Data Path, Verification, legacy tab references
    Views/Drawing/        Scope renderer
    Resources/            Colors.xaml, Styles.xaml
  tests/FpdSimViewer.Tests/
    Models/               Per-model tests
    Engine/               Engine and integration tests
    ViewModels/           Data Path, Verification, Operation Monitor, Physical Parameters
```

## References

- GUI spec: `.moai/specs/SPEC-FPD-GUI-002/`
- Legacy viewer spec: `.moai/specs/SPEC-FPD-GUI-001/`
- C++ golden models: `sim/golden_models/models/`
- User guide: [USER_GUIDE.md](USER_GUIDE.md)
