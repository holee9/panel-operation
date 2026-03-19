# 모드별 패널 전원 설정 설계서

**문서 버전:** v1.0  
**작성일:** 2026-03-18  
**대상 시스템:** X-ray FPD — 7가지 부품 조합 (C1~C7)  
**적용 범위:** Gate IC 전원 (VGH/VGL), 포토다이오드 바이어스 (VPD), AFE 전원, 전원 시퀀싱, 모드 전환 절차

---

## 목차

1. [전원 도메인 구조](#1-전원-도메인-구조)
2. [동작 모드 정의 (M0~M5)](#2-동작-모드-정의-m0m5)
3. [패널별 모드별 전압 테이블](#3-패널별-모드별-전압-테이블)
4. [Gate IC 전원 시퀀싱 (NV1047 / NT39565D)](#4-gate-ic-전원-시퀀싱-nv1047--nt39565d)
5. [AFE 전원 시퀀싱 (AD71124 / AD71143 / AFE2256)](#5-afe-전원-시퀀싱-ad71124--ad71143--afe2256)
6. [포토다이오드 바이어스 전압 제어 (VPD\_BIAS)](#6-포토다이오드-바이어스-전압-제어-vpd_bias)
7. [모드 전환 절차 및 안정화 대기시간](#7-모드-전환-절차-및-안정화-대기시간)
8. [인러시 전류 관리](#8-인러시-전류-관리)
9. [비상 차단 시퀀스 (Emergency Shutdown)](#9-비상-차단-시퀀스-emergency-shutdown)
10. [전원 모니터링 및 보호 회로](#10-전원-모니터링-및-보호-회로)
11. [전원 제어 FPGA 모듈 명세](#11-전원-제어-fpga-모듈-명세)
12. [패널별 권장 전압값 요약](#12-패널별-권장-전압값-요약)

---

## 1. 전원 도메인 구조

### 1.1 시스템 전원 아키텍처

```
                    24V / 12V DC 입력 (외부 SMPS)
                           │
           ┌───────────────┼───────────────────────┐
           │               │                       │
    ┌──────▼──────┐  ┌──────▼──────┐  ┌────────────▼────────┐
    │  Gate IC    │  │  AFE/ROIC   │  │    FPGA/MCU         │
    │  전원 모듈  │  │  전원 모듈  │  │    전원 모듈        │
    │             │  │             │  │                     │
    │ VGH: +15~35V│  │ AVDD1:1.85V │  │ VCCINT: 0.95V       │
    │ VGL:  -5~-15V│ │ AVDD2:3.3V  │  │ VCCO_HP: 1.8V       │
    │ DVDD:  3.3V │  │ (12× 병렬)  │  │ VCCO_HD: 3.3V       │
    │ VPD:  -1.5V │  │             │  │ VCCO_MGT: 1.8V      │
    └─────────────┘  └─────────────┘  └─────────────────────┘
           │
    전원 시퀀서 (CPLD 또는 TI TPS3706)
    → FPGA GPIO 제어 신호로 각 레일 활성화/비활성화
```

### 1.2 전원 도메인별 역할

| 도메인 | 공급 전압 | 담당 부품 | 비고 |
|---|---|---|---|
| VGH (Gate High) | +15~35V | Gate IC 출력 (TFT Gate ON) | 패널 행별로 순차 활성화 |
| VGL (Gate Low) | −5~−15V | Gate IC 출력 (TFT Gate OFF) | 항상 VGH보다 먼저 인가 |
| VPD\_BIAS | −1.5V ~ +4V | 포토다이오드 바이어스 | 역바이어스/순바이어스 전환 |
| DVDD | +3.3V | Gate IC 디지털 로직 | 항상 On (VGL보다 먼저) |
| AVDD1 | +1.85V | AFE2256 아날로그 코어 | 먼저 인가 |
| AVDD2 | +3.3V | AFE2256 I/O 인터페이스 | AVDD1 안정화 후 |
| AVDD5B/F | +5V | AD71124/AD71143 아날로그 | 동시 또는 순차 |
| AVDDI | +5V | AD71124 내부 레귤레이터 | AVDD5 안정화 후 |
| DVDD\_AFE | +3.3V | AD71124 디지털 | 마지막 |

---

## 2. 동작 모드 정의 (M0~M5)

### 2.1 모드 개요

시스템 동작은 6가지 전원 모드로 구분된다. 각 모드는 소비 전력, 패널 응답, X-ray 취득 가능 여부가 다르다.

| 모드 | 명칭 | 전원 상태 | X-ray 취득 | 주요 용도 |
|---|---|---|---|---|
| **M0** | Power-Up / Power-Down | 모든 레일 0V | 불가 | 초기화 전/종료 후 |
| **M1** | ACTIVE (Full Power) | VGH ON + VGL ON + VPD REV | **가능** | 정지영상/형광투시 취득 |
| **M2** | Idle L1 (Scan-off) | VGH 0V + VGL ON + VPD REV | 불가 | 취득 대기, 단기 휴지 |
| **M3** | Idle L2 (Low Bias) | VGH IDLE + VGL IDLE + VPD LOW | 불가 | 중기 휴지, Dark 억제 |
| **M4** | Idle L3 (Deep Sleep) | Gate IC 전원 OFF + VPD 0V | 불가 | 장기 휴지, 절전 |
| **M5** | CAL / DARK | M1과 동일 | 다크만 취득 | 캘리브레이션 전용 |

### 2.2 모드별 소비 전력 (추정)

| 모드 | Gate IC 소비 | AFE 소비 | FPGA 소비 | 시스템 합계 |
|---|---|---|---|---|
| M0 | 0 mW | 0 mW | ~200 mW | ~200 mW |
| M1 | ~500 mW | ~80~960 mW | ~1,300 mW | ~1,900~2,760 mW |
| M2 | ~200 mW | ~50~600 mW | ~1,000 mW | ~1,250~1,800 mW |
| M3 | ~100 mW | ~10~120 mW | ~800 mW | ~910~1,020 mW |
| M4 | ~10 mW | ~5~60 mW | ~600 mW | ~615~670 mW |
| M5 | ~500 mW | ~80~960 mW | ~1,300 mW | M1과 동일 |

*AFE 소비: 단일 AFE(C1~C5) vs 12× AFE(C6~C7) 차이 반영*

---

## 3. 패널별 모드별 전압 테이블

### 3.1 R1717 / 1717 패널 (C1, C2, C3 조합)

Gate IC: NV1047 (VGG max +35V, VEE max −15V)

| 모드 | VGH (Gate ON) | VGL (Gate OFF) | VPD\_BIAS | DVDD | 비고 |
|---|---|---|---|---|---|
| M0 Power-Up | 0V | 0V | 0V | 0V | 완전 비전원 |
| M1 ACTIVE | **+20V** | **−10V** | **−1.5V** | 3.3V | 정상 취득 |
| M2 Idle L1 | 0V (driver off) | **−10V 유지** | **−1.5V 유지** | 3.3V | VGL·VPD 유지 |
| M3 Idle L2 | +5V (dummy 시만) | **−5V** | **−0.2V** | 3.3V | 저전력 dark 억제 |
| M4 Idle L3 | 0V | 0V | 0V | 0V | 드라이버 전원 OFF |
| M5 CAL/DARK | +20V | −10V | −1.5V | 3.3V | M1 동일 |

**VGH 마진:** NV1047 최대 +35V → 권장 +20V (57% 마진)  
**VGL 마진:** NV1047 최대 −15V → 권장 −10V (33% 마진)  

### 3.2 R1714 패널 (C4, C5 조합)

Gate IC: NV1047 (동일)

| 모드 | VGH | VGL | VPD\_BIAS | DVDD | 비고 |
|---|---|---|---|---|---|
| M0 | 0V | 0V | 0V | 0V | |
| M1 ACTIVE | **+20V** | **−10V** | **−1.5V** | 3.3V | |
| M2 Idle L1 | 0V | **−10V 유지** | **−1.5V 유지** | 3.3V | |
| M3 Idle L2 | +5V | **−5V** | **−0.2V** | 3.3V | |
| M4 Idle L3 | 0V | 0V | 0V | 0V | |
| M5 CAL/DARK | +20V | −10V | −1.5V | 3.3V | |

*R1714는 R1717과 동일 Gate IC 사용 → 동일 전압 설정 적용*

### 3.3 X239AW1-102 패널 (C6, C7 조합)

Gate IC: NT39565D ×6 (VGH max +35V, VGL max −15V, 권장 +28V / −12V)

| 모드 | VGH | VGL | VGG | VEE | VPD\_BIAS | VCC (DVDD) | 비고 |
|---|---|---|---|---|---|---|---|
| M0 | 0V | 0V | 0V | 0V | 0V | 0V | 완전 비전원 |
| M1 ACTIVE | **+28V** | **−12V** | +28V | −12V | **−1.5V** | 3.3V | 표준 취득 |
| M2 Idle L1 | 0V | **−12V 유지** | 0V | −12V | **−1.5V 유지** | 3.3V | VGL·VPD 유지 |
| M3 Idle L2 | +8V (dummy 시만) | **−6V** | +8V | −6V | **−0.2V** | 3.3V | 저전력 |
| M4 Idle L3 | 0V | 0V | 0V | 0V | 0V | 3.3V | NT39565D 로직만 유지 |
| M5 CAL/DARK | +28V | −12V | +28V | −12V | −1.5V | 3.3V | M1 동일 |

**NT39565D 특수 전압:**
- VCC: 3.3V (디지털 I/O 공급)
- VGG: Gate High 출력 (= VGH, +28V)
- VEE: Gate Low 출력 (= VGL, −12V)
- 전원 인가 순서: VCC → VGL(VEE) → VGH(VGG) — 반드시 준수

---

## 4. Gate IC 전원 시퀀싱 (NV1047 / NT39565D)

### 4.1 NV1047 파워업 시퀀스 (C1~C5)

```
[NV1047 파워업 시퀀스]

CRITICAL RULE: VGL must be stable BEFORE VGH is applied
               VGL 미인가 상태에서 VGH 인가 → Gate IC TFT 래치업 손상 위험

T=0 ms:    DVDD = 3.3V 인가 (NV1047 디지털 로직 공급)
           ├── 안정화 대기: 2ms
           └── FPGA: NV1047 SPI 초기화 시작 가능

T=5 ms:    VGL 레일 램프업 시작
           ├── 목표: −10V (C1~C5 기준)
           ├── 슬루 레이트: ≤5 V/ms (대기: ~2ms)
           └── 안정화 확인: ADC 모니터링으로 ±5% 이내 확인

T=10 ms:   ✓ VGL 안정화 확인 후 → VGH 램프업 시작
           ├── 목표: +20V (C1~C5 기준)
           ├── 슬루 레이트: ≤5 V/ms (대기: ~4ms)
           └── Soft-start: inrush 제한 (§8 참조)

T=15 ms:   ✓ VGH 안정화 확인
           ├── CPV 클럭 저주파수 테스트 모드 시작 (50 kHz)
           └── 첫 STV 펄스: 전체 패널 소거 scan 1회

T=20 ms:   Gate IC 초기화 완료
           └── FPGA: M1 ACTIVE 상태 진입 준비 완료
```

### 4.2 NT39565D 파워업 시퀀스 (C6, C7)

NT39565D는 5단계 전원 순서가 반드시 지켜져야 한다 ([Patent US8599182B2](https://patents.google.com/patent/US8599182B2/en)):

```
[NT39565D 파워업 시퀀스 — MANDATORY ORDER]

Step 1: VCC = 3.3V (디지털 I/O 공급)
        └── 대기: 5ms (POR 회로 완료)

Step 2: VGL (= VEE) 인가
        ├── 램프: 0V → −12V (슬루: ≤3 V/ms → 대기: 4ms)
        └── 안정화: 추가 3ms 대기

Step 3: 내부 부스트 회로 (VEE) 안정화 대기
        └── 대기: 5ms

Step 4: VGH (= VGG) 인가
        ├── 램프: 0V → +28V (슬루: ≤5 V/ms → 대기: 5.6ms)
        └── Soft-start 필수 (인러시 피크 2.5A 제한, §8 참조)

Step 5: NT39565D SPI 설정 (게인, 방향, 테스트 모드)
        ├── SPI 클럭: ≤10 MHz
        └── 설정 후: CPV 클럭 인가 (100 kHz), STV1 펄스

총 파워업 시간: ~25ms
```

```
타이밍 다이어그램 (NT39565D):

  VCC  ─────[3.3V]──────────────────────────────────
  VEE  ─────────────[−12V]──────────────────────────  ← VCC + 5ms
  VGG  ──────────────────────────────[+28V]─────────  ← VEE + 8ms
  STV1 ─────────────────────────────────────[PULSE]─  ← VGG + 2ms
  CPV  ─────────────────────────────────────[CLK]───

  시간: 0    5ms  10ms  15ms  20ms  25ms
```

### 4.3 파워다운 시퀀스 (역순)

```
[Gate IC 파워다운 — 파워업의 역순]

공통 규칙:
  - VGH를 먼저 내린 후 → VGL 다운
  - VGH drop rate: ≤5 V/ms (인덕티브 스파이크 방지)

NV1047 파워다운:
  1. STV 중단 → 모든 게이트 VGL 상태로 유지
  2. CPV 클럭 중단
  3. VGH: +20V → 0V (슬루: 5 V/ms → 4ms)
  4. VGL: −10V → 0V (슬루: 5 V/ms → 2ms)
  5. DVDD: 3.3V → 0V
  총 파워다운 시간: ~8ms

NT39565D 파워다운:
  1. STV 중단 (모든 게이트 VEE 상태 유지)
  2. CPV 클럭 중단
  3. VGG: +28V → 0V (슬루: 5 V/ms → 5.6ms)
  4. VEE 부스트 차단
  5. VGL(VEE): −12V → 0V (슬루: 3 V/ms → 4ms)
  6. VCC: 3.3V → 0V
  총 파워다운 시간: ~12ms
```

---

## 5. AFE 전원 시퀀싱 (AD71124 / AD71143 / AFE2256)

### 5.1 AD71124 파워업 시퀀스 (C1, C4, C6)

AD71124는 내부 LDO 레귤레이터를 포함하며 4가지 전원 레일이 있다:

```
[AD71124 파워업 시퀀스]

AD71124 전원 구조:
  AVDD5B = +5V (외부 아날로그 공급, 출력 바이어스 측)
  AVDD5F = +5V (외부 아날로그 공급, 적분기 측)
  AVDDI  = +5V (내부 LDO 입력 → 내부 2.5V 기준 생성)
  DVDD   = +3.3V (디지털 로직)

권장 파워업 순서:
  T=0 ms:   DVDD = 3.3V 인가
  T=2 ms:   AVDD5B = +5V 인가
  T=2 ms:   AVDD5F = +5V 인가  [AVDD5B와 동시 가능]
  T=4 ms:   AVDDI  = +5V 인가  [AVDD5 안정화 후]
  T=10 ms:  내부 LDO 기준전압 안정화 완료
             └── SPI 설정: 게인(IFS), 오프셋, 전원 모드
  T=15 ms:  AD71124 준비 완료

IFS (Full Scale) 설정:
  bit[5:0] = 6비트 (0x00~0x3F)
  권장 정지영상: IFS = 0x3F (최대 게인, 최저 노이즈 560 e⁻)
  권장 고선량: IFS = 0x10 (중간 게인)

파워다운 (역순):
  1. SPI: 전력절감 모드 설정 (Power-Down bit)
  2. AVDDI → 0V
  3. AVDD5F, AVDD5B → 0V
  4. DVDD → 0V
```

### 5.2 AD71143 파워업 시퀀스 (C2)

```
[AD71143 파워업 시퀀스]

AD71143은 AD71124 대비 저전력 설계 (5-bit IFS):
  전원 레일 구조는 AD71124와 유사
  특이사항: tLINE_min = 60µs (AD71124의 22µs 대비 2.7배 느림)

파워업:
  T=0 ms:   DVDD = 3.3V
  T=2 ms:   AVDD5B, AVDD5F = +5V
  T=5 ms:   AVDDI = +5V
  T=12 ms:  SPI 설정: IFS 5-bit 설정
  T=15 ms:  준비 완료

저전력 모드 (M3 Idle L2):
  SPI: 부분 셧다운 모드 활성화
  AVDDI: 내부 기준 유지 (빠른 재기동 위해)
  소비전류: 활성 대비 ~20% 수준
```

### 5.3 AFE2256 파워업 시퀀스 (C3, C5, C7)

```
[AFE2256 파워업 시퀀스 — 2레일 구조]

전원 레일:
  AVDD1 = 1.85V (아날로그 코어: 256채널 적분기 + CDS)
  AVDD2 = 3.3V  (I/O 및 출력 인터페이스)

CRITICAL: AVDD1 먼저 → AVDD2 나중 (역순 파워업 금지)

T=0 ms:    AVDD1 = 1.85V 램프업
           ├── 슬루: 1.85V/2ms = 0.925 V/ms
           └── 인러시: 12칩 × 추정 100µF = 1.2mF → 2.2A peak (§8 참조)

T=3 ms:    ✓ AVDD1 안정화 (±2% 이내)
           └── AVDD2 = 3.3V 램프업

T=5 ms:    ✓ AVDD2 안정화
           └── 내부 기준전압 안정화 대기: +2ms

T=7 ms:    MCLK 인가 시작
           ├── MCLK = 32 MHz (정지영상 기본)
           └── MCLK 안정화 대기: 2ms (10 사이클 이상)

T=10 ms:   SPI 설정:
           ├── PGA 게인 설정
           ├── Full-scale range 설정
           ├── Pipeline 모드 활성화 (선택)
           ├── CIC 프로파일 설정 (REG_CIC_PROFILE)
           └── Power mode: Active

T=15 ms:   SYNC 인가 가능 → 적분 시작 가능

파워다운 (역순):
  1. SYNC 비활성화 → 적분 중단
  2. MCLK 중단
  3. SPI: Nap mode (전류 거의 0으로 감소)
  4. AVDD2 → 0V
  5. AVDD1 → 0V

C7 조합 (12× AFE2256) 특이사항:
  - 12칩 동시 파워업 → 인러시 전류 관리 필수 (§8 참조)
  - AVDD1 팬아웃: 스타 배선 + 각 칩 100µF decoupling
  - MCLK 팬아웃: CDCLVP1208 또는 LTC6957 클럭 분배 IC
  - SYNC 팬아웃: 등길이 배선 (±200ps 이내)
```

---

## 6. 포토다이오드 바이어스 전압 제어 (VPD\_BIAS)

### 6.1 VPD\_BIAS 모드별 전압

포토다이오드 바이어스는 패널 동작 모드에 따라 3가지 레벨로 전환된다:

| 모드 | VPD\_BIAS | 상태 | 목적 |
|---|---|---|---|
| 역바이어스 (표준) | **−1.5V** | 정상 감지 모드 | Dark current 최소화 + 최대 감도 |
| 저바이어스 (idle) | **−0.2V** | Idle 절전 | 접합 전계 유지, Dark current 저감 |
| 순바이어스 (Forward Bias) | **+4V** | 트랩 초기화 | Lag 제거용, 취득 전후 인가 |
| 0V | **0V** | 슬립 모드 | 완전 비전원 상태 (M0, M4) |

### 6.2 VPD\_BIAS 전환 절차

```
[VPD_BIAS 전환 절차 — Starman et al. 2011 기반]

역바이어스 → 순바이어스 (Forward Bias 인가):
  1. VPD → 0V (역바이어스 해제, 대기 10µs)
  2. VPD → +4V (순방향 인가)
     ├── 슬루: ≤1 V/µs (급격한 전압 변화로 인한 노이즈 방지)
     └── 안정화: 50µs
  3. Forward Bias 유지: 30ms
     ├── 스위칭 주파수: 100 kHz (ON 10µs / OFF 10µs)
     └── 전하: 20 pC/photodiode
  4. VPD → 0V (10µs)
  5. VPD → −1.5V (역바이어스 복귀)
     └── 안정화: 100µs

순바이어스 → 역바이어스 (즉시 복귀):
  1. VPD → 0V (1µs)
  2. VPD → −1.5V
     └── 안정화: 200µs

역바이어스 → 저바이어스 (M1→M3 전환):
  1. VPD: −1.5V → −0.2V
     ├── 슬루: ≤0.1 V/ms (완만한 전환)
     └── 안정화: 2ms
```

### 6.3 VPD\_BIAS 회로 구현 (FPGA 제어)

```
FPGA → DAC → 아날로그 드라이버 → VPD_BIAS

DAC 사양:
  해상도: 12-bit (4096 레벨)
  범위: −5V ~ +5V
  분해능: 10V / 4096 ≈ 2.44 mV/LSB
  업데이트 속도: ≥1 MHz (Forward Bias 스위칭용)

FPGA DAC 제어 (SPI):
  vpd_dac_code = (vpd_voltage_mv + 5000) / 2.44
  // 예: −1.5V = (−1500 + 5000) / 2.44 ≈ 1434
  // 예: +4.0V = (4000 + 5000) / 2.44 ≈ 3689
```

---

## 7. 모드 전환 절차 및 안정화 대기시간

### 7.1 모드 전환 매트릭스

```
전환 경로 및 소요 시간:

FROM\TO  │  M0   │  M1   │  M2   │  M3   │  M4   │  M5
─────────┼───────┼───────┼───────┼───────┼───────┼───────
M0       │  —    │ 25ms  │ 15ms  │ 12ms  │  5ms  │ 25ms
M1       │ 12ms  │  —    │  5ms  │ 15ms  │ 25ms  │  1ms
M2       │ 10ms  │ 10ms  │  —    │  8ms  │ 15ms  │ 10ms
M3       │  8ms  │ 20ms  │  8ms  │  —    │ 10ms  │ 20ms
M4       │  3ms  │ 35ms  │ 30ms  │ 25ms  │  —    │ 35ms
M5       │ 12ms  │  1ms  │  5ms  │ 15ms  │ 25ms  │  —
```

### 7.2 주요 전환 절차 상세

#### M0 → M1 (파워업 → 정상 취득)

```
[M0 → M1 전환: 완전 파워업 시퀀스]

C1~C5 (NV1047 기반):
  Phase 1: Gate IC 파워업 (§4.1)     → 20ms
  Phase 2: AFE 파워업 (§5.1~5.3)    → 15ms (병렬 가능)
  Phase 3: VPD_BIAS 램프업
           → 0V → −1.5V (슬루: 0.5 V/ms → 3ms)
  Phase 4: 온도 센서 읽기 및 확인
  Phase 5: 첫 Dummy Scan 1회 (패널 초기화)
  총 소요: ~25ms + 첫 dummy scan 시간

C6~C7 (NT39565D 기반):
  Phase 1: Gate IC 파워업 (§4.2)     → 25ms
  Phase 2: AFE 파워업 (병렬)          → 15ms
  Phase 3: VPD_BIAS 램프업            → 3ms
  Phase 4: 6× NT39565D SYNC 확인
  Phase 5: 첫 Dummy Scan 1회
  총 소요: ~30ms + 첫 dummy scan 시간
```

#### M1 → M2 (취득 완료 → 단기 대기)

```
[M1 → M2 전환: Scan Off, Bias 유지]

1. 현재 진행 중인 Gate Scan 완료 대기 (max T_readout)
2. CPV 클럭 중단
3. VGH: +20V(+28V) → 0V
   ├── 슬루: 5 V/ms
   └── 대기: 4ms (C1~C5) / 6ms (C6~C7)
4. VGL: 유지 (−10V 또는 −12V 그대로)
5. VPD_BIAS: 유지 (−1.5V 그대로)
6. AFE: Nap 모드 (SPI 설정)

M2 상태에서 M1 복귀:
  1. AFE Nap 해제 (SPI)
  2. VGH 램프업 → +20V(+28V)  (안정화 5ms)
  3. CPV 재시작 → Dummy Scan 2~3회
  총 복귀 시간: ~10ms + dummy scan
```

#### M1 → M3 (취득 완료 → 중기 절전)

```
[M1 → M3 전환: Low Power Idle]

1. Gate Scan 완료
2. VGH → 0V
3. VGL: −10V → −5V (C1~C5) / −12V → −6V (C6~C7)
   ├── 슬루: ≤2 V/ms
   └── 안정화: 5ms
4. VPD_BIAS: −1.5V → −0.2V
   └── 안정화: 3ms
5. AFE: 저전력 모드 (SPI)
6. Gate IC: Idle 클럭 중단

M3 → M1 복귀 소요:
  1. VGL: −5V → −10V (3ms)
  2. VGH: 0V → +20V (4ms)
  3. VPD: −0.2V → −1.5V (2ms)
  4. AFE 활성 모드 복귀 (SPI, 5ms)
  5. Dummy Scan 5~8회 (패널 재안정화)
  총: ~20ms + dummy scan
```

#### M1 → M4 (장기 절전)

```
[M1 → M4 전환: Deep Sleep]

경고: M4 복귀 시 콜드 부팅 수준의 초기화 필요

1. M1 → M2 전환 수행
2. AFE: 완전 파워다운 (§5 역순)
3. VGL: −10V → 0V
4. VPD_BIAS: −1.5V → 0V
5. Gate IC: 파워다운 (§4 역순)
6. DVDD: 3.3V → 0V

M4 → M1 복귀: 콜드 파워업 수준 (~35ms + 30~50회 dummy scan)
```

### 7.3 모드 전환 시 VGH/VGL 안정화 대기

| 전환 | 변화량 | 슬루 레이트 | 안정화 대기 |
|---|---|---|---|
| 0V → VGH (+20V) | 20V | 5 V/ms | 4ms + 1ms (settle) |
| 0V → VGH (+28V) | 28V | 5 V/ms | 6ms + 1ms (settle) |
| VGH → 0V | −20V / −28V | 5 V/ms | 4~6ms |
| 0V → VGL (−10V) | 10V | 5 V/ms | 2ms + 1ms |
| VGL 레벨 변경 | ±5V | 2 V/ms | 2.5ms |
| VPD → −1.5V | ~1.5V | 0.5 V/ms | 3ms |
| VPD → −0.2V | ~0.3V 감소 | 0.1 V/ms | 3ms |

---

## 8. 인러시 전류 관리

### 8.1 VGH 인러시 분석

```
[VGH 인러시 전류 계산]

소스 용량:
  TFT Gate-to-source 기생 용량: ~30 fF/crossing
  C6/C7 (3072 열 × 6 Gate IC): 3072 × 30 fF × 6 = 553 pF
  Gate IC 전원 핀 디커플링 캐패시터: 6 × 10µF = 60µF (주요 인러시 소스)
  총 유효 용량: ~60µF

VGH 인러시 전류 (Soft-start 없이):
  I_inrush = C × dV/dt = 60µF × 28V / 1ms ≈ 1.68A (C6/C7)
  I_inrush = C × dV/dt = 30µF × 20V / 1ms ≈ 0.60A (C1~C5)

목표: VGH 인러시 < 2.5A (전원 IC 한계)

완화 방법:
  1. Soft-start: 슬루 레이트 ≤5 V/ms (PMIC 설정)
     → I_inrush ≤ 60µF × 28V / 5.6ms = 300mA ✓
  2. NTC 서미스터: 10Ω, 3A 정격 (초기 전류 제한)
     → 정상 동작 후 Bypass Relay로 우회
  3. TPS65150 PMIC: VGH/VGL 통합 슬루 제어
```

### 8.2 AFE2256 인러시 (C7 — 12칩)

```
[AFE2256 × 12 인러시 계산]

AVDD1 (1.85V) 인러시:
  12칩 × 100µF (추정 디커플링) = 1.2mF
  슬루 없이: I_inrush = 1.2mF × 1.85V / 1ms = 2.22A → 과도

  완화 (Soft-start 2ms 램프):
  I_inrush = 1.2mF × 1.85V / 2ms = 1.11A ✓

AVDD2 (3.3V) 인러시:
  12칩 × 50µF = 600µF
  슬루 2ms: I_inrush = 600µF × 3.3V / 2ms = 990mA ✓
```

### 8.3 전원 제어 IC 권장 부품

| 레일 | 제어 IC | 특징 |
|---|---|---|
| VGH (+20~35V) | TPS65150 | VGH/VGL/VCOM 통합 제어, Soft-start 내장 |
| VGL (−10~−15V) | TPS65150 (부스트/반전) | VGH와 연동 시퀀싱 |
| AVDD1 (1.85V) | TPS62135 | 2A 벅, Soft-start, Tracking |
| AVDD2 (3.3V) | TPS62130 | 3A 벅, 빠른 과도 응답 |
| VPD (−1.5V) | DAC + 연산증폭기 | 12-bit DAC (±5V 범위), Forward Bias 스위칭 |
| 시퀀서 | TI TPS3706 or CPLD | 멀티 레일 순서 제어, 폴트 감지 |

---

## 9. 비상 차단 시퀀스 (Emergency Shutdown)

### 9.1 비상 차단 트리거 조건

```
비상 차단(Emergency Shutdown) 발생 조건:
  1. VGH 과전압: VGH > VGH_MAX + 10% (NV1047: >38.5V, NT39565D: >38.5V)
  2. VGH 저전압: VGH < VGH_MIN − 10% (예: <18V)
  3. VPD 과전압: VPD > +5V (포토다이오드 파손 위험)
  4. 온도 초과: 패널 온도 > 45°C
  5. FPGA 내부 오류: PLL 잠금 해제, 메모리 ECC 에러
  6. 외부 비상 정지 신호: EMERGENCY_STOP GPIO (H/W 버튼)
```

### 9.2 비상 차단 시퀀스

```
[Emergency Shutdown Sequence]

우선순위: 안전 > 속도

Step 1: X_RAY_ENABLE 즉시 LOW (0ms)
        └── generator로의 노출 승인 즉시 차단

Step 2: Gate Scan 즉시 중단 (0ms)
        ├── CPV 클럭 강제 중단
        └── 모든 Gate → VGL 상태로 고정

Step 3: VPD_BIAS → 0V (1ms)
        └── 포토다이오드 바이어스 즉시 해제

Step 4: AFE 강제 셧다운 (1ms)
        └── SPI Power-Down 커맨드 + AVDD2 차단

Step 5: VGH 강제 차단 (5ms)
        ├── VGH 드라이버 disable
        └── 방전 저항 연결 (VGH 빠른 방전)

Step 6: VGL 차단 (8ms)
        └── VGL 레일 방전

Step 7: 상태 보고 (10ms)
        ├── ERR_FLAGS 레지스터 기록
        ├── MCU 인터럽트 발생
        └── Host PC에 긴급 알림 전송

Step 8: 전원 고립 상태 유지
        └── 수동 재기동 명령(REG_CTRL[7]=SOFT_RST) 까지 대기
```

### 9.3 FPGA 비상 차단 로직

```systemverilog
// emergency_shutdown.sv
// 하드웨어 레벨 비상 차단 — 최우선 처리

module emergency_shutdown (
    input  wire        clk,
    input  wire        rst_n,

    // 트리거 입력
    input  wire        hw_emergency_in,   // 하드웨어 비상 버튼
    input  wire [11:0] vgh_adc_code,      // VGH ADC 모니터링
    input  wire [7:0]  temp_sensor,       // 온도 센서
    input  wire        fpga_pll_locked,   // FPGA PLL 상태
    input  wire        mem_ecc_err,       // 메모리 ECC 에러

    // 즉각 차단 출력 (동기/비동기 혼용)
    output reg         xray_enable_n,     // X-ray Enable 비활성화
    output reg         gate_oe_n,         // Gate IC Output Disable
    output reg         vpd_zero_req,      // VPD → 0V 요청
    output reg         afe_shutdown_req,  // AFE 셧다운 요청
    output reg         vgh_disable_req,   // VGH 차단 요청

    // 상태 및 에러
    output reg [7:0]   shutdown_reason,   // 비상 차단 사유 코드
    output reg         shutdown_active    // 차단 상태 플래그
);

// 비동기 비상 신호 (클럭 대기 없이 즉각 반응)
always_comb begin
    if (hw_emergency_in || !fpga_pll_locked || mem_ecc_err) begin
        xray_enable_n = 1'b0;  // 즉각 차단
        gate_oe_n     = 1'b1;  // Gate 출력 비활성화
    end
end

// ADC 기반 과전압 감지 (동기)
localparam VGH_OV_CODE  = 12'd3800;  // >38V 기준 (12-bit ADC, 40V 풀스케일)
localparam VGH_UV_CODE  = 12'd1800;  // <18V 기준
localparam TEMP_OV_CODE = 8'd200;    // >50°C (2°C/LSB 기준)

always_ff @(posedge clk) begin
    if (vgh_adc_code > VGH_OV_CODE) begin
        shutdown_reason <= 8'h01;  // VGH 과전압
        shutdown_active <= 1'b1;
    end else if (vgh_adc_code < VGH_UV_CODE && shutdown_active == 0) begin
        shutdown_reason <= 8'h02;  // VGH 저전압
        shutdown_active <= 1'b1;
    end else if (temp_sensor > TEMP_OV_CODE) begin
        shutdown_reason <= 8'h04;  // 온도 초과
        shutdown_active <= 1'b1;
    end
end

endmodule
```

---

## 10. 전원 모니터링 및 보호 회로

### 10.1 모니터링 대상 및 임계값

| 전원 레일 | 정상 범위 | 경고 임계 | 오류 임계 | 샘플링 주기 |
|---|---|---|---|---|
| VGH (C1~C5) | +18~22V | <17V 또는 >23V | <15V 또는 >25V | 10ms |
| VGH (C6~C7) | +26~30V | <25V 또는 >31V | <22V 또는 >33V | 10ms |
| VGL (C1~C5) | −9~−11V | >−8V 또는 <−12V | >−6V 또는 <−14V | 10ms |
| VGL (C6~C7) | −11~−13V | >−10V 또는 <−14V | >−8V 또는 <−15V | 10ms |
| VPD\_BIAS | −2~0V | >0V | >+0.5V (위험!) | 1ms |
| AVDD1 | 1.80~1.90V | <1.75V 또는 >1.95V | <1.65V 또는 >2.0V | 50ms |
| AVDD2 | 3.15~3.45V | <3.0V 또는 >3.6V | <2.8V 또는 >3.8V | 50ms |
| 패널 온도 | 15~40°C | <10°C 또는 >42°C | <5°C 또는 >45°C | 1s |

### 10.2 보호 동작 정책

```
전원 보호 동작 계층:

Level 0 (정보): 정상 범위 내 → 로그 기록만
Level 1 (경고): 경고 임계 초과
  → MCU에 경고 플래그 전달
  → 모드 M2 또는 M3으로 자동 강등
  → 연속 모니터링 유지

Level 2 (오류): 오류 임계 초과 또는 VPD 양전압
  → 취득 즉시 중단 (ABORT)
  → 모드 M4(Deep Sleep)로 전환
  → 에러 코드 기록
  → 수동 확인 후 재기동

Level 3 (긴급): VPD > +0.5V, 온도 > 45°C, HW 비상
  → Emergency Shutdown (§9.2 전체 시퀀스)
  → 전원 완전 차단
  → 재기동 잠금 (require manual RESET)
```

### 10.3 FPGA ADC 모니터링 회로

```
전원 모니터링 ADC:
  - 12-bit SAR ADC (FPGA 내장 XADC 또는 외부 AD7699)
  - 멀티플렉서: 8~16채널 (모든 전원 레일 스캔)
  - 샘플링: 라운드 로빈 방식 (모든 채널 순차 샘플)
  - 샘플 주기: 1ms (VPD) ~ 1s (온도)

FPGA XADC 활용 (Xilinx Kintex UltraScale):
  - XADC: 1Msps, ±0.5 LSB INL
  - 외부 입력 채널 최대 16개
  - FPGA 온도 내장 센서 (칩 접합 온도)
  - AXI 인터페이스로 MCU 접근 가능

레지스터 맵 (전원 모니터링):
  0x20 REG_VGH_ADC:    VGH ADC 코드 [12-bit]
  0x21 REG_VGL_ADC:    VGL ADC 코드
  0x22 REG_VPD_ADC:    VPD ADC 코드
  0x23 REG_AVDD1_ADC:  AVDD1 ADC 코드
  0x24 REG_AVDD2_ADC:  AVDD2 ADC 코드
  0x25 REG_TEMP_ADC:   온도 센서 ADC 코드
  0x26 REG_PWR_STATUS: 전원 상태 플래그
  0x27 REG_PWR_ERR:    전원 에러 플래그
```

---

## 11. 전원 제어 FPGA 모듈 명세

### 11.1 power\_sequencer 모듈

```systemverilog
// power_sequencer.sv — 전원 시퀀싱 제어 모듈
// M0~M5 전환 시 전원 레일 순서 및 타이밍 관리

module power_sequencer #(
    parameter CLK_HZ = 100_000_000
)(
    input  wire        clk,
    input  wire        rst_n,

    // 모드 제어 (MCU → FPGA)
    input  wire [2:0]  target_mode,    // 목표 전원 모드 M0~M5
    output reg  [2:0]  current_mode,   // 현재 전원 모드
    output reg         mode_stable,    // 전환 완료 플래그

    // 조합 선택 (전압값 결정)
    input  wire [7:0]  reg_combo,      // C1~C7

    // 전원 제어 출력
    output reg         vgh_en,         // VGH 활성화
    output reg         vgl_en,         // VGL 활성화
    output reg  [7:0]  vgh_dac,        // VGH DAC 설정값
    output reg  [7:0]  vgl_dac,        // VGL DAC 설정값 (절대값)
    output reg         vpd_en,
    output reg  [11:0] vpd_dac,        // VPD DAC 설정값
    output reg         dvdd_en,
    output reg         avdd1_en,
    output reg         avdd2_en,

    // 비상 차단
    input  wire        emergency_in,
    output reg         emergency_ack,

    // 모니터링
    input  wire [11:0] vgh_adc,
    input  wire [11:0] vgl_adc,
    input  wire [11:0] vpd_adc,
    output reg  [7:0]  pwr_err_flags
);

// 전압 설정 LUT (조합별)
function [7:0] get_vgh_dac;
    input [7:0] combo;
    case (combo)
        8'h01, 8'h02, 8'h03, 8'h04, 8'h05: get_vgh_dac = 8'd20; // 20V
        8'h06, 8'h07:                        get_vgh_dac = 8'd28; // 28V
        default:                             get_vgh_dac = 8'd0;
    endcase
endfunction

// 모드 전환 시퀀서 FSM
typedef enum logic [3:0] {
    PS_STABLE  = 4'd0,
    PS_RAMP_UP = 4'd1,
    PS_RAMP_DN = 4'd2,
    PS_WAIT    = 4'd3,
    PS_EMERGENCY = 4'd15
} ps_state_t;

ps_state_t ps_state;

endmodule
```

### 11.2 전원 관련 레지스터 맵

| 주소 | 이름 | R/W | 설명 |
|---|---|---|---|
| 0x18 | REG\_MODE\_REQ | W | 요청 전원 모드 M0~M5 |
| 0x19 | REG\_MODE\_CURR | R | 현재 전원 모드 |
| 0x1A | REG\_MODE\_STABLE | R | \[0\]=모드 전환 완료 |
| 0x1B | REG\_VGH\_SET | W | VGH 설정값 \[V\] |
| 0x1C | REG\_VGL\_SET | W | VGL 설정값 \[V, 절대값\] |
| 0x1D | REG\_VPD\_SET\_L | W | VPD 설정값 하위 8비트 \[mV\] |
| 0x1E | REG\_VPD\_SET\_H | W | VPD 설정값 상위 4비트 |
| 0x1F | REG\_PWR\_FORCE | W | 강제 모드 전환 (비상용) |
| 0x20~0x27 | REG\_MON\_* | R | 전원 모니터링 ADC 값 |

---

## 12. 패널별 권장 전압값 요약

### 12.1 C1~C3 / C4~C5 (1717 / R1717 / R1714 패널 + NV1047)

| 전압 레일 | M1 ACTIVE | M2 Idle L1 | M3 Idle L2 | M4 Deep Sleep | 비고 |
|---|---|---|---|---|---|
| VGH | **+20V** | 0V | +5V (dummy 시만) | 0V | NV1047 max +35V |
| VGL | **−10V** | **−10V 유지** | **−5V** | 0V | M2에서 유지 필수 |
| VPD\_BIAS | **−1.5V** | **−1.5V 유지** | **−0.2V** | 0V | 역바이어스 |
| DVDD | 3.3V | 3.3V | 3.3V | 0V | 항상 GateIC 전 인가 |
| AVDD5B/F | +5V | +5V | +5V | 0V | AD71124/AD71143 |
| AVDDI | +5V | +5V | 저전력 | 0V | |
| AVDD1 | 1.85V | 1.85V | 1.85V | 0V | AFE2256 (C3,C5) |
| AVDD2 | 3.3V | 3.3V | 3.3V | 0V | AFE2256 (C3,C5) |
| MCLK (AFE2256) | 32 MHz | 32 MHz | 8 MHz (분주) | Off | C3,C5 전용 |

**VGH/VGL 파워업 순서:** DVDD → VGL → VGH (반드시 준수)

### 12.2 C6~C7 (X239AW1-102 패널 + NT39565D ×6)

| 전압 레일 | M1 ACTIVE | M2 Idle L1 | M3 Idle L2 | M4 Deep Sleep | 비고 |
|---|---|---|---|---|---|
| VCC (DVDD) | 3.3V | 3.3V | 3.3V | 3.3V | NT39565D 항상 유지 |
| VGG (VGH) | **+28V** | 0V | +8V (dummy 시) | 0V | NT39565D max +35V |
| VEE (VGL) | **−12V** | **−12V 유지** | **−6V** | 0V | M2에서 유지 필수 |
| VPD\_BIAS | **−1.5V** | **−1.5V 유지** | **−0.2V** | 0V | |
| AVDD1 (×12) | 1.85V | 1.85V | 1.85V | 0V | AFE2256 ×12 (C7) |
| AVDD2 (×12) | 3.3V | 3.3V | 3.3V | 0V | AFE2256 ×12 (C7) |
| AVDD5B/F (×12) | +5V | +5V | +5V | 0V | AD71124 ×12 (C6) |
| MCLK | 32 MHz | 32 MHz | 8 MHz | Off | AFE2256 (C7) |

**NT39565D 파워업 순서:** VCC → VGL(VEE) → VGH(VGG) (반드시 준수)  
**인러시 주의:** VGH +28V, 30µF+ → 반드시 Soft-start (§8 참조)

### 12.3 Forward Bias 전압 파라미터 (전 조합 공통)

| 파라미터 | 값 | 비고 |
|---|---|---|
| Forward Bias 전압 | **+4V** | 표준 순방향 바이어스 |
| 전류 (per photodiode) | **20 pC** | 표준 전하량 |
| 스위칭 주파수 | **100 kHz** | ON/OFF 각 10µs |
| 적용 시간 | **30ms** | 취득 전 또는 후 |
| 역바이어스 복귀 | **−1.5V** | 항상 동일 |
| 복귀 대기 | **100µs** | 안정화 |

### 12.4 온도별 VPD 보정 (선택 적용)

Dark current는 온도에 따라 지수적으로 증가하므로, 고온 환경에서는 VPD 바이어스를 소폭 강화할 수 있다:

| 패널 온도 | 권장 VPD\_BIAS | Dark current 배수 | 비고 |
|---|---|---|---|
| 15°C | −1.0V | × 0.5 (기준 대비) | 저온, 바이어스 완화 가능 |
| 20°C | **−1.5V** | × 1.0 (기준) | 표준 동작점 |
| 25°C | **−1.5V** | × 1.4 | 표준 유지 |
| 30°C | **−1.8V** | × 2.0 | 강화 바이어스 권장 |
| 35°C | **−2.0V** | × 2.8 | 최대 강화 |
| 40°C | **−2.5V** | × 4.0 | 경고 범위 |

---

*본 설계서는 research_07_multi_afe_advanced.md, research_02_gate_ic_control.md, research_04_drive_sequence_patents.md 기반 딥리서치 결과 및 데이터시트(NV1047, NT39565D, AD71124, AFE2256) 분석을 바탕으로 작성되었습니다.*

*참조 특허: [US8599182B2](https://patents.google.com/patent/US8599182B2/en) (Gate IC Power Sequence), [EP2148500A1](https://patents.google.com/patent/EP2148500A1/en) (Power State Machine)*  
*참조 자료: [PCB Artists VGH/VGL Guide](https://pcbartists.com/design/power-supply/vgh-vgl-vcom-avdd-voltage-generation-schematic-tft-lcd/), [Analog Devices AN165](https://www.analog.com/media/en/technical-documentation/application-notes/an165fa.pdf)*  
*참조 논문: [Starman et al., Medical Physics 2011](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/) (Forward Bias Protocol)*
