# SPEC-FPD-GUI-001: Implementation Plan

## Coding Start Order

This document defines the exact file creation order for implementation.
Each phase must be completed before proceeding to the next.

---

## Phase 1: Project Scaffolding + Core Types

### Files to create:

1. **sim/viewer/FpdSimViewer.sln** - Solution file (.NET 8)
2. **sim/viewer/src/FpdSimViewer/FpdSimViewer.csproj** - WPF project, net8.0-windows
3. **sim/viewer/tests/FpdSimViewer.Tests/FpdSimViewer.Tests.csproj** - xUnit test project

4. **sim/viewer/src/FpdSimViewer/Models/Core/SignalTypes.cs**
   - Port from: sim/golden_models/core/SignalTypes.h
   - Contains: SignalValue (readonly record struct), SignalMap (Dictionary alias), Mismatch record
   - Key: SignalValue wraps uint (scalar) or ushort[] (vector)

5. **sim/viewer/src/FpdSimViewer/Models/Core/GoldenModelBase.cs**
   - Port from: sim/golden_models/core/GoldenModelBase.h
   - Contains: abstract Reset(), Step(), SetInputs(), GetOutputs(), CycleCount property
   - Skip: compare(), generate_vectors() (not needed for viewer)

6. **sim/viewer/src/FpdSimViewer/Models/Core/FoundationConstants.cs**
   - Port from: sim/golden_models/models/FoundationConstants.h
   - Contains: kReg* constants, ComboDefaultNCols(), ComboMinTLine(), MakeDefaultRegisters(), IsReadOnlyRegister()

### Test:
7. **sim/viewer/tests/FpdSimViewer.Tests/Models/FoundationConstantsTests.cs**
   - Verify combo defaults match C++ values

---

## Phase 2: Core Models (FSM + Registers + Row Scan)

### Files to create:

8. **sim/viewer/src/FpdSimViewer/Models/RegBankModel.cs**
   - Port from: sim/golden_models/models/RegBankModel.h/cpp
   - 32-register file, Read()/Write(), combo defaults, TLINE clamping

9. **sim/viewer/src/FpdSimViewer/Models/PanelFsmModel.cs**
   - Port from: sim/golden_models/models/PanelFsmModel.h/cpp
   - 11-state FSM + ERROR (state 15), 5 operating modes
   - MOST CRITICAL FILE - port step() logic 1:1

10. **sim/viewer/src/FpdSimViewer/Models/RowScanModel.cs**
    - Port from: sim/golden_models/models/RowScanModel.h/cpp
    - Row counter, gate_on_pulse, gate_settle, row_done, scan_done

### Tests:
11. **sim/viewer/tests/FpdSimViewer.Tests/Models/RegBankModelTests.cs**
    - Test: defaults, TLINE clamping, combo switch, read-only protection

12. **sim/viewer/tests/FpdSimViewer.Tests/Models/PanelFsmModelTests.cs**
    - Test: STATIC cycle (IDLE->RESET->INTEG->READOUT->DONE), abort, error

---

## Phase 3: Gate IC + AFE Models

### Files to create:

13. **sim/viewer/src/FpdSimViewer/Models/GateNv1047Model.cs**
    - Port from: sim/golden_models/models/GateNv1047Model.h/cpp
    - BBM counter, SD1/SD2 shift, CLK divider, OE logic

14. **sim/viewer/src/FpdSimViewer/Models/GateNt39565dModel.cs**
    - Port from: sim/golden_models/models/GateNt39565dModel.h/cpp
    - Dual STV phase, split OE1/OE2, cascade complete

15. **sim/viewer/src/FpdSimViewer/Models/AfeAd711xxModel.cs**
    - Port from: sim/golden_models/models/AfeAd711xxModel.h/cpp
    - AD71124/AD71143 config, convert, sample line, tline_error

16. **sim/viewer/src/FpdSimViewer/Models/AfeAfe2256Model.cs**
    - Port from: sim/golden_models/models/AfeAfe2256Model.h/cpp
    - CIC filter, pipeline latency, FCLK

### Tests:
17. **sim/viewer/tests/FpdSimViewer.Tests/Models/GateNv1047ModelTests.cs**
    - Test: BBM timing, OE gap, row advance

---

## Phase 4: Safety + Clock Models

### Files to create:

18. **sim/viewer/src/FpdSimViewer/Models/ProtMonModel.cs**
    - Port from: sim/golden_models/models/ProtMonModel.h/cpp

19. **sim/viewer/src/FpdSimViewer/Models/ClkRstModel.cs**
    - Port from: sim/golden_models/models/ClkRstModel.h/cpp

20. **sim/viewer/src/FpdSimViewer/Models/PowerSeqModel.cs**
    - Port from: sim/golden_models/models/PowerSeqModel.h/cpp

21. **sim/viewer/src/FpdSimViewer/Models/EmergencyShutdownModel.cs**
    - Port from: sim/golden_models/models/EmergencyShutdownModel.h/cpp

22. **sim/viewer/src/FpdSimViewer/Models/RadiogModel.cs**
    - Port from: sim/golden_models/models/RadiogModel.h/cpp

---

## Phase 5: Simulation Engine

### Files to create:

23. **sim/viewer/src/FpdSimViewer/Engine/SimulationEngine.cs**
    - Orchestrates 12 models in dependency order (see spec.md Section 6.1)
    - Step(), Reset(), SetCombo(), SetMode()

24. **sim/viewer/src/FpdSimViewer/Engine/HardwareComboConfig.cs**
    - C1-C7 factory: creates correct gate driver + AFE model per combo

25. **sim/viewer/src/FpdSimViewer/Engine/SimulationSnapshot.cs**
    - Immutable per-cycle state capture for UI binding

26. **sim/viewer/src/FpdSimViewer/Engine/TraceCapture.cs**
    - Circular buffer of snapshots (default 2048) for timeline rendering

27. **sim/viewer/src/FpdSimViewer/Engine/RequirementTracker.cs**
    - R-SIM-041~052, AC-SIM-035~047 PASS/FAIL evaluation

### Test:
28. **sim/viewer/tests/FpdSimViewer.Tests/Engine/SimulationEngineTests.cs**
    - Test: full STATIC cycle, combo switch, error path

---

## Phase 6: ViewModels

### Files to create:

29. **sim/viewer/src/FpdSimViewer/ViewModels/MainViewModel.cs**
30. **sim/viewer/src/FpdSimViewer/ViewModels/SimControlViewModel.cs**
31. **sim/viewer/src/FpdSimViewer/ViewModels/RegisterEditorViewModel.cs**
32. **sim/viewer/src/FpdSimViewer/ViewModels/PanelScanViewModel.cs**
33. **sim/viewer/src/FpdSimViewer/ViewModels/FsmDiagramViewModel.cs**
34. **sim/viewer/src/FpdSimViewer/ViewModels/ImagingCycleViewModel.cs**

---

## Phase 7: Views + Drawing

### Files to create:

35. **sim/viewer/src/FpdSimViewer/App.xaml** + App.xaml.cs
36. **sim/viewer/src/FpdSimViewer/MainWindow.xaml** + MainWindow.xaml.cs
37. **sim/viewer/src/FpdSimViewer/Resources/Colors.xaml**
38. **sim/viewer/src/FpdSimViewer/Resources/Styles.xaml**
39. **sim/viewer/src/FpdSimViewer/Converters/FsmStateToColorConverter.cs**
40. **sim/viewer/src/FpdSimViewer/Converters/BoolToVisibilityConverter.cs**
41. **sim/viewer/src/FpdSimViewer/Converters/SignalValueToStringConverter.cs**
42. **sim/viewer/src/FpdSimViewer/Views/Controls/SimControlBar.xaml**
43. **sim/viewer/src/FpdSimViewer/Views/Controls/ComboModeSelector.xaml**
44. **sim/viewer/src/FpdSimViewer/Views/Controls/StatusBar.xaml**
45. **sim/viewer/src/FpdSimViewer/Views/Controls/RegisterEditorPanel.xaml**
46. **sim/viewer/src/FpdSimViewer/Views/Controls/RequirementStatusPanel.xaml**
47. **sim/viewer/src/FpdSimViewer/Views/Tabs/PanelScanTab.xaml**
48. **sim/viewer/src/FpdSimViewer/Views/Tabs/FsmDiagramTab.xaml**
49. **sim/viewer/src/FpdSimViewer/Views/Tabs/ImagingCycleTab.xaml**
50. **sim/viewer/src/FpdSimViewer/Views/Drawing/PanelGridRenderer.cs**
51. **sim/viewer/src/FpdSimViewer/Views/Drawing/FsmGraphRenderer.cs**
52. **sim/viewer/src/FpdSimViewer/Views/Drawing/TimingDiagramRenderer.cs**

---

## Coding Entry Points (for Codex)

When starting implementation, begin with these files in order:

```
1. sim/viewer/src/FpdSimViewer/FpdSimViewer.csproj          (project setup)
2. sim/viewer/src/FpdSimViewer/Models/Core/SignalTypes.cs    (foundation types)
3. sim/viewer/src/FpdSimViewer/Models/Core/GoldenModelBase.cs
4. sim/viewer/src/FpdSimViewer/Models/Core/FoundationConstants.cs
5. sim/viewer/src/FpdSimViewer/Models/RegBankModel.cs        (first model)
6. sim/viewer/src/FpdSimViewer/Models/PanelFsmModel.cs       (most critical)
```

For each model file, read the corresponding C++ source FIRST:
- Read the .h file for interface/state variables
- Read the .cpp file for step() logic
- Port 1:1 to C#, changing only syntax (not logic)

---

## Relationship to Codex Prep Document

The `docs/review/sw-sim-gui-coding-prep.md` (Codex-authored) proposed C++ Dear ImGui at `sim/gui/`.
This plan supersedes that with C# WPF at `sim/viewer/`.

Key mappings from Codex prep to this plan:
| Codex Prep | This Plan |
|-----------|-----------|
| sim/gui/AppMain.cpp | sim/viewer/src/FpdSimViewer/App.xaml |
| sim/gui/ScenarioConfig.h | HardwareComboConfig.cs + RegisterEditorViewModel.cs |
| sim/gui/ScenarioRunner.h | SimulationEngine.cs |
| sim/gui/ModelRegistry.h | HardwareComboConfig.cs |
| sim/gui/TraceCapture.h | TraceCapture.cs |
| sim/gui/views/TimelineView.cpp | ImagingCycleTab.xaml + TimingDiagramRenderer.cs |
| sim/gui/views/MismatchView.cpp | RequirementStatusPanel.xaml + RequirementTracker.cs |
| sim/gui/views/GateView.cpp | PanelScanTab.xaml (gate signal section) |
| sim/gui/views/AfeArrayView.cpp | PanelScanTab.xaml (AFE status section) |
