# SPEC-FPD-GUI-002 v4.0: 통합 동작 모니터 + 설정 분리

## Context

**핵심 원칙**: 동작 확인은 **하나의 통합 화면**, 설정/분석은 **별도 탭**

실제 벤치 테스트와 동일:
- **메인 모니터** = 오실로스코프 + 로직 분석기 화면 → 전체 동작을 한눈에
- **조작 패널** = 파라미터 설정, 레지스터 편집 → 별도 공간

---

## 화면 구조

```
┌─────────────────────────────────────────────────────────────────────┐
│ [▶Play] [⏸Pause] [→Step] [⟲Reset] [Speed ━━━━━] | P1+G1+A1 | STATIC │  ← 컨트롤 바
├═══════════════════════════════════════════════╤══════════════════════╡
│                                               │                      │
│          ★ MAIN: Operation Monitor ★          │   CONFIG TABS        │
│          (항상 표시, 탭 전환 없음)               │   (설정/분석용)       │
│                                               │                      │
│  ┌─ FSM State ───────────────────────────┐   │  [HW Setup]          │
│  │ ●IDLE → ●PWR → ●RST → ★READ → ○DONE │   │  [Parameters]        │
│  │         Cycle: 1,234,567 | 12.35 ms    │   │  [Registers]         │
│  └────────────────────────────────────────┘   │  [Data Path]         │
│                                               │  [Verification]      │
│  ┌─ Panel Scan ──────────────────────────┐   │                      │
│  │ Row 255/2048  [████████░░░░] 12.4%    │   │  ┌─ 현재 탭 내용 ──┐  │
│  │                                        │   │  │                  │ │
│  │ [완료 행] [활성★VGH +20V] [대기 행]    │   │  │  (탭에 따라      │ │
│  │                                        │   │  │   내용 변경)     │ │
│  │ 픽셀 히트맵: [████████████████████]    │   │  │                  │ │
│  │ Min:120 Max:3842 Mean:2048 DN          │   │  │                  │ │
│  └────────────────────────────────────────┘   │  │                  │ │
│                                               │  │                  │ │
│  ┌─ Signal Scope (하단, 오실로스코프) ────┐   │  │                  │ │
│  │                                        │   │  │                  │ │
│  │ Gate OE  +20V ─┐    ┌── VGH           │   │  │                  │ │
│  │         -10V ──┘────┘── VGL            │   │  │                  │ │
│  │              ←30µs→                     │   │  │                  │ │
│  │ Gate CLK ┌┐┌┐┌┐┌┐┌┐ 200kHz           │   │  │                  │ │
│  │          └┘└┘└┘└┘└┘                    │   │  │                  │ │
│  │ AFE SYNC ─┐  ┌──── (conversion)       │   │  │                  │ │
│  │           └──┘                         │   │  │                  │ │
│  │ AFE DOUT ═══════════╗ valid            │   │  │                  │ │
│  │                      ╚════             │   │  │                  │ │
│  │ Power    ─────── VGL OK ── VGH OK ──  │   │  │                  │ │
│  │                                        │   │  │                  │ │
│  │ [5µs/div] ◀━━━━━●━━━━━▶ zoom          │   │  └──────────────────┘ │
│  └────────────────────────────────────────┘   │                      │
│                                               │                      │
├───────────────────────────────────────────────┴──────────────────────┤
│ [State: READOUT] | Row 255/2048 | 12.35ms | T_line:30µs ✓ | VGL ✓  │  ← 상태바
└─────────────────────────────────────────────────────────────────────┘
```

---

## Main: Operation Monitor (좌측 ~65%, 항상 표시)

### 영역 1: FSM State (상단, ~8% 높이)

```
●IDLE → ●PWR_CHK → ●RESET → ★READOUT → ○SETTLE → ○DONE
                              ↑ 현재 상태 (골드 강조)

Cycle: 1,234,567 | Elapsed: 12.35 ms | Mode: STATIC
```

- 수평 파이프라인 형태로 FSM 진행 표시 (원형 노드가 아닌 **진행 바**)
- 현재 상태 강조 (골드) + 완료 상태 (초록) + 미진행 (회색)
- 사이클/시간 카운터 상시 표시

### 영역 2: Panel Scan (중단, ~30% 높이)

```
Row Progress: [████████████░░░░░░░░░░░░] 255/2048 (12.4%)

┌─────────────────────────────────────────────────┐
│ ■■■■■■■■■■■■■■ (완료, 녹색)                     │
│ ▓▓▓▓▓▓▓▓▓▓▓▓▓▓ Row 255 ★ Gate: VGH +20V       │ ← 활성 행
│ ░░░░░░░░░░░░░░ (대기, 회색)                      │
│ ░░░░░░░░░░░░░░                                   │
└─────────────────────────────────────────────────┘

Pixel Heatmap (Row 255): [컬러바 2048 pixels]
Min: 120 DN | Max: 3842 DN | Mean: 2048 DN | σ: 45.2
```

- 행 진행률 바 (전체 대비 현재 위치)
- 패널 단면 축약 (완료/활성/대기 3구간)
- 활성 행에 Gate 전압 표시
- 현재 행 픽셀 값 히트맵 + 통계

### 영역 3: Signal Scope (하단, ~55% 높이)

**오실로스코프 스타일 멀티채널 파형** — 이것이 핵심

```
채널 구성 (기본 6채널, 사용자 추가/제거 가능):

Ch1: Gate OE     VGH(+20V) ↔ VGL(-10V) 스윙   [파랑]
Ch2: Gate CLK    0V ↔ 3.3V 클럭                 [청록]
Ch3: AFE SYNC    0V ↔ 3.3V 펄스                 [초록]
Ch4: AFE DOUT    LVDS 데이터 유효 구간           [주황]
Ch5: VGL Rail    전압 곡선 (-10V 목표)           [보라]
Ch6: VGH Rail    전압 곡선 (+20V 목표)           [빨강]
```

**오실로스코프 기능**:
- **시간축 줌**: 1µs/div ~ 10ms/div (마우스 휠)
- **커서**: 두 점 클릭 → ΔT 측정 (µs 단위)
- **자동 측정**: 각 채널별 주파수, 펄스 폭, 전압 레벨
- **트리거**: FSM 상태 전환 시 자동 정렬 (현재 행 시작점 기준)
- **채널 표시/숨김**: 체크박스로 선택

**NV1047 모드 (C1~C5)**:
- Ch1: OE (VGH/VGL), Ch2: CLK, Ch3: SD1, Ch4: SD2

**NT39565D 모드 (C6~C7)**:
- Ch1: OE1, Ch2: OE2, Ch3: STV1, Ch4: STV2, Ch5: CPV

**AFE 채널 (AD71124/AD71143)**:
- SYNC, ACLK, Phase(CDS/ADC/OUT), DOUT valid

**AFE 채널 (AFE2256)**:
- SYNC, MCLK counter, FCLK, CIC status, Pipeline overlap

---

## Config Tabs (우측 ~35%, 탭 전환)

### Tab A: HW Setup (하드웨어 설정)

```
── Hardware Selection ──
Preset:  [C1 ▼] (또는 Custom)
Panel:   [R1717 (17×17") ▼]     Rows: 2048  Cols: 2048
Gate IC: [NV1047 ▼]             Max Rows: 2048 ✓
AFE:     [AD71124 ▼]            Chips: 8  TLINE_MIN: 22µs

── Operating Mode ──
Category: [Acquisition ▼]
Mode:     [Static Radiography ▼]
```

### Tab B: Parameters (물리 파라미터)

```
── Gate IC Timing ──
T_gate_on:     [  30.0 ] µs   ━━━━●━━━ (15~50)
T_settle:      [   5.0 ] µs   ━━●━━━━━ (2.5~10)
BBM gap:       [   2.0 ] µs   ━●━━━━━━ (≥2.0)
CLK freq:      [ 200   ] kHz  ━━━━━━●━ (50~200)

── AFE Settings ──
T_line:        [  30.0 ] µs   ━━━●━━━━ (min: 22.0)
IFS range:     [  32   ] pC   (AD71124: 6-bit)
CIC:           [ OFF ▼ ]      (AFE2256 only)

── Power Rails ──
VGH target:    [ +20.0 ] V    ━━━━●━━━ (+15~+30)
VGL target:    [ -10.0 ] V    ━━━●━━━━ (-15~-5)
Slew rate:     [   5.0 ] V/ms ━━━━━●━━ (≤5.0)

── Integration ──
T_integrate:   [ 100.0 ] ms   (0.1~1000)
N_reset:       [     3 ] scans
```

- 슬라이더 + 숫자 입력 병행
- 변경 즉시 시뮬레이션에 반영 (Apply 불필요)
- Spec 범위 초과 시 노란색 경고

### Tab C: Registers (레지스터 편집기)

기존 RegisterEditorPanel 유지 (고급 사용자용 hex 편집)
- Physical Parameters와 양방향 동기화

### Tab D: Data Path (데이터 경로 상세)

```
[AFE ×8]──LVDS──→[ISERDES]──→[Line Buf A/B]──→[CSI-2 TX]

Line Buffer: Bank A [████████░░░░] 1024/2048 write
             Bank B [████████████] 2048/2048 → CSI-2

CSI-2: Lanes 2 | Rate 240MB/s | CRC ✓ | ECC ✓
Packets: FS(1) + Lines(254) + FE(pending)

Pixel Preview (완료 행):
[히트맵 바] Min:120 Max:3842 Mean:2048
```

### Tab E: Verification (검증 + 내보내기)

```
── Timing Verification ──
T_gate_on:  30.0µs  Spec:30µs   ✓ PASS
BBM gap:     2.5µs  Spec:≥2µs   ✓ PASS
T_line:     37.5µs  Spec:≥22µs  ✓ PASS
VGL→VGH:    8.0ms   Spec:≥5ms   ✓ PASS
Readout:   61.4ms   Spec:±1%    ✓ PASS

── Export ──
[Export VCD] [Export CSV] [Generate Vectors] [Compare RTL]

── Event Log ──
0.00ms: IDLE → POWER_CHECK
0.01ms: VGL ramp start → -10V
8.0ms:  VGL stable, VGH ramp start → +20V
16.0ms: All power stable → RESET
...
```

---

## 구현 파일 목록

### 신규 파일

| 파일 | 역할 |
|------|------|
| **Views/Drawing/ScopeRenderer.cs** | 오실로스코프 스타일 멀티채널 파형 렌더러 (핵심) |
| **Views/Drawing/PowerRailRenderer.cs** | 전압 램프/시퀀싱 렌더러 |
| **Views/Controls/OperationMonitor.xaml** | 통합 동작 모니터 (FSM + Panel + Scope) |
| **ViewModels/OperationMonitorViewModel.cs** | 통합 모니터 VM |
| **Views/Controls/PhysicalParamPanel.xaml** | 물리 파라미터 편집기 |
| **ViewModels/PhysicalParamViewModel.cs** | 파라미터 ↔ Register 변환 |
| **Views/Tabs/HwSetupTab.xaml** | 하드웨어 선택 탭 |
| **Views/Tabs/DataPathTab.xaml** | 데이터 경로 탭 |
| **Views/Tabs/VerificationTab.xaml** | 검증 + 내보내기 탭 |
| **ViewModels/DataPathViewModel.cs** | 데이터 경로 VM |
| **ViewModels/VerificationViewModel.cs** | 검증 VM |
| **Engine/TimingMeasurement.cs** | 자동 타이밍 측정 |
| **Engine/ScopeChannelConfig.cs** | 스코프 채널 구성 (Gate IC/AFE별 자동 전환) |
| **Engine/TraceExporter.cs** | VCD/CSV 내보내기 |
| **Engine/TestVectorGenerator.cs** | 테스트 벡터 생성 |
| **Engine/SignalComparer.cs** | RTL VCD 비교 |
| **Engine/CRC16.cs** | CRC-16 CCITT |
| **Engine/MipiEcc.cs** | CSI-2 ECC |
| **Models/SpiSlaveModel.cs** | 데이터 경로 모델 (8종) |
| **Models/AfeSpiMasterModel.cs** | |
| **Models/LvdsRxModel.cs** | |
| **Models/LineBufModel.cs** | |
| **Models/DataOutMuxModel.cs** | |
| **Models/McuDataIfModel.cs** | |
| **Models/Csi2PacketModel.cs** | |
| **Models/Csi2LaneDistModel.cs** | |

### 변경 파일

| 파일 | 변경 |
|------|------|
| **MainWindow.xaml** | 좌: OperationMonitor (고정) + 우: Config Tabs (5탭) |
| **ViewModels/MainViewModel.cs** | 통합 모니터 + 5탭 VM 연결 |
| **Engine/SimulationEngine.cs** | 20개 모델 + 전압 시뮬레이션 + 타이밍 측정 |
| **Engine/SimulationSnapshot.cs** | 전압 값, 파형 샘플, 타이밍 측정값 추가 |
| **Engine/HardwareComboConfig.cs** | Panel/Gate/AFE 개별 선택 |
| **Models/Core/FoundationConstants.cs** | 물리 상수 (전압, 타이밍, 전하량) |
| **Views/Controls/SimControlBar.xaml** | 컨트롤 바 (HW 요약 + 모드 표시) |
| **ViewModels/SimControlViewModel.cs** | 11-Mode + 개별 HW 선택 |

### 제거/대체 파일

| 기존 파일 | 상태 |
|-----------|------|
| Views/Tabs/PanelScanTab.xaml | → OperationMonitor 영역 2로 통합 |
| Views/Tabs/FsmDiagramTab.xaml | → OperationMonitor 영역 1로 통합 |
| Views/Tabs/ImagingCycleTab.xaml | → OperationMonitor Signal Scope로 대체 |
| Views/Drawing/FsmGraphRenderer.cs | → FSM 진행 바로 대체 |
| Views/Drawing/TimingDiagramRenderer.cs | → ScopeRenderer로 대체 |
| Views/Drawing/PanelGridRenderer.cs | → OperationMonitor Panel 영역으로 대체 |
| Views/Controls/RegisterEditorPanel.xaml | → Config Tab C로 이동 (유지) |

---

## Acceptance Criteria

### 통합 동작 모니터 (핵심)

```
AC-MON-001: 단일 화면에서 FSM 상태 + Panel 스캔 + Signal Scope가 동시에 표시됨
AC-MON-002: FSM 상태 전환 시 Panel 영역과 Scope 영역이 동기화되어 갱신됨
AC-MON-003: Play 중 3개 영역이 실시간으로 함께 업데이트됨
```

### Signal Scope (오실로스코프)

```
AC-SCP-001: Gate OE 신호가 VGH(+20V)↔VGL(-10V) 전압 레벨로 표시됨
AC-SCP-002: Gate CLK 신호가 실제 주파수(kHz)로 표시됨
AC-SCP-003: AFE SYNC/변환 단계가 시간축에서 구분됨
AC-SCP-004: 전원 레일(VGL/VGH)이 전압 곡선으로 표시됨
AC-SCP-005: 시간축 줌 가능 (1µs/div ~ 10ms/div, 마우스 휠)
AC-SCP-006: 커서로 두 점 간 ΔT(µs) 측정 가능
AC-SCP-007: 채널 표시/숨김 토글 가능
AC-SCP-008: Gate IC 변경 시 채널 구성 자동 전환 (NV1047↔NT39565D)
AC-SCP-009: AFE 변경 시 채널 구성 자동 전환 (AD711xx↔AFE2256)
AC-SCP-010: 각 채널 자동 측정값(주파수, 펄스 폭) + Spec Pass/Fail 표시
```

### Panel View

```
AC-PNL-001: 행 진행률 바 표시 (현재 행/전체 행, 백분율)
AC-PNL-002: 활성 행에 Gate 전압(VGH) 표시
AC-PNL-003: 현재 행의 픽셀 값 히트맵 표시 (Min/Max/Mean/σ)
```

### 물리 파라미터 설정

```
AC-PRM-001: T_gate_on을 µs 단위 슬라이더+숫자 입력으로 설정 가능
AC-PRM-002: VGH/VGL 전압을 V 단위로 설정 가능
AC-PRM-003: 파라미터 변경 시 Scope 파형이 즉시 반영됨
AC-PRM-004: Register 값과 양방향 동기화됨
AC-PRM-005: Spec 범위 초과 시 경고 표시
```

### Hardware 선택 + 모드 (기존 승인)

```
AC-HW-001~008: Panel/Gate/AFE 개별 선택
AC-MODE-001~013: 3-Category 11-Mode
AC-COMPAT-001~002: C1~C7 Preset 하위 호환
```

### 데이터 경로

```
AC-DP-001: Data Path 탭에서 Line Buffer ping-pong 상태 표시
AC-DP-002: CSI-2 패킷 구성(Header+CRC) 표시
AC-DP-003: 완료 행 픽셀 프리뷰 표시
```

### 내보내기 + 검증

```
AC-EXP-001: VCD 내보내기 (GTKWave 호환)
AC-EXP-002: CSV 내보내기
AC-EXP-003: 테스트 벡터 생성 (cocotb 호환)
AC-EXP-004: RTL VCD 비교 + mismatch 리포트

AC-VER-001: 타이밍 검증 결과 (Pass/Fail) 상시 표시
AC-VER-002: 이벤트 로그 (FSM 전환 + 전압 이벤트 + 에러)
```

---

## Phase 순서

```
Phase 1: ScopeRenderer + OperationMonitor 통합 레이아웃
         → 단일 화면에서 FSM + Panel + Scope가 동시 표시되는 뼈대

Phase 2: Gate IC 파형 (전압 레벨, 타이밍 측정)
         → OE: VGH/VGL 스윙, CLK: 주파수, BBM gap 측정

Phase 3: AFE 파형 + Power Rail 곡선
         → SYNC/CDS/ADC/DOUT 단계 + VGL/VGH 램프

Phase 4: Hardware 개별 선택 + 11-Mode + Physical Parameters
         → Panel/Gate/AFE 3-dropdown + 물리 파라미터 편집기

Phase 5: 데이터 경로 모델 8종 + Data Path 탭
         → LVDS → LineBuf → CSI-2 전체 파이프라인

Phase 6: 내보내기 + 비교 + Verification 탭
         → VCD/CSV + 벡터 생성 + RTL 비교

Phase 7: 테스트 + 문서 갱신
```

---

## Verification

```
1. dotnet test → 전체 테스트 PASS
2. GUI 실행 후:
   a. 하나의 화면에서 FSM + Panel + Scope가 동시에 보이는지 확인
   b. Play → FSM READOUT 진입 시 Panel 행 진행 + Scope OE 파형 동기 확인
   c. Scope에서 OE 파형이 +20V/-10V로 표시되는지 확인
   d. 시간축 줌으로 Gate 펄스 폭 30µs 확인
   e. Physical Param에서 T_gate_on=25µs → Scope 파형 변경 확인
   f. Panel 선택 변경 → Scope 채널 자동 전환 확인
   g. Export VCD → GTKWave 로드 확인
```
