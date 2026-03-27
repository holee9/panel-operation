# FPD Simulation Viewer — 사용 가이드

FPD(Flat Panel Detector) FPGA 설계를 위한 골든 모델 시뮬레이션 뷰어입니다.
12개 C++ 골든 모델을 C# .NET 8 WPF로 bit-accurate 포팅하여, 실시간 시각화를 제공합니다.

---

## 1. 사전 요구사항

| 항목 | 요구 버전 |
|------|-----------|
| OS | Windows 10/11 (x64) |
| .NET SDK | 8.0 이상 |
| (선택) IDE | Visual Studio 2022 |

### .NET 8 SDK 설치 확인

```powershell
dotnet --version
# 출력 예: 8.0.xxx
```

설치되지 않은 경우 [.NET 8 SDK 다운로드](https://dotnet.microsoft.com/download/dotnet/8.0) 페이지에서 설치합니다.

---

## 2. 빌드 및 실행

### 방법 A: 명령줄 (권장)

```powershell
cd sim/viewer
dotnet run --project src/FpdSimViewer/FpdSimViewer.csproj
```

### 방법 B: Visual Studio

1. `sim/viewer/FpdSimViewer.sln` 을 Visual Studio 2022에서 열기
2. `FpdSimViewer` 를 시작 프로젝트로 설정
3. F5 (디버그 실행) 또는 Ctrl+F5 (릴리스 실행)

### 테스트 실행

```powershell
cd sim/viewer
dotnet test tests/FpdSimViewer.Tests/FpdSimViewer.Tests.csproj
# 64개 xUnit 테스트 실행
```

---

## 3. 화면 구성

```
┌─────────────────────────────────────────────────────────────────────┐
│ [Combo ▼] [Mode ▼] [Reset] [Step] [Play] [Speed ━━━━━━━]          │  ← 컨트롤 바
│ Power: [TargetMode ▼] [☐VGL] [☐VGH]                               │
│ X-ray: [☐Ready] [☐Prep] [☐On] [☐Off]                             │
│ Faults: [☐VGH Over] [☐VGH Under] [☐Temp] [☐PLL] [☐Emergency]    │
├────────────────────────────────────────────┬────────────────────────┤
│                                            │  Register Editor      │
│  [ Panel Scan | FSM Diagram | Imaging ]    │  ┌──────────────────┐ │
│                                            │  │Addr│Name│Val│R/W │ │
│         ← 메인 탭 영역 →                    │  │0x00│CTRL│...│R/W │ │
│                                            │  │0x01│STAT│...│R   │ │
│                                            │  │ ...│ ...│...│... │ │
│                                            │  │0x1F│RSV │...│R   │ │
│                                            │  └──────────────────┘ │
├────────────────────────────────────────────┴────────────────────────┤
│ [State: IDLE] | Row 0 / 2048 | Cycle 0 | Elapsed 00:00:00.000     │  ← 상태바
└─────────────────────────────────────────────────────────────────────┘
```

---

## 4. 빠른 시작 (Quick Start)

### Step 1: 하드웨어 조합 선택

상단 **Combo** 드롭다운에서 C1~C7 중 선택합니다.

| ID | 패널 | Gate IC | AFE | 용도 |
|----|-------|---------|-----|------|
| C1 | R1717 (17×17") | NV1047 | AD71124 | 표준 정지영상 |
| C2 | R1717 | NV1047 | AD71143 | 저전력/모바일 |
| C3 | R1717 | NV1047 | AFE2256 | 고화질 (저노이즈, CIC) |
| C4 | R1714 (17×14") | NV1047 | AD71124 | 비정방 패널 |
| C5 | R1714 | NV1047 | AFE2256 | 비정방 고화질 |
| C6 | X239AW1 (43×43cm) | NT39565D | AD71124 ×12 | 대형 패널, 다중 AFE |
| C7 | X239AW1 | NT39565D | AFE2256 ×12 | 대형 패널 고화질 |

> Combo를 변경하면 시뮬레이션이 자동으로 리셋됩니다.

### Step 2: 동작 모드 선택

**Mode** 드롭다운에서 선택합니다.

| 모드 | 설명 |
|------|------|
| STATIC | 단일 프레임 촬영 (1회 실행 후 DONE 정지) |
| CONTINUOUS | 연속 촬영 (DONE → RESET 자동 반복) |
| TRIGGERED | X-ray 외부 트리거 대기 후 촬영 |
| DARK_FRAME | Gate OFF 상태에서 AFE만 읽기 (오프셋 캘리브레이션) |
| RESET_ONLY | 패널 리셋만 수행 (진단용) |

### Step 3: 시뮬레이션 실행

- **Play** 버튼 클릭 또는 `Space` 키 → 자동 실행 시작
- **Speed** 슬라이더로 속도 조절 (1 ~ 120)

### Step 4: 탭에서 결과 관찰

3개 탭이 실시간으로 업데이트됩니다 (아래 섹션 참고).

---

## 5. 키보드 단축키

| 키 | 동작 |
|----|------|
| `Space` | Play / Pause 토글 |
| `→` (오른쪽 화살표) | 1 사이클 Step |
| `Home` | 시뮬레이션 Reset |

---

## 6. 컨트롤 바 상세

### 시뮬레이션 제어

| 컨트롤 | 설명 |
|---------|------|
| **Reset** | 시뮬레이션 정지 + 사이클 0으로 초기화 (모드 유지) |
| **Step** | 1 사이클만 진행 |
| **Play/Pause** | 자동 실행 토글 |
| **Speed** 슬라이더 | 1~10: 프레임당 1~10 사이클, 11~100: 점진 가속, 101~120: 최대 ~6000배속 |

### 외부 신호 주입 (External Signal Injection)

실제 하드웨어 환경을 시뮬레이션하기 위한 외부 입력입니다.

**Power (전원)**

| 체크박스 | 설명 |
|----------|------|
| Target Mode | 전원 시퀀싱 목표 모드 (0~6) |
| VGL Stable | VGL 전원 레일 안정 상태 |
| VGH Stable | VGH 전원 레일 안정 상태 |

> STATIC 촬영을 시작하려면 VGL Stable + VGH Stable을 모두 체크해야 Power Good 상태가 됩니다.

**X-ray (엑스레이)**

| 체크박스 | 설명 |
|----------|------|
| Ready | X-ray 제너레이터 준비 완료 |
| Prep | 고전압 셋업 요청 |
| On | 조사 중 (인테그레이션 구간) |
| Off | 조사 종료 |

> TRIGGERED 모드에서는 X-ray 핸드셰이크 신호를 순서대로 체크해야 FSM이 진행됩니다.

**Faults (결함 주입)**

| 체크박스 | 설명 |
|----------|------|
| VGH Over | VGH 과전압 결함 |
| VGH Under | VGH 저전압 결함 |
| Temp Over | 과열 결함 |
| PLL Unlocked | PLL 잠금 해제 결함 |
| HW Emergency | 하드웨어 비상 정지 |

> 결함을 주입하면 ProtMon(보호 모니터)이 감지하여 FSM을 ERROR 상태로 전환합니다.

---

## 7. 탭 상세

### Tab 1: Panel Scan (패널 스캔)

행(Row) 단위 스캔 진행 상황을 실시간 비트맵으로 시각화합니다.

**색상 의미:**

| 색상 | 의미 |
|------|------|
| 회색 (Gray) | 미스캔 행 (대기 중) |
| 파랑 (Blue) | 현재 행 — Gate ON 펄스 활성 |
| 주황 (Orange) | 현재 행 — Gate 세틀링 중 |
| 초록 (Green) | 스캔 완료 행 |

**우측 정보 패널:**

- **Key Metrics**: 현재 행 번호, 전체 행 수, AFE Ready/Data Valid 상태
- **Gate Signals**: Gate IC 출력 신호 목록 (OE, CLK, SD, BBM, STV 등)
- **AFE Status**: AFE 칩별 상태 (IDLE / READY / VALID) — C6/C7은 12칩까지 표시

### Tab 2: FSM Diagram (상태 기계 다이어그램)

FSM 상태 노드를 그래프로 표시하고, 전이 이력을 기록합니다.

**노드 색상:**

| 색상 | 의미 |
|------|------|
| 금색 (Gold) | 현재 활성 상태 |
| 연한 파랑 (Light Steel Blue) | 비활성 상태 |
| 빨강 (Red) | ERROR 상태 (S15) |

**FSM 상태 목록:**

| ID | 상태 이름 | 설명 |
|----|-----------|------|
| 0 | IDLE | 대기 |
| 1 | POWER_CHECK | 전원 확인 |
| 2 | RESET | 패널 리셋 |
| 3 | WAIT_PREP | X-ray 준비 대기 |
| 4 | BIAS_STAB | 바이어스 안정화 |
| 5 | XRAY_INTEG | X-ray 인테그레이션 |
| 6 | CONFIG_AFE | AFE 설정 |
| 7 | READOUT | 데이터 읽기 |
| 8 | SETTLE | 세틀링 |
| 9 | FRAME_DONE | 프레임 완료 |
| 10 | DONE | 완료 |
| 15 | ERROR | 오류 |

**우측 Transition History**: 최근 24개 상태 전이 기록 (형식: `Cycle XXXXXXX: STATE_NAME`)

### Tab 3: Imaging Cycle (이미징 사이클)

타이밍 위상과 신호 파형을 타임라인으로 시각화합니다.

**Phase Bar (위상 바):**

| 색상 | 위상 |
|------|------|
| 회색 | IDLE |
| 파랑 | RESET |
| 연두 | INTEGRATE (BIAS_STAB ~ XRAY_INTEG) |
| 청록 | READOUT (CONFIG_AFE ~ SETTLE) |
| 올리브 | DONE |
| 빨강 | ERROR |

> 현재 활성 위상은 100% 불투명도, 비활성은 35%로 표시됩니다.

**Signal Traces (신호 파형, 4채널):**

| 신호 | 색상 | 설명 |
|------|------|------|
| GateOn | 파랑 | 행 게이트 펄스 활성 여부 |
| AfeValid | 청록 | AFE 데이터 출력 유효 |
| PowerGood | 올리브 | 전원 안정 상태 |
| ProtErr | 빨강 | 보호 에러 플래그 |

**Visible Window 슬라이더**: 표시 범위 조절 (100 ~ 2000 사이클)

**Progress Bar**: 행 스캔 진행률 (`현재 행 / 전체 행`)

---

## 8. Register Editor (레지스터 편집기)

화면 우측에 위치한 32개 레지스터 편집 패널입니다.

### 편집 방법

1. **Value** 열의 셀을 클릭
2. 16진수 값 입력 (예: `0x0800` 또는 `0800`)
3. Enter로 적용

> R (Read-only) 레지스터는 편집할 수 없습니다 (회색 배경).

### 주요 레지스터

| 주소 | 이름 | R/W | 설명 |
|------|------|-----|------|
| 0x00 | REG_CTRL | R/W | 시작/중단 제어 비트 |
| 0x01 | REG_STATUS | R | 상태 플래그 (busy, done, error) |
| 0x02 | REG_MODE | R/W | 동작 모드 |
| 0x03 | REG_COMBO | R/W | 하드웨어 조합 선택 (C1~C7) |
| 0x04 | REG_NROWS | R/W | 전체 행 수 |
| 0x05 | REG_NCOLS | R/W | 전체 열 수 |
| 0x06 | REG_TLINE | R/W | 라인 시간 (10ns 단위). 콤보별 최솟값 자동 클램프 |
| 0x07 | REG_TRESET | R/W | 리셋 펄스 지속시간 |
| 0x08 | REG_TINTEG | R/W | 인테그레이션 시간 (하위 16비트) |
| 0x09 | REG_TGATE_ON | R/W | Gate ON 펄스 폭 |
| 0x0A | REG_TGATE_SETTLE | R/W | Gate 세틀 시간 |
| 0x0B | REG_AFE_IFS | R/W | AFE 입력/필터 선택 |
| 0x0C | REG_AFE_LPF | R/W | AFE 로우패스 필터 설정 |
| 0x0D | REG_AFE_PMODE | R/W | AFE 전력 모드 |
| 0x0E | REG_CIC_EN | R/W | AFE2256 CIC 필터 활성화 |
| 0x0F | REG_CIC_PROFILE | R/W | AFE2256 CIC 필터 프로파일 |
| 0x10 | REG_SCAN_DIR | R/W | 스캔 방향 (순방향/역방향) |
| 0x14 | REG_LINE_IDX | R | 현재 행 인덱스 (상태) |
| 0x15 | REG_ERR_CODE | R | 에러 코드 (상태) |
| 0x17 | REG_TINTEG_HI | R/W | 인테그레이션 시간 (상위 16비트) |
| 0x18 | REG_VERSION | R | 펌웨어 버전 |

---

## 9. 상태바

화면 하단에 4가지 정보를 실시간 표시합니다.

| 항목 | 형식 | 설명 |
|------|------|------|
| State | `[상태명]` | 현재 FSM 상태 (색상 배지) |
| Row | `Row XX / YYY` | 현재 행 / 전체 행 |
| Cycle | `Cycle NNNN` | 리셋 이후 총 사이클 수 |
| Elapsed | `HH:MM:SS.fff` | 경과 시간 (100MHz 클럭 기준) |

우측 인디케이터:
- **Protection Error** (경고색) — 보호 모니터 트리거 시 표시
- **Power Good** (강조색) — 전원 안정 시 표시

---

## 10. 사용 시나리오 예제

### 예제 1: C1 STATIC 모드 단일 촬영

1. Combo → **C1** 선택
2. Mode → **STATIC** 선택
3. Power: **VGL Stable** ✓, **VGH Stable** ✓ 체크
4. **Play** 클릭 (또는 `Space`)
5. FSM 진행 관찰: IDLE → POWER_CHECK → RESET → READOUT → DONE
6. Panel Scan 탭에서 행이 회색 → 파랑 → 초록으로 변하는 것을 확인

### 예제 2: TRIGGERED 모드 X-ray 핸드셰이크

1. Combo → **C3** 선택 (AFE2256 고화질)
2. Mode → **TRIGGERED** 선택
3. Power: **VGL Stable** ✓, **VGH Stable** ✓ 체크
4. **Play** 시작
5. FSM이 WAIT_PREP에서 대기하는 것을 확인
6. X-ray: **Ready** ✓ 체크 → FSM이 BIAS_STAB으로 진행
7. X-ray: **Prep** ✓ 체크
8. X-ray: **On** ✓ 체크 → XRAY_INTEG 진행
9. X-ray: **Off** ✓ 체크 → READOUT 진행
10. Imaging Cycle 탭에서 위상 전환 타이밍 관찰

### 예제 3: 결함 주입 테스트

1. 정상 STATIC 촬영 실행 중
2. Faults: **Temp Over** ✓ 체크
3. FSM Diagram 탭에서 ERROR(S15, 빨강) 상태 전환 확인
4. 상태바에 **Protection Error** 배지 표시 확인
5. **Temp Over** 체크 해제 → **Reset** 클릭으로 복구

### 예제 4: 대형 패널 (C6/C7) 다중 AFE

1. Combo → **C6** 선택 (43×43cm, 12 AFE)
2. Panel Scan 탭 → AFE Status에서 AFE1~AFE12 상태 확인
3. 3072×3072 해상도 스캔 진행 관찰
4. Speed 슬라이더를 100 이상으로 올려 고속 진행

### 예제 5: 레지스터 수동 조정

1. 임의 Combo 선택
2. Register Editor에서 **REG_TLINE** (0x06) 값을 `0x1000`으로 변경
3. **REG_TGATE_ON** (0x09) 값을 `0x0100`으로 변경
4. **Play** 실행 후 타이밍 변화를 Imaging Cycle 탭에서 관찰

---

## 11. 트러블슈팅

| 증상 | 원인 | 해결 방법 |
|------|------|-----------|
| Play 눌러도 FSM이 IDLE에서 안 움직임 | 전원 신호 미설정 | Power → VGL Stable, VGH Stable 체크 |
| 행이 스캔되지 않음 | 레지스터 미설정 | REG_NROWS, REG_TLINE 값 확인 |
| 즉시 ERROR 상태 진입 | 결함 주입 활성 | Faults 체크박스 모두 해제 후 Reset |
| 레지스터 편집 불가 | Read-only 레지스터 | Access 열에서 R/W 여부 확인 |
| Speed 조절이 반응 없음 | Play 미실행 상태 | Play 버튼 먼저 클릭 |
| 빌드 오류 | .NET SDK 미설치 | `dotnet --version`으로 8.0+ 확인 |

---

## 12. 시뮬레이션 엔진 구조 (참고)

매 사이클(100MHz = 10ns) 다음 순서로 12개 모델이 실행됩니다:

```
RegBank → ClkRst → PowerSeq → EmergencyShutdown → PanelFsm
→ RowScan → GateDriver → AfeModel → Radiog → ProtMon
→ TraceCapture → RequirementTracker
```

- 클럭: 100MHz (10ns/cycle)
- 상태바의 Elapsed 시간은 `Cycles / 100,000,000`초로 계산됩니다
