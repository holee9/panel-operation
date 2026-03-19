# FPGA 정지영상(Radiography) 전용 구동 알고리즘 설계서

**문서 버전:** v1.0  
**작성일:** 2026-03-18  
**대상 시스템:** X-ray FPD (Flat Panel Detector) — 7가지 부품 조합 (C1~C7)  
**적용 모드:** REG\_MODE\[2:0\] = 3'b000 (STATIC 모드 전용)

---

## 목차

1. [정지영상 모드 개요 및 형광투시(Fluoroscopy)와의 차이점](#1-정지영상-모드-개요)
2. [STATIC 모드 전체 FSM 설계](#2-static-모드-전체-fsm-설계)
3. [Pre-Exposure 초기화 시퀀스](#3-pre-exposure-초기화-시퀀스)
4. [Prep-Request / X-ray Enable 핸드셰이크](#4-prep-request--x-ray-enable-핸드셰이크)
5. [통합(Integration) 윈도우 관리](#5-통합integration-윈도우-관리)
6. [풀 패널 Readout 시퀀스](#6-풀-패널-readout-시퀀스)
7. [취득 후 안정화 — Forward Bias / Dummy Scan](#7-취득-후-안정화--forward-bias--dummy-scan)
8. [다크 프레임 취득 알고리즘](#8-다크-프레임-취득-알고리즘)
9. [플랫 필드(Flat Field) 보정 절차](#9-플랫-필드flat-field-보정-절차)
10. [결함 화소 보정 파이프라인](#10-결함-화소-보정-파이프라인)
11. [조합별(C1~C7) 정지상 타이밍 파라미터 테이블](#11-조합별c1c7-정지상-타이밍-파라미터-테이블)
12. [SystemVerilog 핵심 모듈 명세](#12-systemverilog-핵심-모듈-명세)
13. [MCU↔FPGA SPI 커맨드 시퀀스](#13-mcufpga-spi-커맨드-시퀀스)
14. [검증 계획 및 시뮬레이션 시나리오](#14-검증-계획-및-시뮬레이션-시나리오)

---

## 1. 정지영상 모드 개요

### 1.1 형광투시(Fluoroscopy) vs. 정지영상(Radiography) 근본 차이

정지영상(Radiography)은 단 한 장의 고화질 X-ray 이미지를 획득하는 모드다. 형광투시와 달리 고도(高線量) 단일 펄스를 사용하므로 패널 상태 관리, 래그(lag) 제어, 완전 리셋이 훨씬 엄격하게 요구된다.

| 파라미터 | 형광투시(CONTINUOUS) | **정지영상(STATIC)** |
|---|---|---|
| 프레임 수 | 연속 (7.5~30 fps) | **단일 프레임** |
| 픽셀 당 선량 | 10~100 nGy/frame | **~1 µGy (10~100배 고선량)** |
| 노출 전 리셋 | 롤링 또는 연속 | **전 패널 완전 리셋 필수** |
| Readout 방식 | 통합과 중첩 허용 | **중첩 불가, 완전 순차** |
| 래그 영향 | 프레임 간 평균으로 희석 | **첫 프레임 래그가 화질 직접 결정** |
| 취득 후 처리 | 다음 프레임으로 진행 | **후처리 완료 후 전원 상태 변환** |

> **설계 원칙:** 정지영상 모드에서 FPGA는 완전한 상태 머신(FSM)을 수행한다. 전 패널 리셋 → 통합 대기 → 전 패널 readout → 보정 파이프라인의 4단계가 단절 없이 수행되어야 하며, 각 단계 사이의 타이밍 마진을 엄격히 준수해야 한다.

### 1.2 a-Si:H 물리 특성이 정지영상에 미치는 영향

a-Si:H 포토다이오드는 X-ray 노출 후 **수분 단위**로 지속되는 전하 트랩 상태를 가진다 ([Siemens Hoheisel et al., ISCMP 1996](https://www.mhoheisel.de/docs/ISCMP91996112.pdf)):

- **미보정 1st-frame lag:** 노출 신호 대비 2~5% 잔류 신호가 다음 취득에 영향
- **Forward Bias 적용 후:** 잔류 신호 < 0.3%로 감소 (93~95% 저감)
- **Dark current:** 온도 8~10°C 상승마다 2배 증가 (Arrhenius 법칙) → 취득 직전 다크 프레임 필수
- **Charge Injection Compensation(CIC):** 동적 범위 50% → 92%로 확장

### 1.3 REG\_MODE 등록기 정의

```
REG_MODE[2:0] (FPGA reg_bank offset 0x02):
  3'b000 = STATIC     — 정지영상 모드 (본 설계서 대상)
  3'b001 = CONTINUOUS — 형광투시 모드
  3'b010 = TRIGGERED  — 외부 트리거 모드
  3'b011 = DARK_FRAME — 다크 프레임 전용 취득
  3'b100 = RESET_ONLY — 패널 리셋만 실행
```

---

## 2. STATIC 모드 전체 FSM 설계

### 2.1 최상위 FSM 상태 다이어그램

```
                         ┌──────────────────────────────────────────────────────┐
                         │              STATIC_MODE FSM                          │
                         │                                                        │
  MCU → REG_MODE=000     │                                                        │
  ──────────────────►  S0_IDLE                                                    │
                         │                                                        │
                         ▼                                                        │
                       S1_POWER_CHECK ─── FAIL ──► S_ERROR                       │
                         │ PASS                                                   │
                         ▼                                                        │
                       S2_PANEL_RESET ←──────────────── 3~8회 반복               │
                         │ (Pre-exposure dummy scan 완료)                        │
                         ▼                                                        │
                       S3_PREP_WAIT ─── PREP_REQ 수신 ──►                        │
                         │                                                        │
                         ▼                                                        │
                       S4_BIAS_STABILIZE (Forward Bias 선택 시)                  │
                         │ (~30ms)                                                │
                         ▼                                                        │
                       S5_XRAY_ENABLE ──► X_RAY_ENABLE 신호 출력                 │
                         │                                                        │
                         ▼                                                        │
                       S6_INTEGRATION ─── X_RAY_ON 감지 ──► 통합 시작            │
                         │ (통합 윈도우 오픈 상태)                               │
                         ▼                                                        │
                       S7_SETTLE (1~10ms 전하 재분배 대기)                       │
                         │                                                        │
                         ▼                                                        │
                       S8_READOUT (전 패널 순차 Gate scan)                       │
                         │                                                        │
                         ▼                                                        │
                       S9_POST_DARK (선택: 즉시 다크 1~2장 취득)                 │
                         │                                                        │
                         ▼                                                        │
                       S10_CORRECTION (오프셋/게인/결함 보정)                    │
                         │                                                        │
                         ▼                                                        │
                       S11_TRANSFER (MCU → Host로 데이터 전송)                   │
                         │                                                        │
                         ▼                                                        │
                       S12_POST_STAB (Forward Bias 후처리 / Dummy scan)          │
                         │                                                        │
                         ▼                                                        │
                       S0_IDLE (다음 취득 대기)                                  │
                         │                                                        │
                         └──────────────────────────────────────────────────────┘
```

### 2.2 상태별 소요 시간 요약

| 상태 | 최소 시간 | 권장 시간 | 비고 |
|------|-----------|-----------|------|
| S2\_PANEL\_RESET | 3회 × T_frame | 8회 × T_frame | dummy scan 반복 횟수 |
| S4\_BIAS\_STABILIZE | 20ms | 30ms | Forward Bias 적용 시 |
| S5\_XRAY\_ENABLE 후 대기 | 500ms | 2s | generator prep 시간 포함 |
| S7\_SETTLE | 1ms | 10ms | TFT 전하 재분배 |
| S8\_READOUT | N_rows × T_LINE | — | 조합별 상이 (§11 참조) |
| S9\_POST\_DARK | 1 frame | 2 frames | 즉시 다크 취득 |
| S12\_POST\_STAB | 30ms | 100ms | Forward Bias 후처리 |

---

## 3. Pre-Exposure 초기화 시퀀스

### 3.1 목적

X-ray 노출 직전 패널을 **재현 가능한 초기 상태**로 만드는 것이 핵심이다. 전하 트랩 상태가 불균일하면 노출 이미지에 ghost 아티팩트가 발생한다 ([Carestream EP2148500A1](https://patents.google.com/patent/EP2148500A1/en)).

### 3.2 Pre-Exposure Reset 절차 (S2\_PANEL\_RESET)

```
[Pre-Exposure Reset Sequence — S2_PANEL_RESET]

Step 1: 전기적 바이어스 사이클링 (Bias Cycling Reset)
  ├── VPD_BIAS → 0V (역바이어스 해제)
  ├── 1ms 대기
  ├── VPD_BIAS → +0.5V (전순바이어스: 트랩 강제 방전)
  ├── 2ms 대기
  └── VPD_BIAS → PD_REV (-1.5V) 복귀 (역바이어스 재인가)

Step 2: Dummy Gate Scan (N_RESET_SCAN 회 반복, 기본 N=5)
  FOR i = 0 TO N_RESET_SCAN-1:
    ├── STV 펄스 인가 (Gate IC에 스타트 펄스)
    ├── CPV × N_rows 클럭 (전 행 순차 활성화)
    ├── 각 행 T_gate_on = 30µs 유지 (전하 drain)
    ├── AFE 출력은 무시 (FIFO 저장 안 함)
    └── 1행 종료 후 다음 행으로
  END FOR

Step 3: 리셋 완료 확인 (선택적)
  ├── N_RESET_SCAN번째 dummy scan 결과 샘플링
  ├── 평균 픽셀값 < DARK_THRESHOLD (예: 500 DN) 확인
  └── 미달 시 N_RESET_SCAN += 2회 추가 (최대 16회)

Step 4: 전하 재분배 대기
  └── 5ms 대기 (TFT 내부 전하 안정)
```

**FPGA 제어 파라미터:**
```systemverilog
// Pre-exposure reset 파라미터 레지스터
localparam REG_RESET_CNT = 8'h10;   // reset scan 횟수 (기본 5)
localparam REG_RESET_TH  = 8'h11;   // dark threshold (기본 500)

// 리셋 카운터
reg [3:0] reset_scan_cnt;
always_ff @(posedge clk) begin
    if (state == S2_PANEL_RESET) begin
        if (scan_done)
            reset_scan_cnt <= reset_scan_cnt + 1;
        if (reset_scan_cnt >= reg_reset_cnt)
            state <= S3_PREP_WAIT;
    end
end
```

### 3.3 패널 안정화 고려사항

| 조건 | 권장 dummy scan 횟수 | 근거 |
|------|---------------------|------|
| 콜드 부팅 (처음 전원 인가) | 50~100회 | AAPM TG-150, 30~60분 warm-up 대체 |
| 이전 취득 후 1분 이내 | 3~5회 | 트랩 상태 부분 유지 |
| 이전 취득 후 5분 이상 | 8~16회 | 트랩 상태 partial decay |
| 이전 취득 후 30분 이상 | 30~50회 | 완전 리셋 필요 |

---

## 4. Prep-Request / X-ray Enable 핸드셰이크

### 4.1 표준 핸드셰이크 프로토콜

[US20130126742A1](https://patents.google.com/patent/US20130126742A1/en) 특허에 기반한 FPGA↔Generator 핸드셰이크 시퀀스:

```
Generator                                  FPGA (Detector)
    │                                           │
    │──── PREP_REQUEST (TTL HIGH) ──────────►  │
    │                                           │
    │                              [S3→S4: 전원상태 High로 전환]
    │                              [S4: Forward Bias 실행 ~30ms]
    │                              [S2: Final dummy scan 실행]
    │                                           │
    │  ◄──── X_RAY_ENABLE (TTL HIGH) ──────────│
    │                                           │
    │── X_RAY_ON (노출 시작) ──────────────►   │
    │                                           │
    │                              [S6: 통합 윈도우 오픈]
    │                              [X-ray 노출 진행 중]
    │                                           │
    │── X_RAY_OFF (노출 종료) ──────────────►  │
    │                                           │
    │                              [S7: SETTLE 대기]
    │                              [S8: Readout 시작]
    │                                           │
    │  ◄──── XFER_DONE (선택적) ───────────────│
    │                                           │
```

### 4.2 FPGA 핸드셰이크 타이밍 관리

```systemverilog
// 핸드셰이크 신호 정의
input  wire PREP_REQUEST;    // generator → FPGA (GPIO 입력)
output reg  X_RAY_ENABLE;    // FPGA → generator (GPIO 출력)
input  wire X_RAY_ON;        // generator → FPGA (노출 시작 동기)
input  wire X_RAY_OFF;       // generator → FPGA (노출 종료 동기)

// 핸드셰이크 FSM
always_ff @(posedge clk or posedge rst) begin
    if (rst) begin
        X_RAY_ENABLE <= 1'b0;
        hs_state <= HS_IDLE;
    end else begin
        case (hs_state)
        HS_IDLE: begin
            X_RAY_ENABLE <= 1'b0;
            if (PREP_REQUEST)
                hs_state <= HS_PREP_ACK;
        end

        HS_PREP_ACK: begin
            // Pre-exposure 준비 완료 대기
            if (preexposure_done) begin
                X_RAY_ENABLE <= 1'b1;  // generator에 사용 승인
                hs_state <= HS_WAIT_XRAY;
            end
        end

        HS_WAIT_XRAY: begin
            // X-ray 노출 대기 (타임아웃 포함)
            if (X_RAY_ON) begin
                X_RAY_ENABLE <= 1'b0;
                hs_state <= HS_INTEGRATING;
            end else if (timeout_counter >= TIMEOUT_30S) begin
                X_RAY_ENABLE <= 1'b0;
                hs_state <= HS_TIMEOUT_ERR;
            end
        end

        HS_INTEGRATING: begin
            if (X_RAY_OFF || aec_stop)
                hs_state <= HS_READOUT;
        end

        HS_READOUT: begin
            if (readout_done)
                hs_state <= HS_IDLE;
        end

        HS_TIMEOUT_ERR: begin
            err_flag <= ERR_XRAY_TIMEOUT;
            hs_state <= HS_IDLE;
        end
        endcase
    end
end
```

### 4.3 타이밍 파라미터

| 파라미터 | 최솟값 | 권장값 | 최댓값 |
|---|---|---|---|
| PREP\_REQUEST → X\_RAY\_ENABLE 지연 | 200ms | 500ms | 2s |
| X\_RAY\_ENABLE → X\_RAY\_ON 허용 대기 | — | — | 30s (타임아웃) |
| X\_RAY\_OFF → SETTLE 시작 | 0 | 1ms | 10ms |
| SETTLE → Readout 시작 | 1ms | 5ms | 10ms |

---

## 5. 통합(Integration) 윈도우 관리

### 5.1 통합 윈도우 설계 원칙

정지영상에서 통합 윈도우는 X-ray 펄스 전체를 완전히 포함해야 한다. FPGA는 **통합 시간을 X-ray 펄스보다 항상 길게** 유지한다 ([US20130126742A1](https://patents.google.com/patent/US20130126742A1/en)):

```
T_window = T_X_ray_pulse_max + T_setup_margin + T_hold_margin

권장값:
  T_setup_margin = 10ms (X-ray 시작 전 여유)
  T_hold_margin  = 20ms (X-ray 종료 후 여유)
  T_X_ray_pulse_max = 시스템별 최대 노출 시간 (예: 200ms)

  → T_window ≥ 230ms (권장 기본값)
```

### 5.2 통합 윈도우 제어 레지스터

```
FPGA reg_bank:
  REG_INT_TIME_L (0x08): 통합 시간 하위 16비트 [단위: µs]
  REG_INT_TIME_H (0x09): 통합 시간 상위 16비트
  REG_INT_MODE   (0x0A): 
    [0] = 0: 고정 시간 모드
    [0] = 1: X_RAY_OFF 이벤트 종료 모드
    [1] = 0: AEC 미적용
    [1] = 1: AEC stop 신호로 종료
```

### 5.3 모드별 통합 제어

| 모드 | 통합 종료 조건 | 용도 |
|------|---------------|------|
| 고정 시간 | REG\_INT\_TIME 카운터 만료 | 자동 노출 조건 고정 시 |
| Generator 동기 | X\_RAY\_OFF 신호 수신 | generator 연동 표준 모드 |
| AEC 제어 | AEC\_STOP 신호 수신 | 자동 노출 제어 |
| 소프트웨어 | MCU SPI 커맨드 | 테스트/캘리브레이션 |

---

## 6. 풀 패널 Readout 시퀀스

### 6.1 Readout 기본 타이밍 (S8\_READOUT)

```
[S8_READOUT 시퀀스]

T=0:      STV 펄스 인가 (Gate IC row 0 시작)
T=T_LINE: Row 0 gate pulse 종료 → AFE Row 0 데이터 유효
          ├── AFE SYNC 펄스 인가 (ADC 변환 시작)
          └── AFE 데이터 수신 → FPGA FIFO에 저장
T=2×T_LINE: Row 1 활성화 (Row 0 처리와 파이프라인)
...
T=N_rows×T_LINE: 전 패널 readout 완료

총 readout 시간:
  T_readout = N_rows × T_LINE
  예 (C1, AD71124): 3072 × 22µs = 67.6ms
  예 (C3, AFE2256): 3072 × 8µs  = 24.6ms
```

### 6.2 Gate IC ↔ AFE 파이프라인 타이밍

```
         Gate IC (NV1047 or NT39565D)            AFE (AD71124/AD71143/AFE2256)
         ─────────────────────────────────────────────────────────────────────
Row N:   [VGH ON]──────[VGH OFF]
                ↕ T_gate_on
                └── 전하 전달 완료 (5×τ_TFT, τ_TFT≈3~9µs → T_gate_on≥15~45µs)

              [CDS Reset][CDS Sample]──[ADC Convert]──[Data Out]
                          ↕                 ↕
               T_sample_hold         T_adc_convert

Row N+1: 다음 STV/CPV 클럭 (파이프라인 overlap 가능 여부는 AFE 종류에 따라)
```

**파이프라인 규칙:**
- **AD71124:** CDS 방식 비파이프라인 — Row N 데이터 출력 후 Row N+1 게이트 활성화
- **AD71143:** 비파이프라인 — tLINE\_min = 60µs
- **AFE2256:** 파이프라인 모드 지원 — Row N+1 게이트 활성화 후 Row N 데이터 동시 출력 가능

### 6.3 Readout 데이터 경로

```
AFE Output (16-bit/pixel)
    │
    ├── [C1,C2,C3,C4,C5: 단일 AFE]
    │     └── LVDS/SPI → FPGA GPIO → 수신 FIFO
    │
    └── [C6,C7: 12× AFE — 대형 패널]
          ├── AFE0~AFE11 병렬 LVDS 출력
          ├── FPGA 48× LVDS 수신 → IDELAY 정렬
          └── 12× 256채널 × 행 데이터 → DDR4 Frame Buffer

FPGA 내부 경로:
  수신 FIFO → [오프셋 보정] → [게인 보정] → [결함화소 보정]
           → [CIC 보정 (AFE2256 전용)]
           → Frame Buffer (DDR3/DDR4)
           → SPI/LVDS → MCU → Ethernet → Host PC
```

---

## 7. 취득 후 안정화 — Forward Bias / Dummy Scan

### 7.1 Forward Bias 프로토콜 (S4 및 S12)

X-ray 노출 후 포토다이오드의 트랩 상태를 균일하게 초기화하기 위해 Forward Bias(순방향 바이어스)를 인가한다 ([Starman et al., Medical Physics 2011](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/)):

```
[Forward Bias Protocol]

단계 1: 전순바이어스 인가 (노출 전 또는 노출 후)
  ├── VPD_BIAS = +4V (순방향)
  ├── 전류: 20 pC/photodiode
  ├── 스위칭 주파수: 100 kHz
  ├── 8행 단위 그룹으로 적용 (Gate IC 제어)
  └── 소요 시간: ~30ms (40µs/pixel × 파이프라인)

단계 2: 역바이어스 복귀
  ├── VPD_BIAS → 0V (10µs 대기)
  └── VPD_BIAS → PD_REV (-1.5V)

효과:
  ├── 1st-frame lag: 2~5% → <0.3% (93~95% 저감)
  ├── Ghost 대비: 88% 저감 (Frame 2 기준)
  └── CBCT radar artifact: 81% 저감 (골반 기준)

주의: Forward Bias는 X-ray 윈도우를 18ms(15fps) 또는 32ms(10fps)로 제한
     → 정지영상 단발 취득이므로 윈도우 제한 없음 ✓
```

### 7.2 Post-Acquisition Dummy Scan

X-ray 취득 직후 트랩 상태를 안정화하기 위한 추가 dummy scan:

```
[Post-Acquisition Dummy Scan — S12_POST_STAB]

횟수: 3~10회 (권장 5회)
타이밍: Forward Bias 완료 후 즉시 실행
목적:
  1. 취득 중 누적된 잔류 전하 완전 drain
  2. 다음 취득을 위한 트랩 상태 균일화
  3. Dark current 안정화

```

---

## 8. 다크 프레임 취득 알고리즘

### 8.1 즉시 다크 취득 (Post-Exposure Dark)

취득 직후 동일 타이밍으로 1~2 장의 다크 프레임을 취득한다. 이 방식은 온도/전하 상태가 노출 직후 상태와 가장 유사하여 오프셋 정확도가 높다 ([Carestream EP2148500A1](https://patents.google.com/patent/EP2148500A1/en)):

```
[즉시 다크 취득 알고리즘]

Step 1: 노출 readout 완료 후 즉시 (5ms 이내)
Step 2: REG_MODE → 3'b011 (DARK_FRAME 모드)
Step 3: 동일 T_LINE, 동일 AFE 설정으로 완전 패널 scan
Step 4: 다크 프레임 K장 평균 (K=1~4, 권장 K=2)
Step 5: 평균 다크 = D_post[x,y]
Step 6: 보정: Corrected[x,y] = Raw[x,y] - D_post[x,y]
```

### 8.2 정기 다크 캘리브레이션 (Dark Calibration Map)

고정밀 오프셋 맵 갱신을 위한 정기 다크 캘리브레이션:

```
[Dark Calibration 취득 알고리즘 — GE US5452338A 기반]

권장 취득 횟수: N_dark = 64장 (권장), 최소 16장

재귀 필터 방식:
  초기화 (i=0): a₀ = p₀
  갱신 (i>0):   aᵢ = (1 - 1/n) × aᵢ₋₁ + (1/n) × pᵢ
  
  여기서:
    aᵢ = i번째 갱신 후 오프셋 맵 픽셀값
    pᵢ = i번째 다크 프레임 픽셀값
    n  = 스무딩 파라미터 (권장: 16)
    
  노이즈 등가: (2n-1) = 31장 평균과 동일 노이즈 수준
  시정수: n^0.5 ≈ 4회 반복

전제조건:
  ├── X-ray OFF 확인 (방사선 없음 확인)
  ├── 패널 온도 기록 (NTC 읽기)
  └── 전원 안정화 확인

```

### 8.3 온도 인덱싱 오프셋 맵

온도 변화에 따른 Dark current 드리프트를 보상하기 위해 온도별 오프셋 맵을 관리한다:

```
온도 인덱스 테이블:
  T_index = floor((T_celsius - 15) / 5)  [15°C 기준, 5°C 간격]
  
  저장 맵: DARK_MAP[T_index][x][y]  (Flash NVM에 저장)
  
  적용 조건:
    현재 온도와 저장 맵 온도 차이 > 3°C → 새 맵으로 교체
    또는: 보간 (선형 보간 on T_index)
    
  권장 업데이트 주기: 30분마다, 또는 온도 변화 5°C마다
```

---

## 9. 플랫 필드(Flat Field) 보정 절차

### 9.1 Bright Field 취득

균일한 X-ray 조사 조건에서 취득한 밝은 프레임으로 게인 맵을 생성한다:

```
[Flat Field 취득 프로토콜 — AAPM TG-150 준수]

취득 조건:
  X-ray 에너지: 80 kVp (IEC 62220-1 RQA5 조건)
  SID: 182 cm
  균일 조사 (Lucite 팬텀 없음 — 공기 중)
  신호 수준: 픽셀 포화의 50~80% (최적 SNR 영역)

취득 프레임 수: N_bright = 64장 권장
평균 밝기 맵: B[x,y] = (1/N_bright) × Σᵢ Bright_i[x,y]

게인 보정 맵:
  G[x,y] = B_mean / B[x,y]
  여기서 B_mean = 전 픽셀 평균값

  Gain-corrected = Raw_corrected[x,y] × G[x,y]
```

### 9.2 게인 맵 스무딩 필터 (Siemens US20050092909A1)

```
[게인 맵 스무딩 — US20050092909A1 기반]

목적: 게인 맵의 고주파 노이즈 제거
      (단, 실제 픽셀 응답 비균일성 신호는 유지)

1D 스무딩 (열 방향):
  Gs[x,y] = Σₖ w(k) × G[x, y+k]
  w(k): Gaussian 가중치 (σ = 3~5 픽셀)

2D 스무딩 (선택적):
  Gs[x,y] = Σⱼ Σₖ w(j,k) × G[x+j, y+k]
  커널 크기: 7×7 ~ 15×15 픽셀

FPGA 구현:
  ├── 파이프라인 FIR 필터 구조
  ├── 라인 버퍼 7개 (7행 필터 시)
  └── MAC 연산: 16-bit × 12-bit 가중치
```

### 9.3 IEC 62220-1 준수 캘리브레이션 조건

| 파라미터 | 값 |
|---|---|
| X-ray 에너지 | 80 kVp (RQA5) |
| 추가 여과재 | 21 mm Al |
| HVL | 7.1 mm Al |
| 측정 전 예열 | 취득 전 30분 이상 예열 |
| 재캘리브레이션 제한 | 측정 시리즈 중간 불가 |

---

## 10. 결함 화소 보정 파이프라인

### 10.1 결함 화소 분류

| 분류 | 정의 | FPGA 처리 |
|---|---|---|
| Dead pixel | 항상 0 또는 항상 Full | 이웃 8 픽셀 평균으로 대체 |
| Bright pixel | 게인 > 1.5× 평균 | 게인 맵 보정 후 클리핑 |
| Dark pixel | 게인 < 0.5× 평균 | 이웃 평균 보간 |
| Cluster | 5×5 이내 결함 군집 | 2D 이웃 보간 |
| Line defect | 전체 행 또는 열 결함 | 인접 2행/2열 선형 보간 |

### 10.2 결함 보정 알고리즘

```
[결함 화소 보정 파이프라인]

입력: Raw 픽셀 스트림 (16bit/pixel)
출력: 결함 보정된 픽셀 스트림

Step 1: 오프셋 보정 (실시간)
  corr1[x,y] = raw[x,y] - offset_map[x,y]

Step 2: 게인 보정 (실시간)
  corr2[x,y] = corr1[x,y] × gain_map[x,y]

Step 3: CIC 보정 (AFE2256 전용, REG_CIC_EN=1)
  corr3[x,y] = CIC_compensation(corr2[x,y], profile)
  // Q_inj ≈ 0.5pC, 동적범위 50%→92%로 확장

Step 4: 결함 화소 교체
  IF defect_map[x,y] == DEAD OR defect_map[x,y] == CLUSTER:
    corr4[x,y] = interpolate_neighbors(corr3, x, y, kernel=3×3)
  ELSE:
    corr4[x,y] = corr3[x,y]

Step 5: 래그 보정 (정지영상에서는 선택적)
  // 이전 프레임 존재 시:
  corr5[x,y] = corr4[x,y] - Σₙ bₙ × Sₙ × exp(-aₙ)
  // 첫 취득 시 Skip (이전 프레임 없음)

Step 6: 클리핑
  output[x,y] = clamp(corr5[x,y], 0, 65535)
```

### 10.3 FPGA 보정 파이프라인 레지스터

```
FPGA reg_bank:
  0x0E REG_CIC_EN:      bit[0] = AFE2256 CIC 보정 활성화
  0x0F REG_CIC_PROFILE: CIC 프로파일 선택 (0~3)
  0x12 REG_CORR_EN:     bit[0]=오프셋, bit[1]=게인, bit[2]=결함, bit[3]=래그
  0x13 REG_LAG_EN:      bit[0] = 래그 보정 활성화
```

---

## 11. 조합별(C1~C7) 정지상 타이밍 파라미터 테이블

### 11.1 조합별 핵심 타이밍 파라미터

| 파라미터 | C1 (1717+NV1047+AD71124) | C2 (1717+NV1047+AD71143) | C3 (1717+NV1047+AFE2256) | C4 (R1714+NV1047+AD71124) | C5 (R1714+NV1047+AFE2256) | C6 (X239AW1-102+NT39565D+AD71124×12) | C7 (X239AW1-102+NT39565D+AFE2256×12) |
|---|---|---|---|---|---|---|---|
| 패널 크기 | 1717 (17"×17") | 1717 (17"×17") | 1717 (17"×17") | R1714 (17"×14") | R1714 (17"×14") | X239AW1-102 (43"×43") | X239AW1-102 (43"×43") |
| 픽셀 배열 (추정) | 2048×2048 | 2048×2048 | 2048×2048 | 2048×1664 | 2048×1664 | 3072×3072 | 3072×3072 |
| Gate IC | NV1047 | NV1047 | NV1047 | NV1047 | NV1047 | NT39565D ×6 | NT39565D ×6 |
| VGH (Gate ON) | +20V | +20V | +20V | +20V | +20V | +28V | +28V |
| VGL (Gate OFF) | −10V | −10V | −10V | −10V | −10V | −12V | −12V |
| T\_gate\_on (권장) | 30µs | 30µs | 25µs | 30µs | 25µs | 40µs | 30µs |
| T\_LINE\_min | 22µs | 60µs | 8µs | 22µs | 8µs | 22µs | 8µs |
| T\_LINE (권장) | 30µs | 65µs | 10µs | 30µs | 10µs | 35µs | 10µs |
| Readout 시간 | 61.4ms | 132.7ms | 20.5ms | 49.9ms | 16.6ms | 107.5ms | 30.7ms |
| Gate RC delay (τ) | 0.5µs | 0.5µs | 0.5µs | 0.4µs | 0.4µs | 0.92µs | 0.92µs |
| Settle 필요 횟수 (3~5×τ) | ≥2.5µs | ≥2.5µs | ≥2.5µs | ≥2µs | ≥2µs | ≥4.6µs | ≥4.6µs |
| Pre-reset scan 횟수 | 5회 | 5회 | 5회 | 5회 | 5회 | 8회 | 8회 |
| CIC 보정 | N/A | N/A | 활성화 | N/A | 활성화 | N/A | 활성화 |
| 다크 프레임 수 (캘리브) | 64장 | 64장 | 64장 | 64장 | 64장 | 64장 | 64장 |
| AFE 수 | 1 | 1 | 1 | 1 | 1 | 12 | 12 |
| IFS (Full Scale) | 6-bit | 5-bit | 16-bit SAR | 6-bit | 16-bit SAR | 6-bit | 16-bit SAR |
| 등가 노이즈 | 560 e⁻ | — | — | 560 e⁻ | — | 560 e⁻ | — |

### 11.2 FPGA 레지스터 설정값 (조합별)

```
조합 C1 (1717 / NV1047 / AD71124):
  REG_COMBO  = 8'h01
  REG_NROWS  = 16'd2048
  REG_NCOLS  = 16'd2048
  REG_TLINE  = 16'd30      // 30µs in units of 1µs tick
  REG_CIC_EN = 1'b0
  VGH_TARGET = 8'd20       // 20V
  VGL_TARGET = 8'd246      // -10V (signed: 256-10=246)

조합 C2 (1717 / NV1047 / AD71143):
  REG_COMBO  = 8'h02
  REG_NROWS  = 16'd2048
  REG_NCOLS  = 16'd2048
  REG_TLINE  = 16'd65      // 65µs
  REG_CIC_EN = 1'b0

조합 C3 (1717 / NV1047 / AFE2256):
  REG_COMBO  = 8'h03
  REG_NROWS  = 16'd2048
  REG_NCOLS  = 16'd2048
  REG_TLINE  = 16'd10      // 10µs (MCLK=32MHz, 256 cycles/row)
  REG_CIC_EN = 1'b1
  REG_CIC_PROFILE = 2'd0   // 기본 CIC 프로파일

조합 C6 (X239AW1-102 / NT39565D×6 / AD71124×12):
  REG_COMBO  = 8'h06
  REG_NROWS  = 16'd3072
  REG_NCOLS  = 16'd3072
  REG_TLINE  = 16'd35      // 35µs (RC delay 0.92µs × 5 = 4.6µs 포함)
  REG_CIC_EN = 1'b0
  VGH_TARGET = 8'd28       // 28V (NT39565D)
  VGL_TARGET = 8'd244      // -12V

조합 C7 (X239AW1-102 / NT39565D×6 / AFE2256×12):
  REG_COMBO  = 8'h07
  REG_NROWS  = 16'd3072
  REG_NCOLS  = 16'd3072
  REG_TLINE  = 16'd10      // 10µs
  REG_CIC_EN = 1'b1
  REG_CIC_PROFILE = 2'd1   // 대형 패널 CIC 프로파일
```

### 11.3 정지영상 단일 취득 전체 소요 시간 (조합별)

| 조합 | Pre-reset | FB+Settle | Readout | Dark1장 | 보정 | 총계 (추정) |
|---|---|---|---|---|---|---|
| C1 | 150ms | 35ms | 61.4ms | 61.4ms | 30ms | ~340ms |
| C2 | 325ms | 35ms | 132.7ms | 132.7ms | 30ms | ~660ms |
| C3 | 50ms | 35ms | 20.5ms | 20.5ms | 20ms | ~146ms |
| C4 | 150ms | 35ms | 49.9ms | 49.9ms | 25ms | ~310ms |
| C5 | 50ms | 35ms | 16.6ms | 16.6ms | 20ms | ~138ms |
| C6 | 280ms | 35ms | 107.5ms | 107.5ms | 50ms | ~580ms |
| C7 | 80ms | 35ms | 30.7ms | 30.7ms | 30ms | ~207ms |

*Pre-reset: 5~8회 dummy scan 시간. 이미지 전송 시간(MCU→Host) 별도.*

---

## 12. SystemVerilog 핵심 모듈 명세

### 12.1 static\_acq\_ctrl 최상위 모듈

```systemverilog
// static_acq_ctrl.sv — 정지영상 취득 최상위 제어 모듈
// 담당: S0~S12 FSM, 핸드셰이크, 타이밍 관리

module static_acq_ctrl #(
    parameter CLK_FREQ_HZ  = 100_000_000,   // FPGA 클럭 100MHz
    parameter MAX_ROWS      = 3072,
    parameter MAX_COLS      = 3072,
    parameter N_AFE_MAX     = 12
)(
    input  wire        clk,
    input  wire        rst_n,

    // MCU SPI 인터페이스 (레지스터 뱅크)
    input  wire [7:0]  reg_mode,
    input  wire [7:0]  reg_combo,
    input  wire [15:0] reg_nrows,
    input  wire [15:0] reg_tline,      // line time [µs]
    input  wire        reg_cic_en,
    input  wire [3:0]  reg_reset_cnt,  // pre-reset scan 횟수

    // Generator 핸드셰이크 신호
    input  wire        PREP_REQUEST,
    output reg         X_RAY_ENABLE,
    input  wire        X_RAY_ON,
    input  wire        X_RAY_OFF,
    input  wire        AEC_STOP,

    // Gate IC 제어
    output reg         gate_stv,       // Start Vertical
    output reg         gate_cpv,       // Shift Clock
    output reg         gate_oe_n,      // Output Enable (active low)

    // AFE 제어
    output reg  [N_AFE_MAX-1:0] afe_sync,   // AFE 통합 시작
    output reg  [N_AFE_MAX-1:0] afe_mclk_en,

    // VPD Bias 제어 (Forward Bias 지원)
    output reg  [1:0]  vpd_bias_ctrl,  // 00=0V, 01=FWD, 10=REV

    // 데이터 인터페이스
    output reg         frame_valid,
    output reg         frame_dark,     // 1=dark frame
    output reg  [15:0] pixel_addr,
    output reg  [15:0] pixel_data,

    // 상태 출력
    output reg  [3:0]  acq_state,
    output reg  [7:0]  err_flags
);

// FSM 상태 정의
typedef enum logic [3:0] {
    S0_IDLE         = 4'd0,
    S1_POWER_CHECK  = 4'd1,
    S2_PANEL_RESET  = 4'd2,
    S3_PREP_WAIT    = 4'd3,
    S4_FWD_BIAS     = 4'd4,
    S5_XRAY_ENABLE  = 4'd5,
    S6_INTEGRATION  = 4'd6,
    S7_SETTLE       = 4'd7,
    S8_READOUT      = 4'd8,
    S9_POST_DARK    = 4'd9,
    S10_CORRECTION  = 4'd10,
    S11_TRANSFER    = 4'd11,
    S12_POST_STAB   = 4'd12,
    S_ERROR         = 4'd15
} state_t;

state_t state, next_state;

// 타이머 (100MHz → 10ns 해상도)
reg [31:0] timer_cnt;
wire       timer_done;

// 상태 진행 로직은 조합별 파라미터 테이블 기반
// 실제 구현 시 REG_COMBO에 따라 timing 파라미터 선택

endmodule
```

### 12.2 gate\_scan\_engine 모듈

```systemverilog
// gate_scan_engine.sv — Gate IC 순차 스캔 엔진
// NV1047 / NT39565D 공용 (combo에 따라 파라미터 변환)

module gate_scan_engine #(
    parameter CLK_HZ     = 100_000_000,
    parameter MAX_ROWS   = 3072
)(
    input  wire        clk,
    input  wire        rst_n,
    input  wire        scan_start,     // 스캔 시작 트리거
    input  wire        scan_abort,     // 즉시 중단
    input  wire [15:0] n_rows,         // 실제 행 수
    input  wire [15:0] t_line_us,      // 행당 시간 [µs]
    input  wire [7:0]  t_gate_on_us,   // Gate ON 펄스 폭 [µs]

    output reg         gate_stv,
    output reg         gate_cpv,
    output reg         afe_row_valid,  // 현재 행 AFE 유효

    output reg         scan_done,
    output reg [15:0]  current_row
);

// CPV 생성: t_gate_on_us 주기의 50% duty cycle
// t_cpv_half = t_gate_on_us × (CLK_HZ/1_000_000) / 2
localparam CLK_PER_US = CLK_HZ / 1_000_000;  // 100 (100MHz 기준)

reg [15:0] cpv_counter;
reg [15:0] row_counter;
reg [31:0] line_counter;   // 행당 전체 시간 카운터

always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
        gate_stv     <= 1'b0;
        gate_cpv     <= 1'b0;
        scan_done    <= 1'b0;
        current_row  <= 16'd0;
        row_counter  <= 16'd0;
    end else if (scan_start && !scan_abort) begin
        // 1. STV 펄스 생성 (1 CLK 폭)
        // 2. CPV 클럭 n_rows회 생성
        // 3. 각 CLK 주기 = t_gate_on_us × CLK_PER_US
        // 4. t_line_us 전체 타이머로 행간 간격 제어
    end
end

endmodule
```

### 12.3 forward\_bias\_ctrl 모듈

```systemverilog
// forward_bias_ctrl.sv — Forward Bias 제어 모듈
// 취득 전후 포토다이오드 순방향 바이어스 인가

module forward_bias_ctrl #(
    parameter CLK_HZ = 100_000_000
)(
    input  wire  clk,
    input  wire  rst_n,
    input  wire  fb_start,        // Forward Bias 시작
    output reg   fb_done,         // 완료 신호

    // VPD 제어 출력
    output reg  [1:0] vpd_ctrl,   // 00=0V, 01=FWD+4V, 10=REV-1.5V

    // 파라미터 (MCU에서 설정)
    input  wire [7:0]  fb_rows_per_group,   // 8행 그룹
    input  wire [15:0] fb_switch_freq_khz,  // 100 kHz
    input  wire [15:0] fb_total_time_ms     // 30ms
);
// 8행 단위로 Forward Bias → Reverse Bias 순환
// 100kHz: 10µs ON, 10µs OFF 사이클
// 총 30ms = 3000 사이클
// 완료 후 vpd_ctrl = 2'b10 (Rev bias 복귀)
endmodule
```

---

## 13. MCU↔FPGA SPI 커맨드 시퀀스

### 13.1 정지영상 취득 명령 흐름

```
MCU → FPGA SPI 커맨드 시퀀스 (정지영상 모드):

1. 초기화 설정:
   WRITE REG_COMBO  = 조합 ID (0x01~0x07)
   WRITE REG_NROWS  = 행 수
   WRITE REG_NCOLS  = 열 수
   WRITE REG_TLINE  = 행당 시간 [µs]
   WRITE REG_CIC_EN = 0 또는 1

2. 모드 설정:
   WRITE REG_MODE   = 3'b000  // STATIC 모드

3. 취득 시작:
   WRITE REG_CTRL   = 8'h01   // START_ACQ 비트 세트

4. 상태 폴링:
   LOOP:
     READ REG_STATUS
     IF REG_STATUS[0] == READY: EXIT
     IF REG_STATUS[7] == ERROR: 에러 처리
     WAIT 10ms

5. 데이터 수신:
   DMA 전송: FPGA Frame Buffer → MCU RAM
   수신 완료 후: READ REG_STATUS[1] (XFER_DONE)

6. 후처리:
   MCU에서 래그 보정 계수 계산
   Host PC로 Ethernet 전송
```

### 13.2 레지스터 맵 완전 정의

| 주소 | 이름 | R/W | 비트 정의 |
|------|------|-----|-----------|
| 0x00 | REG\_CTRL | W | \[0\]=START\_ACQ, \[1\]=ABORT, \[2\]=CAL\_REQ, \[7\]=SOFT\_RST |
| 0x01 | REG\_STATUS | R | \[0\]=READY, \[1\]=XFER\_DONE, \[2\]=IN\_ACQ, \[7\]=ERROR |
| 0x02 | REG\_MODE | W | \[2:0\]=모드 (000~100) |
| 0x03 | REG\_COMBO | W | \[7:0\]=조합 ID (01~07) |
| 0x04 | REG\_NROWS | W | \[15:0\]=행 수 |
| 0x05 | REG\_NCOLS | W | \[15:0\]=열 수 |
| 0x06 | REG\_TLINE | W | \[15:0\]=행당 시간 \[µs\] |
| 0x07 | REG\_TRESET | W | \[7:0\]=pre-reset scan 횟수 |
| 0x08 | REG\_INT\_TIME\_L | W | 통합 시간 하위 16비트 \[µs\] |
| 0x09 | REG\_INT\_TIME\_H | W | 통합 시간 상위 16비트 |
| 0x0A | REG\_INT\_MODE | W | \[0\]=고정/이벤트, \[1\]=AEC 활성화 |
| 0x0B | REG\_FB\_EN | W | \[0\]=Forward Bias 활성화 |
| 0x0C | REG\_DARK\_CNT | W | \[7:0\]=다크 취득 장수 |
| 0x0D | REG\_TEMP | R | \[15:0\]=온도 (0.1°C 단위) |
| 0x0E | REG\_CIC\_EN | W | \[0\]=AFE2256 CIC 보정 |
| 0x0F | REG\_CIC\_PROFILE | W | \[1:0\]=CIC 프로파일 |
| 0x10 | REG\_RESET\_CNT | W | \[3:0\]=pre-reset 횟수 |
| 0x11 | REG\_RESET\_TH | W | \[15:0\]=reset 완료 임계값 \[DN\] |
| 0x12 | REG\_CORR\_EN | W | \[3:0\]=보정 활성화 비트맵 |
| 0x13 | REG\_LAG\_EN | W | \[0\]=래그 보정 활성화 |
| 0x14 | REG\_ERR | R | \[7:0\]=에러 플래그 |

---

## 14. 검증 계획 및 시뮬레이션 시나리오

### 14.1 시뮬레이션 시나리오

| 시나리오 | 검증 항목 | 합격 기준 |
|---|---|---|
| TC-STATIC-01 | Pre-reset scan 5회 후 PREP\_WAIT 진입 | 정확히 5 scan 사이클 후 S3 진입 |
| TC-STATIC-02 | PREP\_REQUEST → X\_RAY\_ENABLE 타이밍 | 500ms ± 5% 이내 |
| TC-STATIC-03 | X\_RAY\_OFF → SETTLE → READOUT 시작 | 5ms ± 10% 이내 |
| TC-STATIC-04 | C1 조합 Readout 완료 시간 | 2048 × 30µs = 61.44ms ± 1% |
| TC-STATIC-05 | C7 조합 Readout 완료 시간 | 3072 × 10µs = 30.72ms ± 1% |
| TC-STATIC-06 | Forward Bias 완료 후 VPD 복귀 | FB_done 신호 후 vpd\_ctrl=2'b10 |
| TC-STATIC-07 | 타임아웃 (30s 이내 X\_RAY\_ON 미수신) | ERR\_XRAY\_TIMEOUT 플래그 세트 |
| TC-STATIC-08 | 다크 취득 2장 평균 정확성 | 평균값 오차 < 0.1 DN |
| TC-STATIC-09 | 결함화소 보정 (Dead pixel 교체) | 이웃 8 픽셀 평균과 일치 |
| TC-STATIC-10 | REG\_MODE 변경으로 모드 전환 | 취득 중 변경 → ABORT 처리 |

### 14.2 하드웨어 검증 항목

| 항목 | 측정 도구 | 합격 기준 |
|---|---|---|
| Gate pulse 폭 | 오실로스코프 (500MHz) | T\_gate\_on ± 2µs |
| CPV 클럭 주파수 | 오실로스코프 | 조합별 T\_LINE에서 ±1% |
| AFE SYNC vs Gate CPV 타이밍 | 로직 분석기 | SYNC 어설션 Gate ON 후 1µs 이내 |
| 전 패널 Readout 시간 | 로직 분석기 | 조합별 이론값 ±2ms |
| VGH 파형 오버슈트 | 고압 프로브 | < 10% 오버슈트 |
| Forward Bias 전류 | 전류 프로브 | 20 pC/diode ± 20% |

### 14.3 임상 검증 항목

| 항목 | 방법 | 합격 기준 |
|---|---|---|
| 1st-frame lag | 스텝 응답 측정 | Forward Bias 적용 시 < 0.3% |
| SNR | IEC 62220-1 방법 | DQE(0) ≥ 0.7 |
| Uniformity | 플랫 필드 균일성 | ±5% 이내 |
| Defect pixel rate | 결함 맵 검사 | < 0.1% 총 픽셀 |
| Dark current | 30분 warm-up 후 | < 5 DN drift/hr |

---

*본 설계서는 research_04_drive_sequence_patents.md, research_02_gate_ic_control.md, research_03_roic_afe_optimal.md, research_05_lag_correction.md, research_06_calibration.md 기반 딥리서치 결과를 정지영상 전용으로 특화하여 작성되었습니다.*

*참조 특허: [EP2148500A1](https://patents.google.com/patent/EP2148500A1/en) (Carestream), [US5452338A](https://patents.google.com/patent/US5452338A/en) (GE), [US20130126742A1](https://patents.google.com/patent/US20130126742A1/en), [US7792251B2](https://patents.google.com/patent/US7792251B2/en)*  
*참조 논문: [Starman et al., Medical Physics 2011](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/), [Siemens Hoheisel et al., ISCMP 1996](https://www.mhoheisel.de/docs/ISCMP91996112.pdf)*
