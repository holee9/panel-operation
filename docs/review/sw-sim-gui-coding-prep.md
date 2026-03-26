# SW Sim GUI Coding Prep

## 검토 기준

- `D:\workspace-github\panel-operation\.moai\specs\SPEC-FPD-GUI-001\spec.md`
- `D:\workspace-github\panel-operation\.moai\specs\SPEC-FPD-GUI-001\acceptance.md`
- `D:\workspace-github\panel-operation\.moai\specs\SPEC-FPD-GUI-001\plan.md`
- `D:\workspace-github\panel-operation\.moai\specs\SPEC-FPD-SIM-001\spec.md`
- `D:\workspace-github\panel-operation\.moai\specs\SPEC-FPD-SIM-001\acceptance.md`
- `D:\workspace-github\panel-operation\.moai\specs\SPEC-FPD-SIM-001\plan.md`
- `D:\workspace-github\panel-operation\.moai\specs\SPEC-FPD-SIM-001\ECR-001.md`
- `D:\workspace-github\panel-operation\sim\golden_models\core\SignalTypes.h`
- `D:\workspace-github\panel-operation\sim\golden_models\core\GoldenModelBase.h`
- `D:\workspace-github\panel-operation\sim\golden_models\models\FoundationConstants.h`

## 최종 방향

이제 GUI 코딩 준비 기준은 명확하다.

1. 기준 spec은 `SPEC-FPD-GUI-001`이다.
2. 기술 스택은 `C# 12 + .NET 8 WPF + MVVM`이다.
3. 작업 위치는 `sim/gui/`가 아니라 `sim/viewer/`이다.
4. `SPEC-FPD-SIM-001` v1.2.0에서 추가된 `R-SIM-041~052`는 GUI spec의 `FR-011~020`으로 이미 흡수되었다.

즉, 기존 준비서에서 잡았던 "SIM spec을 GUI로 번역" 단계는 이제 끝났고, 앞으로는 `SPEC-FPD-GUI-001 plan.md` 순서대로 바로 구현에 들어가면 된다.

## 기술 스택 정정

기존 가정:

- C++17
- Dear ImGui
- `sim/gui/`

정정 후 기준:

- C# 12
- .NET 8 WPF
- MVVM (`CommunityToolkit.Mvvm`)
- xUnit + FluentAssertions
- `sim/viewer/`

## SIM v1.2.0 반영 확인

`SPEC-FPD-GUI-001`은 `SPEC-FPD-SIM-001` v1.2.0 추가분을 GUI 기능 요구로 이미 정리하고 있다.

| SIM Spec | GUI Spec |
|----------|----------|
| `R-SIM-041`, `AC-SIM-035/036` | `FR-011` |
| `R-SIM-042`, `AC-SIM-037` | `FR-012` |
| `R-SIM-043`, `AC-SIM-038` | `FR-013` |
| `R-SIM-044`, `AC-SIM-039/021` | `FR-014` |
| `R-SIM-046`, `AC-SIM-040` | `FR-015` |
| `R-SIM-047`, `AC-SIM-041/022` | `FR-016` |
| `R-SIM-049`, `AC-SIM-043` | `FR-017` |
| `R-SIM-050`, `AC-SIM-044` | `FR-018` |
| `R-SIM-051`, `AC-SIM-045/047` | `FR-019` |
| `R-SIM-052`, `AC-SIM-046` | `FR-020` |

따라서 별도의 GUI 요구 재해석은 더 필요 없다.

## Codex 준비서 -> GUI Plan 매핑

기존 Codex 준비서에서 제안했던 `sim/gui/` 구조는 아래처럼 `SPEC-FPD-GUI-001 plan.md`의 `sim/viewer/` 구조로 옮겨 읽으면 된다.

| 기존 준비 개념 | 실제 구현 파일 |
|----------------|----------------|
| `sim/gui/AppMain.cpp` | `sim/viewer/src/FpdSimViewer/App.xaml`, `MainWindow.xaml` |
| `sim/gui/ScenarioConfig.*` | `Engine/HardwareComboConfig.cs`, `ViewModels/RegisterEditorViewModel.cs` |
| `sim/gui/ScenarioRunner.*` | `Engine/SimulationEngine.cs` |
| `sim/gui/ModelRegistry.*` | `Engine/HardwareComboConfig.cs` |
| `sim/gui/TraceCapture.*` | `Engine/TraceCapture.cs` |
| `sim/gui/RequirementMap.*` | `Engine/RequirementTracker.cs` |
| `sim/gui/views/TimelineView.cpp` | `Views/Tabs/ImagingCycleTab.xaml`, `Views/Drawing/TimingDiagramRenderer.cs` |
| `sim/gui/views/MismatchView.cpp` | `Views/Controls/RequirementStatusPanel.xaml`, `Engine/RequirementTracker.cs` |
| `sim/gui/views/GateView.cpp` | `Views/Tabs/PanelScanTab.xaml`의 gate signal section |
| `sim/gui/views/AfeArrayView.cpp` | `Views/Tabs/PanelScanTab.xaml`의 AFE status section |
| `sim/gui/views/PowerSeqView.cpp` | `Views/Tabs/ImagingCycleTab.xaml`의 power rail timeline |
| `sim/gui/views/FramePreviewView.cpp` | `Views/Tabs/ImagingCycleTab.xaml` 또는 별도 dark-frame/CIC panel |

## 코딩 시작 순서

이번 준비의 핵심 지시사항은 `plan.md Phase 1` 순서를 그대로 따르는 것이다.

### Phase 1 우선순위

1. `SignalTypes`
2. `GoldenModelBase`
3. `FoundationConstants`
4. `RegBank`
5. `PanelFsm`

### 실제 시작 파일

1. `D:\workspace-github\panel-operation\sim\viewer\src\FpdSimViewer\FpdSimViewer.csproj`
2. `D:\workspace-github\panel-operation\sim\viewer\src\FpdSimViewer\Models\Core\SignalTypes.cs`
3. `D:\workspace-github\panel-operation\sim\viewer\src\FpdSimViewer\Models\Core\GoldenModelBase.cs`
4. `D:\workspace-github\panel-operation\sim\viewer\src\FpdSimViewer\Models\Core\FoundationConstants.cs`
5. `D:\workspace-github\panel-operation\sim\viewer\src\FpdSimViewer\Models\RegBankModel.cs`
6. `D:\workspace-github\panel-operation\sim\viewer\src\FpdSimViewer\Models\PanelFsmModel.cs`

## C++ 참조 원본

위 5단계 코딩 전에는 아래 C++ 원본을 먼저 읽고 1:1 포팅해야 한다.

| C# 대상 | C++ 참조 |
|---------|----------|
| `SignalTypes.cs` | `sim/golden_models/core/SignalTypes.h` |
| `GoldenModelBase.cs` | `sim/golden_models/core/GoldenModelBase.h` |
| `FoundationConstants.cs` | `sim/golden_models/models/FoundationConstants.h` |
| `RegBankModel.cs` | `sim/golden_models/models/RegBankModel.h`, `RegBankModel.cpp` |
| `PanelFsmModel.cs` | `sim/golden_models/models/PanelFsmModel.h`, `PanelFsmModel.cpp` |

## 준비 완료 상태

지금 기준으로 코딩 준비는 아래 수준까지 끝났다.

1. GUI spec 위치 확인 완료
2. 스택 정정 완료
3. SIM v1.2.0 추가 요구의 GUI 반영 위치 확인 완료
4. `sim/gui/` 가정과 `sim/viewer/` 실제 구현 위치의 매핑 완료
5. 첫 코딩 배치 순서 확정 완료

## 다음 액션

다음 코딩 패스에서는 분석 없이 바로 아래 순서로 만들면 된다.

1. `FpdSimViewer.csproj`
2. `SignalTypes.cs`
3. `GoldenModelBase.cs`
4. `FoundationConstants.cs`
5. `RegBankModel.cs`
6. `FoundationConstantsTests.cs`
7. `RegBankModelTests.cs`
8. `PanelFsmModel.cs`
9. `PanelFsmModelTests.cs`

## 메모

- `SPEC-FPD-GUI-001`은 아직 git 추적 전 상태지만, 현재 워크스페이스에는 존재한다.
- 따라서 이후 작업 기준은 `docs/review/sw-sim-gui-coding-prep.md`보다 `SPEC-FPD-GUI-001 plan.md`가 우선이다.
- 이 문서의 역할은 코딩 착수 전에 경로, 스택, 매핑, 시작 순서를 고정하는 것이다.
