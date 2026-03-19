# ROIC/AFE 최적 구동 리서치
## X-ray Flat Panel Detector: AD71124, AD71143, AFE2256 Type

> **연구 날짜**: 2026년 3월 18일  
> **대상 디바이스**: ADAS1256 (AD71124), AD71143, AFE2256 (TI)  
> **용도**: 256채널 16-bit X-ray FPD 시스템, 12-chip 어레이, FPGA 인터페이스 설계

---

## 목차

1. [AFE 디바이스 비교 요약](#1-afe-디바이스-비교-요약)
2. [CDS (Correlated Double Sampling)](#2-cds-correlated-double-sampling)
3. [AFE 적분 최적화](#3-afe-적분-최적화)
4. [TI AFE2256 CIC (Charge Injection Compensation)](#4-ti-afe2256-cic-charge-injection-compensation)
5. [멀티칩 AFE 동기화](#5-멀티칩-afe-동기화)
6. [LVDS 인터페이스 타이밍](#6-lvds-인터페이스-타이밍)
7. [AFE 파워업 시퀀스](#7-afe-파워업-시퀀스)
8. [파이프라인 리드아웃 모드](#8-파이프라인-리드아웃-모드)
9. [노이즈 최적화](#9-노이즈-최적화)
10. [채널 간 크로스토크](#10-채널-간-크로스토크)
11. [온도 영향 및 다크 커런트](#11-온도-영향-및-다크-커런트)
12. [FPGA 설계 함의 요약](#12-fpga-설계-함의-요약)

---

## 1. AFE 디바이스 비교 요약

| 파라미터 | ADAS1256 (AD71124) | AD71143 | AFE2256 (TI) |
|---------|------------------|---------|-------------|
| **채널 수** | 256ch | 256ch | 256ch |
| **분해능** | 16-bit | 16-bit | 16-bit (내장 SAR ADC) |
| **풀스케일 범위** | 최대 32 pC (조정 가능) | 16 pC (저전력 특화) | 0.6 ~ 9.6 pC (6단계) |
| **최소 라인 시간** | 22 µs | 60 µs | < 20 µs |
| **노이즈 (대표값)** | 560 e⁻ @ 2 pC | ~800 e⁻ (추정) | 750 e⁻ @ 1.2 pC |
| **출력 인터페이스** | LVDS 자기동기식 | LVDS | LVDS 직렬 (DCLK, DOUT, FCLK) |
| **클록** | ACLK (외부 단일) | ACLK | MCLK (내부 TG 기반) |
| **ADC 유형** | 통합 16-bit 고속 ADC | 통합 ADC | 4× 16-bit SAR ADC (256:4 MUX) |
| **CDS** | 내장, 듀얼 뱅킹 | 내장 | 내장 듀얼 뱅킹 CDS |
| **CIC** | 해당 없음 | 해당 없음 | ✅ 내장 TFT CIC 기능 |
| **파이프라인 모드** | 지원 (IWR) | 지원 제한 | PIPELINE_EN 레지스터 지원 |
| **SPI 구성** | SPI + SDO 데이지체인 | SPI | SPI (SDATA/SDOUT/SCLK/SEN) |
| **SYNC 핀** | ✅ SYNC 있음 | ✅ | ✅ SYNC 핀 |
| **패키지** | SOF (System-on-Flex) | SOF | COF (Chip-on-Film) |
| **공급 전압** | AVDD5F/AVDD5B/AVDDI | 유사 | AVDD1=1.85V, AVDD2=3.3V |
| **전력 모드** | NAP, 풀 파워다운, 슬립 | NAP 모드 | NAP, 총 파워다운 |

### 적용 가이드라인

```
저선량/정적 영상    → ADAS1256 @ 높은 IFS (16~32 pC), 긴 tINT
고속/형광투시       → ADAS1256 @ 22µs 라인타임, IFS=2pC
저전력/휴대용       → AD71143 (저전력 최적화, 16pC 범위)
TFT CIC 필요 시     → AFE2256 (내장 CIC, 자동 보상)
```

---

## 2. CDS (Correlated Double Sampling)

### 2.1 원리

CDS는 두 번의 샘플링으로 kTC 리셋 노이즈, 1/f 노이즈, DC 오프셋을 제거하는 기법이다.

```
V_signal = V_SHS - V_SHR

V_SHR: 적분기 리셋 후 레벨 샘플 (리셋 노이즈 포함)
V_SHS: TFT 온 → 전하 전송 → 신호 샘플 (리셋 노이즈 + 신호)
출력  : V_SHS - V_SHR = 순수 신호 (노이즈 상쇄)
```

**X-ray FPD 전형적 타이밍 시퀀스 ([TI AFE0064 데이터시트 참고](https://www.ti.com/lit/gpn/AFE0064)):**

```
                    IRST
          ┌─────┐
──────────┘     └──────────────────────────
                    SHR (리셋 레벨 샘플)
                 ┌┐
─────────────────┘└─────────────────────────
                    INTG (TFT 온)
                    ├── tINT ──┤
─────────────────────┐         └────────────
                               SHS (신호 레벨 샘플)
                               ┌┐
───────────────────────────────┘└────────────
```

**타이밍 파라미터 (AFE0064 기준, ADAS1256/AD71124 유사):**

| 심볼 | 설명 | 최소 | 단위 |
|------|------|------|------|
| t1 | IRST/SHR/SHS high 기간 | 30 | ns |
| t2 | IRST 하강 → 첫 번째 CLK 상승 셋업 | 30 | ns |
| t3 | 133번째 CLK 상승 → SHR 상승 딜레이 | 400 | ns |
| t4 | SHR 상승 → INTG 상승 딜레이 | 30 | ns |
| t5 | INTG high 기간 (TFT 온 시간) | 14 | µs |
| t6 | INTG 하강 → SHS 상승 딜레이 | 4.5 | µs |
| t7 | SHS 상승 → IRST 상승 딜레이 | 30 | ns |
| t8 | SHS 상승 → STI 상승 딜레이 | 30 | ns |
| t_scan | 총 스캔 시간 (최소) | 28.3 | µs |

### 2.2 kTC 노이즈 상쇄 메커니즘

리셋 동작 시 궤환 캐패시터 C_F에 저장되는 리셋 노이즈:

```
V_kTC_rms = sqrt(kT/C_F)

예시: C_F = 0.5 pF, T = 300 K
V_kTC_rms = sqrt(1.38e-23 × 300 / 0.5e-12) ≈ 2.88 mV rms
```

CDS를 통해 이 노이즈가 두 샘플에 동시에 존재하므로 차분에서 상쇄된다.

**CDS 유효 시간 간격 최적화:**

CDS 클램프~S&H 간격(τ_CDS)을 줄이면 1/f 노이즈가 효과적으로 억제된다. 연구에 따르면 τ_CDS를 최소화할 때 노이즈가 39 e⁻에서 18.3 e⁻으로 감소한다 ([저노이즈 ROIC 연구, 2024](https://www.researching.cn/articles/OJ161cc11982f75edb)).

### 2.3 SHR/SHS 샘플링 충분 조건

```
TFT RC 시상수: τ_TFT = R_on × (C_pixel + C_line)
  - R_on ≈ 3 MΩ (a-Si TFT)
  - C_pixel ≈ 1~3 pF
  - τ_TFT ≈ 3~9 µs

충분한 전하 전송을 위한 조건:
  t_INTG ≥ 5 × τ_TFT (99.3% 전하 전송 확보)
  t_INTG ≥ 5 × 9 µs = 45 µs (최악 조건)

ADAS1256 최소 22µs는 τ_TFT < 4.4µs인 패널에 적합
```

---

## 3. AFE 적분 최적화

### 3.1 SNR vs 적분 시간 트레이드오프

X-ray FPD의 SNR은 적분 시간에 따라 다음과 같이 결정된다:

```
SNR = Q_signal / √(σ_ADC² + σ_dark² + σ_shot²)

여기서:
  σ_ADC  = AFE 노이즈 플로어 (읽기 노이즈)
  σ_dark = √(I_dark × t_INT) (다크 전류 샷 노이즈)
  σ_shot = √Q_signal (광자 샷 노이즈)
```

**SNR 최적화 영역:**

| 적분 시간 | 지배적 노이즈 | SNR 특성 |
|---------|------------|---------|
| 매우 짧음 (< 1ms) | 읽기 노이즈 지배 | SNR ∝ Q_signal (선형) |
| 중간 (1~100 ms) | 샷 노이즈 지배 | SNR ∝ √Q_signal |
| 매우 긺 (> 100ms) | 다크 전류 지배 | SNR 감소 (다크 전류 누적) |

### 3.2 IFS (풀스케일 범위) 선택

**ADAS1256 / AD71124 IFS 설정:**

| IFS 설정 | 풀스케일 범위 | 적용 분야 | 노이즈 (추정) |
|---------|------------|---------|------------|
| IFS0 | ~0.5 pC | 고감도 저선량 | ~300 e⁻ |
| IFS1 | ~1 pC | 일반 진단 방사선 | ~400 e⁻ |
| IFS2 | **2 pC** | 기준 최적값 | **560 e⁻** |
| IFS3 | ~4 pC | 고선량 응용 | ~700 e⁻ |
| IFS4~N | ~8~32 pC | 특수 응용/넓은 다이나믹 레인지 | ~800+ e⁻ |

**AFE2256 IFS 설정 (6단계):**

| 코드 | 풀스케일 | 적용 분야 |
|-----|---------|---------|
| 000 | 0.6 pC | 최소 노이즈, 맘모그래피 |
| 001 | 1.2 pC | **기본 선택 (750 e⁻ 노이즈)** |
| 010 | 2.4 pC | 일반 방사선 |
| 011 | 4.8 pC | 고선량 |
| 100 | 7.2 pC | 형광투시 |
| 101 | 9.6 pC | 최대 범위 |

**IFS 선택 기준:**

```
필요 IFS = Q_max_expected × 1.2 (20% 마진)

Q_max = Q_signal_max + Q_CIC (TFT 게이트 차지 인젝션)

  예시: Q_signal_max = 3 pC, Q_CIC = 0.5 pC
  → IFS ≥ 3.5 × 1.2 = 4.2 pC → IFS4 (4.8 pC) 선택
```

### 3.3 LPF (저역통과 필터) 차단 주파수 최적화

AFE 내부 적분기는 사실상 LPF로 동작한다. 최적 차단 주파수:

```
f_LPF_opt = 1 / (2π × t_INT)

노이즈 대역폭: BW = f_LPF_opt / 2 (1차 필터 가정)

t_INT = 100 µs  → f_LPF_opt = 1.59 kHz
t_INT = 1 ms    → f_LPF_opt = 159 Hz
t_INT = 10 ms   → f_LPF_opt = 15.9 Hz
```

AF0064 측정값 (30 pF 센서):
- 14 µs 적분: 824 e⁻ rms
- 270 µs 적분: 1400 e⁻ rms (다크 전류 누적 영향)
- **→ 짧은 적분으로 AFE 노이즈 자체는 감소하나, 실제 SNR은 입사 방사선량에 따라 결정됨**

---

## 4. TI AFE2256 CIC (Charge Injection Compensation)

### 4.1 TFT 게이트 온 시 차지 인젝션 메커니즘

TFT 게이트 전압이 상승할 때 기생 정전용량을 통해 데이터 라인(ROIC 입력)에 원치 않는 전하가 주입된다.

```
게이트 라인 전압: -5V → +20V (25V 스텝)
TFT 기생 정전용량: C_par ≈ 10~50 fF (전형적 20 fF)
주입 전하량: Q_inj ≈ ΔV_gate × C_par

  Q_inj = 25V × 20fF = 0.5 pC (전형적)

두 가지 컴포넌트 ([특허 US20150256765A1](https://patents.google.com/patent/US20150256765A1/en)):
  1. 데이터 라인 직접 결합 (빠름, 낮은 τ)
     - 경로: 게이트-드레인 오버랩 C → 데이터 라인
     - τ_fast ≈ R_dataline × C_par (수십 ns)
  
  2. 센서 경유 인젝션 (느림, 높은 τ)
     - 경로: 게이트 → TFT → 픽셀 캐패시터 → 데이터 라인
     - τ_slow = R_TFT_on × C_pixel ≈ 3MΩ × 1pF = 3 µs
```

**차지 인젝션의 영향:**

```
ROIC 입력 전압 오프셋:
  ΔV_offset = Q_inj / C_F (궤환 캐패시터)
  
  Q_inj = 0.5 pC, C_F = 0.5 pF → ΔV_offset = 1V
  
이는 ROIC 출력을 비선형 영역 또는 클리핑 영역으로 밀어낼 수 있음
비선형성 임계값: 0.5V
클리핑 임계값: 전원전압 레일
```

### 4.2 AFE2256 CIC 알고리즘

AFE2256은 내장 CIC를 통해 TFT 차지 인젝션을 소프트웨어적으로 보상한다.

**CIC 동작 순서:**

```
1. 다크 프레임 캡처 (X선 미조사 상태)
   → ROIC 출력 = Q_dark + Q_CIC + Q_offset

2. Q_CIC 측정 및 프로파일 계산
   - 패널 전체에 걸친 마스크 스텝-앤-리피트 정렬 오차로
     인해 컬럼별 Q_CIC 값 다름
   - CIC_PROFILE 레지스터에 저장

3. 적분 주기 시작 시 반대 극성 전하 주입
   - 타이밍: 게이트 라인 온 신호와 동기
   - 크기: CIC_PROFILE에 저장된 보상값 적용
   - RC 시상수: τ_comp ≈ τ_TFT 매칭 (느린 컴포넌트)
     + 추가 τ_fast (빠른 데이터 라인 컴포넌트)

4. 결과: 사용 가능 다이나믹 레인지 개선
   - 보상 전: ~50% 사용 가능
   - 보상 후: ~92% 사용 가능
```

**CIC 보상 유형:**

| 방법 | 설명 | 장단점 |
|------|------|--------|
| 고정 CIC | ROIC 입력에 반대 전하 단일 주입 | 빠른 구현, 느린 τ 성분 미보상 |
| 가변 CIC | 전압/캐패시턴스 조정, 다중 τ | 완전 보상, 설정 복잡 |
| 저이득 모드 | ROIC 이득 낮춤 | 간단하나 노이즈 증가 |
| CSA 리셋 오프셋 | 리셋 시 C_FB에 오프셋 전하 설정 | 양의 인젝션 마진 확장 |

**CIC 레지스터 접근 (AFE2256):**

```
SPI를 통한 CIC_PROFILE 레지스터 프로그래밍:
- 레지스터 폭: 채널당 n비트 (패널별 차이값)
- 자동 적용: 각 라인 스캔 시 자동 인젝션
- 업데이트: 온도 변화 또는 패널 교체 시 재캘리브레이션 필요

파워업 시 초기화:
1. 다크 프레임 N회 (보통 4~8 프레임) 캡처
2. 채널별 CIC 오프셋 평균 계산
3. CIC_PROFILE 레지스터 프로그래밍
4. 정상 운영 모드 전환
```

### 4.3 CIC 캘리브레이션 절차 (FPGA 구현)

```verilog
// Pseudocode: CIC Calibration Flow
task cic_calibration;
  integer frame, ch;
  reg [31:0] sum[255:0];
  reg [15:0] dark_frame[255:0][N_FRAMES-1:0];
  
  // 1. X선 셔터 닫기, 다크 프레임 캡처
  x_ray_enable = 0;
  for (frame = 0; frame < N_DARK_FRAMES; frame++) begin
    trigger_readout();
    capture_frame(dark_frame[frame]);
  end
  
  // 2. 채널별 평균 계산
  for (ch = 0; ch < 256; ch++) begin
    sum[ch] = 0;
    for (frame = 0; frame < N_DARK_FRAMES; frame++)
      sum[ch] += dark_frame[frame][ch];
    cic_offset[ch] = sum[ch] / N_DARK_FRAMES;
  end
  
  // 3. SPI를 통해 CIC_PROFILE 레지스터 프로그래밍
  for (ch = 0; ch < 256; ch++) begin
    spi_write(AFE_CIC_REG + ch, cic_offset[ch][7:0]); // 8비트 예시
  end
endtask
```

---

## 5. 멀티칩 AFE 동기화

### 5.1 12-칩 어레이 구성 (ADAS1256 기반)

대형 FPD(예: 43 cm × 43 cm)는 12개의 ADAS1256/AD71124를 어레이로 구성한다.

```
패널 구성 (예: 3072채널 = 12 × 256ch):

  [AFE_0] [AFE_1] [AFE_2] [AFE_3] [AFE_4] [AFE_5]
  [AFE_6] [AFE_7] [AFE_8] [AFE_9] [AFE_10][AFE_11]
                         ↑
                   TFT 패널 하단 기준
```

### 5.2 SPI 데이지체인 구성

```
FPGA ──SDI──→ [AFE_0] SDO──→ [AFE_1] SDO──→ ... SDO──→ [AFE_11]
                  ↑ SDI              ↑ SDI              ↑ SDI
              (데이지체인)

공통 신호 (브로드캐스트):
- SCK: 모든 AFE에 동일한 SPI 클록
- CS: 모든 AFE에 동일한 칩 셀렉트 (동시 설정 시)
- ACLK: 모든 AFE에 동일한 마스터 클록 (타이밍 소스)
- SYNC: 동기화 신호 (모든 AFE에 브로드캐스트)
- RESET: 공통 리셋

개별 신호:
- DOUT: 각 AFE마다 독립 LVDS 데이터 출력
- DCLK: 각 AFE 데이터 클록 (자기 동기식)
```

**SPI 데이지체인 예시 (FPGA 코드 개념):**

```verilog
// 12칩 SPI 데이지체인 레지스터 쓰기
task spi_write_all_chips(input [7:0] reg_addr, 
                          input [15:0] reg_data);
  integer i;
  // CS 어시트 → 12 × (8+16) = 288 클록 전송
  // 각 AFE는 마지막 24비트 패킷을 자신의 레지스터에 래치
  spi_cs_n = 0;
  for (i = 0; i < 12; i++) begin
    spi_shift_out(8'h00);    // dummy (shift through chain)
    spi_shift_out(reg_addr);
    spi_shift_out(reg_data[15:8]);
    spi_shift_out(reg_data[7:0]);
  end
  spi_cs_n = 1;
endtask
```

### 5.3 SYNC 브로드캐스트 타이밍

SYNC 핀은 모든 AFE가 동시에 적분 시작하도록 동기화한다.

```
게이트 드라이버 VT 신호 (TFT 게이트 온)
        │
        ▼
FPGA 검출 → SYNC_PULSE 생성 → 모든 AFE 브로드캐스트

타이밍 요구사항:
  - SYNC 펄스 최소 폭: ≥ 1 ACLK 주기
  - SYNC → 첫 번째 적분 시작: AFE 내부 시퀀서 처리 후
  - 모든 AFE 간 SYNC 수신 스큐: < 5 ns (PCB 매칭 필요)
```

**SYNC 신호 위상 매칭:**

```
12개 AFE에 대한 SYNC 라우팅 등장 조건:
  - PCB 트레이스 길이 매칭: ±5 mm 이내 (전파 지연 ~33 ps/mm)
  - 팬아웃 버퍼 (ADN4670 등) 사용 시 스큐 < 30 ps
  
ADN4670 (ADI 저스큐 LVDS 팬아웃):
  - 1:10 LVDS 드라이버
  - 출력 스큐: < 30 ps (전형적)
  - ACLK 팬아웃에도 활용 가능
```

### 5.4 ACLK 분배 (ADAS1256)

```
ACLK 소스: FPGA PLL 출력 (예: 40 MHz = 25 ns 주기)
                │
         ADN4670 (1:12 팬아웃 또는 계단식)
        ┌───┬───┬───┬───┬───┬───┐
       AFE0 AFE1 AFE2 ... AFE11
       
ACLK 주파수 계산:
  tLINE = 22 µs (최소)
  ACLK_freq = (N_ADC_CYCLES × N_CHANNELS) / tLINE
  
  ADAS1256: N_ADC_CYCLES 및 채널 당 클록은 내부 시퀀서 설정에 의존
  일반적: ACLK = 40 MHz 사용 시 1클록 = 25 ns
           22 µs / 25 ns = 880 클록 사용 가능
```

---

## 6. LVDS 인터페이스 타이밍

### 6.1 ADAS1256 / AD71124 LVDS 출력

**신호 구성:**

| 신호 | 방향 | 설명 |
|------|------|------|
| DCLK | AFE → FPGA | 자기동기식 데이터 클록 (LVDS) |
| DOUTA | AFE → FPGA | 채널 0~127 직렬 데이터 (LVDS) |
| DOUTB | AFE → FPGA | 채널 128~255 직렬 데이터 (LVDS) |

**데이터 프레임 구조 (전형적):**

```
각 채널 = 16비트 ADC 결과
프레임 = 256채널 × 16비트 = 4096비트

직렬 전송:
  MSB 먼저 → CH0[15]...CH0[0] CH1[15]...CH255[0]

DCLK: 데이터와 엣지 정렬 (소스 동기)
  - 상승/하강 엣지 정의: 데이터시트 확인 필요
  - 전형적 DCLK 주파수: 40~80 MHz (LVDS 레벨)
```

### 6.2 AFE2256 LVDS 출력

**AFE2256 LVDS 신호:**

| 신호 | 설명 |
|------|------|
| DCLK_P/M | 데이터 클록 (LVDS 차동) |
| DOUT_P/M | 직렬 데이터 (LVDS 차동) |
| FCLK_P/M | 프레임 클록 (LVDS 차동) |

```
MCLK → 내부 TG → ADC 변환 → LVDS 직렬화
  DCLK = MCLK 기반 (내부 분주)
  FCLK = 프레임 경계 표시 (동기화 용도)

AFE2256 직렬화 구조:
  256채널 → 4개 ADC (각 64채널)
  → 4개 LVDS DOUT (병렬)
  
데이터 속도: scan_time = 20µs, 256×16bit = 4096bit
  최소 LVDS 속도: 4096bit / 20µs = 204.8 Mbps / DOUT_pair
```

### 6.3 FPGA LVDS 수신 설계 (Xilinx 7-Series)

**기본 수신 체인:**

```
AFE                           FPGA
────────────────────────────────────────
DCLK_P ──────────────── IBUFDS → BUFG
DCLK_M ──────┘
                                   │
                              MMCM/PLL
                                   │ 고속 클록 (DCLK × N)
                                   │ 저속 클록 (DCLK)
                                   │
DOUT_P ──── IBUFDS → IDELAYE2 → ISERDESE2 → 병렬 데이터
DOUT_M ────┘         (지연 보정)  (역직렬화)
```

**ISERDESE2 구성 ([AMD/Xilinx UG953](https://docs.amd.com/r/en-US/ug953-vivado-7series-libraries/ISERDESE2)):**

```verilog
ISERDESE2 #(
  .DATA_RATE      ("DDR"),      // DDR or SDR
  .DATA_WIDTH     (8),          // 역직렬화 폭 (8비트 예시)
  .INTERFACE_TYPE ("NETWORKING"), // 네트워킹 모드
  .IOBDELAY       ("IFD"),      // IDELAY 사용
  .SERDES_MODE    ("MASTER")
) iserdes_inst (
  .CLK     (fast_clk),   // 고속 클록 (예: 400MHz)
  .CLKB    (~fast_clk),  // 반전 고속 클록
  .CLKDIV  (slow_clk),   // 분주 클록 (예: 50MHz)
  .D       (),
  .DDLY    (data_delayed), // IDELAYE2로부터
  .RST     (rst),
  .BITSLIP (bitslip_pulse),
  .Q1      (q[7]), .Q2 (q[6]), ... .Q8 (q[0])
);
```

**비트슬립 정렬 절차:**

```
목표: 병렬 출력이 올바른 16비트 워드 경계와 정렬되도록

1단계: 알려진 패턴 전송
  - AFE 리셋 또는 테스트 모드에서 고정 패턴 출력
  - 예: 0xAAAA (1010...1010) 패턴

2단계: BITSLIP 반복 적용
  for (i = 0; i < DATA_WIDTH; i++) {
    if (current_pattern == expected_pattern) break;
    assert BITSLIP for 1 CLKDIV cycle;
    wait 2 CLKDIV cycles;  // BITSLIP 정착 시간
  }

3단계: 정렬 잠금 확인
  - 16비트 체크섬 또는 헤더 패턴 검증
  - 정렬 유지 시 잠금 상태 유지

주의: BITSLIP은 모든 ISERDESE2에 동시 적용 (채널 간 동기)
```

**IDELAYE2를 이용한 아이 중앙 포착:**

```
아이 다이어그램 중심 설정:
  - IDELAYE2 탭 = 32 (중심, 0~31 범위)
  - 각 탭 = 78 ps (Artix-7 기준)
  - 32 탭 = 2.5 ns (반 UI 기준점)

자동 탭 스캔:
  for (tap = 0; tap < 32; tap++) {
    set_idelay_tap(tap);
    capture_N_samples();
    if (bit_error_rate[tap] == 0) valid_tap_window[tap] = 1;
  }
  optimal_tap = center_of_valid_window();
```

### 6.4 12-칩 LVDS 팬인 설계

```
12개 AFE × 2 DOUT_pair (A/B) = 24 LVDS 쌍
+ 12 DCLK + 12 FCLK = 48 LVDS 쌍
총 LVDS 쌍: 72쌍

FPGA I/O 요구사항:
  - HP(High Performance) I/O 뱅크 권장
  - LVDS 수신 가능 핀 (VCCIO = 1.8V 또는 2.5V)
  - Xilinx Ultrascale+ 또는 Virtex UltraScale 추천

DCLK 처리:
  각 AFE의 DCLK는 독립적으로 BUFG + PLL로 처리
  또는 글로벌 ACLK 기반 재동기화 권장 (PPM 보상)
```

---

## 7. AFE 파워업 시퀀스

### 7.1 ADAS1256 / AD71124 파워업 시퀀스

```
단계 1: 전원 인가 순서
  ① AVDD5B (아날로그 바이어스 5V)
  ② AVDD5F (프런트엔드 아날로그 5V)  
  ③ AVDDI  (ADC 아날로그 전원)
  ④ DVDD5  (디지털 5V)
  ⑤ DVDD   (디지털 코어)
  ⑥ IOVDD  (I/O 전압)
  
  각 전원 안정화: ≥ 10 ms 대기

단계 2: RESET 어시트
  - RESET 핀 Low → High (최소 1 ms 유지)
  - RESET 해제 후 ≥ 1 ms 대기

단계 3: ACLK 인가
  - 안정적인 ACLK 클록 시작
  - 클록 안정화: ≥ 100 µs

단계 4: SPI 초기화
  - SCK 비활성화 상태에서 CS = High 확인
  - 레지스터 기본값 확인 (읽기 후 검증)
  - 설정 레지스터 순차 프로그래밍:
    ① IFS (충전 범위 설정)
    ② 파워 모드 설정
    ③ CDS 파라미터
    ④ 출력 포맷 설정

단계 5: 첫 번째 유효 프레임 대기
  - SYNC 브로드캐스트 → 첫 번째 DOUT 유효
  - 대기: ≥ 2 라인 주기 (더미 프레임)
  
단계 6: 오프셋 캘리브레이션
  - 다크 프레임 캡처 (X선 미조사)
  - 채널별 오프셋 맵 생성
  - 이상값 채널 식별 및 플래그
```

### 7.2 AFE2256 파워업 시퀀스

```
전원 시퀀스:
  ① AVDD2 = 3.3 V (아날로그)
  ② AVDD1 = 1.85 V (ADC 코어)
  ③ 디지털 I/O 전원
  
  AVDD1이 AVDD2보다 늦게 인가되는 것을 권장
  (역전압 방지)

SPI 초기화 순서:
  1. MCLK 인가 (AFE2256 내부 TG 소스)
  2. SEN = High (SPI 비활성화 상태 확인)
  3. 소프트 리셋: SPI 리셋 레지스터 쓰기
  4. 파워다운 모드 해제
  5. IFS 설정 (충전 범위 선택)
  6. CIC_PROFILE 프로그래밍 (사전 캘리브레이션 값)
  7. 파이프라인 모드 설정 (PIPELINE_EN)
  8. SYNC 핀 활성화
  9. 첫 번째 유효 데이터 대기

NAP 모드에서 복귀:
  - NAP 모드 해제 명령 → 최소 10 µs 대기 (AFE0064 기준)
  - LVDS 출력 재안정화: ≥ 2 프레임 대기
```

### 7.3 SPI 초기화 레지스터 설정 순서 (AFE2256 예시)

```
// AFE2256 SPI 초기화 의사코드
void afe2256_init(void) {
    // 1. 소프트 리셋
    spi_write(REG_SOFT_RESET, 0x01);
    delay_us(100);
    
    // 2. 클록 모드 설정
    spi_write(REG_CLOCK_CONFIG, MCLK_DIV_4); // MCLK/4 내부 사용
    
    // 3. 충전 범위 (IFS) 설정
    spi_write(REG_IFS_SELECT, IFS_1P2PC); // 1.2 pC 범위
    
    // 4. CDS 설정
    spi_write(REG_CDS_CONFIG, CDS_DUAL_BANK | CDS_NORMAL);
    
    // 5. CIC 활성화 및 프로파일 로드
    spi_write(REG_CIC_ENABLE, 0x01);
    for (int ch = 0; ch < 256; ch++) {
        spi_write(REG_CIC_PROFILE + ch, cic_cal_data[ch]);
    }
    
    // 6. 파이프라인 모드 설정
    spi_write(REG_PIPELINE_EN, PIPELINE_ENABLE);
    
    // 7. LVDS 출력 설정
    spi_write(REG_OUTPUT_FORMAT, LVDS_DDR_MODE);
    
    // 8. 파워다운 해제
    spi_write(REG_POWER_MODE, NORMAL_OPERATION);
    
    // 9. 안정화 대기
    delay_us(500);
}
```

---

## 8. 파이프라인 리드아웃 모드

### 8.1 Integrate-While-Read (IWR) 원리

파이프라인 모드에서는 이전 라인의 데이터 읽기와 현재 라인의 적분을 동시에 수행한다.

```
비파이프라인 (ITR, Integrate-Then-Read):
┌────────────┬──────────────────────┐
│ Integrate  │       Read out       │
└────────────┴──────────────────────┘
  tINT            tREAD
  
  tLINE = tINT + tREAD (두 작업 직렬화)

파이프라인 (IWR, Integrate-While-Read):
┌────────────┬────────────┬──────────┐
│ Integrate  │ Integrate  │         │
│   n-1      │    n       │  n+1    │
├────────────┼────────────┼──────────┤
│            │  Read n-1  │  Read n  │
└────────────┴────────────┴──────────┘

  tLINE = max(tINT, tREAD) (두 작업 병렬화)
```

**처리율 개선 효과:**

```
예시 조건: tINT = 30 µs, tREAD = 20 µs

ITR 방식: tLINE = 30 + 20 = 50 µs
  → 초당 라인: 20,000 라인/초

IWR 방식: tLINE = max(30, 20) = 30 µs
  → 초당 라인: 33,333 라인/초

처리율 개선: 33.3% (조건에 따라 최대 50%+ 가능)
```

### 8.2 AFE2256 파이프라인 구현

```
PIPELINE_EN = 1 설정 시:
  - 뱅크 A: 채널 0~127 → 적분
  - 뱅크 B: 채널 128~255 → 이전 결과 직렬 출력
  - 다음 사이클에서 A↔B 역할 교환

주의사항:
  1. 파이프라인 모드에서는 CDS 타이밍이 변경됨
  2. 첫 번째 프레임(프라임 프레임)은 유효하지 않음
  3. FPGA에서 1 프레임 지연 보상 필요
  4. SYNC 신호가 파이프라인 경계와 동기화되어야 함
```

**FPGA 파이프라인 구현 주의사항:**

```verilog
// 파이프라인 모드: FPGA 1프레임 지연 보상
module pipeline_compensator #(
  parameter CHANNELS = 256,
  parameter DATA_W = 16
)(
  input  clk,
  input  frame_valid,
  input  [DATA_W-1:0] raw_data [CHANNELS-1:0],
  output [DATA_W-1:0] valid_data [CHANNELS-1:0],
  output valid_flag
);
  reg frame_count;
  
  always @(posedge clk) begin
    if (frame_valid) begin
      frame_count <= frame_count + 1;
      if (frame_count >= 1) begin  // 첫 프레임 버림
        valid_data <= raw_data;
        valid_flag <= 1;
      end
    end
  end
endmodule
```

---

## 9. 노이즈 최적화

### 9.1 X-ray FPD AFE 노이즈 소스 분류

```
총 노이즈 (ENC, 등가 잡음 전하):

  ENC_total² = ENC_kTC² + ENC_1f² + ENC_thermal² + 
               ENC_ADC² + ENC_dark² + ENC_shot²

CDS로 제거 가능:    kTC 노이즈, 1/f 노이즈
CDS로 제거 불가능:  열 노이즈, ADC 양자화 노이즈, 다크 전류 샷 노이즈, 방사선 샷 노이즈
```

**노이즈 소스별 특성:**

| 노이즈 소스 | 발생 메커니즘 | CDS 효과 | 저감 방법 |
|-----------|------------|---------|---------|
| kTC 리셋 노이즈 | C_F 리셋 시 열요동 | ✅ 완전 제거 | CDS 적용 |
| 1/f 노이즈 | MOS 게이트 트랩 | ✅ 부분 제거 (τ_CDS에 의존) | τ_CDS 최소화 |
| 열 노이즈 (opamp) | 입력단 MOSFET | ❌ 제거 불가 | C_F 최적화 |
| ADC 양자화 | 비트 수 제한 | ❌ | 16-bit ADC 사용 |
| 다크 전류 샷 | TFT 누설 × 적분 시간 | ❌ (오프셋 제거 가능) | 짧은 적분 시간 |
| 데이터 라인 열 노이즈 | 라인 저항 × 라인 캐패시턴스 | ❌ | 낮은 저항 금속층 사용 |

### 9.2 입력 등가 노이즈 vs. 적분 시간

AFE0064 측정 데이터 기준 (유사 디바이스 참조값):

| 적분 시간 | C_sensor | 노이즈 (e⁻ rms) | 노이즈 소스 |
|---------|---------|----------------|---------|
| 14 µs | 30 pF | 824 e⁻ | 열 노이즈 지배 |
| 14 µs | 20 pF | 600 e⁻ | 열 노이즈 지배 |
| 270 µs | 30 pF | 1400 e⁻ | 다크 전류 누적 |

**ADAS1256 규격:**
- 560 e⁻ @ 2 pC 범위, 22 µs 적분

**AFE2256 규격:**
- 750 e⁻ @ 1.2 pC 범위, 240 e⁻ 내장 규격

### 9.3 ADC 노이즈 플로어

```
16-bit ADC의 이론적 노이즈 플로어:
  LSB = V_ref / 2^16 (전압 단위)
  
  충전 도메인:
  1 LSB_charge = IFS / 2^16
  
  예시: IFS = 2 pC, 16비트
  1 LSB = 2 pC / 65536 = 30.5 fC = ~190 e⁻ (1e⁻ = 0.16 fC)
  
  AFE2256 INL: ±2 LSB = ±61 fC = ±380 e⁻
  SNR 한계: 20 × log10(65536/1) ≈ 96 dB
```

### 9.4 LVDS 커플링 노이즈 저감

```
LVDS 노이즈 커플링 메커니즘:
  - LVDS 스위칭 노이즈 → PCB 전원 평면 오염
  - 디지털-아날로그 그라운드 공유 시 간섭

저감 방법:
  1. 디지털/아날로그 전원 분리 (별도 LDO)
  2. LVDS PCB 라인: 100Ω 차동 임피던스 매칭
  3. 수신단 100Ω 차동 종단 (FPGA 내부 ODT 또는 외부)
  4. LVDS 라인 → 아날로그 입력 라인 간 거리 ≥ 3mm
  5. 충분한 디커플링 캐패시터: 0.1 µF + 10 µF (각 전원핀)
  6. LVDS 클록 → 데이터 위상 관계 확인 (아이 다이어그램)
```

### 9.5 아날로그 입력 임피던스 최적화

```
최적 노이즈를 위한 입력 임피던스 조건:
  C_G (입력 트랜지스터 게이트) = C_DET (검출기 캐패시턴스)
  
  강한 반전 영역 (strong inversion):
    ENC ∝ C_DET^(1/2) 이면 C_G = C_DET 최적
  
  약한 반전 영역 (weak inversion):
    ENC ∝ C_DET 이면 C_G << C_DET 최적
  
  X-ray FPD 조건: C_DET = 30~200 pF (라인 캐패시턴스)
  → 강한 반전 조건이 일반적
  → AFE 내 적분기 설계 시 C_F 선택이 중요
```

---

## 10. 채널 간 크로스토크

### 10.1 크로스토크 메커니즘

**AFE0064 측정값:**
- 인접 채널 (어그레서 풀스케일) → 0.08% FSR 크로스토크

```
크로스토크 발생 경로:
  1. 정전기 결합 (capacitive): 인접 라인 간 C_cross
     ΔV_victim = ΔV_aggressor × C_cross / (C_cross + C_in)
     
  2. 공통 전원 임피던스: I_aggressor × Z_supply → V_victim
  
  3. 그라운드 바운스: 동시 스위칭 시 그라운드 전위 변동
  
  4. LVDS 직렬화 크로스토크: 인접 LVDS 라인 간
```

### 10.2 동시 리드아웃 vs. 순차 리드아웃

| 방식 | 크로스토크 | 처리율 | 주요 디바이스 |
|------|----------|------|------------|
| 동시 샘플링 | 중간 (전원 임피던스 크로스토크) | 높음 | ADAS1256 (simultaneous sampling) |
| 순차 리드아웃 | 낮음 (한 채널씩) | 낮음 | 구형 AFE |
| 그룹 동시 + 순차 MUX | 낮음 + 높은 속도 | 높음 | AFE2256 (256:4 MUX) |

**AFE2256 256:4 MUX 구조:**

```
256채널 → 4개 그룹 (각 64채널)
         ↓
         4개 독립 SAR ADC (병렬 변환)
         ↓
         4쌍 LVDS 직렬 출력

→ 동시 샘플링 + 그룹별 순차 변환
→ 총 크로스토크 = (그룹 내 동시 샘플 크로스토크) 최소화
```

### 10.3 PCB 격리 설계

```
채널 간 격리 방법:
  1. 차동 입력 라인 (TFT 데이터 라인 → AFE 입력)
     - 인접 라인 사이 그라운드 가드 (shield trace)
     
  2. 입력 라인 간격: 최소 3배 라인 폭 이상
  
  3. AFE IC 내부 가드링: AVSS 또는 AGND 채널 간 삽입
  
  4. 전원 디커플링: 채널 그룹별 독립 디커플링
```

---

## 11. 온도 영향 및 다크 커런트

### 11.1 a-Si TFT 다크 전류 온도 의존성

```
TFT 누설 전류 온도 특성:
  I_leak(T) = I_0 × exp(-E_a / kT)
  
  활성화 에너지: E_a ≈ 0.5~0.8 eV (a-Si TFT)
  
온도 배증 규칙:
  ΔT = 8~10°C 당 다크 전류 2배 (경험칙)
  
  예시:
  T = 25°C → I_dark = 1 pA
  T = 35°C → I_dark ≈ 2 pA
  T = 45°C → I_dark ≈ 4 pA
  T = 55°C → I_dark ≈ 8 pA
```

**ADI 측정 데이터 (a-Si TFT, W/L = 100/50µm):**

| 온도 | 드레인 전류 변화 | 비고 |
|------|--------------|------|
| 25°C | 기준값 | V_GS = -5V (오프 상태) |
| 35°C | ~1.5× | 드레인 전류 증가 |
| 50°C | ~3× | 이미지 품질 저하 시작 |
| 65°C | ~6× | FPD 최대 사용 온도 접근 |
| 80°C | ~12× | 정상 운영 불가 |

### 11.2 이득 드리프트 보상

**AFE 이득 온도 계수:**

```
충전 범위 기준 이득 드리프트 (전형적):
  ΔG/G ≈ ±0.1% (0~70°C 전 범위)
  
  ADC INL: ±2.5 LSB (AFE2256)
  온도 드리프트 기여: ±0.5 LSB / 10°C (추정)

보상 방법:
  1. 주기적 오프셋 캘리브레이션
     - 작동 중 X선 미조사 구간에서 다크 프레임 캡처
     - 채널별 오프셋 테이블 업데이트
     - 주기: 온도 변화 ±3°C 마다 또는 1분 간격
  
  2. 온도 센서 기반 룩업 테이블
     - AFE 근처 NTC 또는 디지털 온도 센서 배치
     - 온도별 보정 계수 FPGA 내 LUT에 저장
     
  3. 레퍼런스 픽셀 보정 (다크 픽셀 방식)
     - 패널 외부 어두운 레퍼런스 픽셀 유지
     - 실시간 오프셋 추출 및 감산
```

### 11.3 다크 전류 인 적분 중 영향

```
적분 시간 t_INT 동안 누적 다크 전하:
  Q_dark = I_dark(T) × t_INT × N_pixels_per_channel

  I_dark = 1 pA @ 25°C, t_INT = 100 ms:
  Q_dark = 1e-12 × 0.1 = 100 fC (1 pC 범위의 10%)
  
  이는 IFS 선택에 직접 영향:
  유효 신호 범위 = IFS - Q_dark_max
  
  고온 운영 시 IFS를 한 단계 높여야 할 수 있음
  또는 적분 시간 단축 필요
```

---

## 12. FPGA 설계 함의 요약

### 12.1 타이밍 제어 FSM 설계

```verilog
// X-ray FPD AFE 제어 상태 기계 (상위 레벨 개념)
typedef enum {
  S_IDLE,
  S_POWERUP,
  S_SPI_INIT,
  S_CIC_CALIBRATE,
  S_WAIT_XRAY,
  S_SYNC_PULSE,
  S_INTEGRATE,   // INTG pulse (TFT on)
  S_CDS_SHR,     // SHR sampling
  S_INTEGRATE2,  // Signal integration
  S_CDS_SHS,     // SHS sampling
  S_ADC_CONVERT,
  S_LVDS_READ,   // 데이터 수신
  S_FRAME_DONE,
  S_ERROR
} afe_state_t;
```

### 12.2 타이밍 카운터 요약표

| 이벤트 | 기준 | 최소 지연 | 비고 |
|--------|------|---------|------|
| 전원 안정 → RESET 어시트 | 전원 인가 | 10 ms | 모든 전원 레일 안정 후 |
| RESET 폭 | 파워업 | 1 ms | 내부 초기화 완료 |
| RESET 해제 → ACLK 인가 | RESET 해제 | 100 µs | 클록 도메인 안정화 |
| ACLK 안정 → SPI 시작 | ACLK 시작 | 10 클록 | 내부 POR 완료 |
| SPI CS 하강 → 첫 클록 | CS | 30 ns | 셋업 타임 |
| 마지막 SPI 클록 → CS 상승 | 마지막 클록 | 30 ns | 홀드 타임 |
| SPI 완료 → SYNC 브로드캐스트 | SPI | 10 µs | 레지스터 반영 |
| INTG 하강 → SHS 상승 (t6) | INTG 하강 | 4.5 µs | 전하 전송 완료 |
| SHR 셋업 → INTG 상승 (t4) | SHR | 30 ns | CDS 시퀀스 |
| 전체 라인 시간 | 이전 라인 | 22 µs (ADAS1256) | tLINE |

### 12.3 12-칩 어레이 FPGA 리소스 추정

| 리소스 | 수량 | 설명 |
|--------|------|------|
| LVDS RX 쌍 | 72쌍 | 12칩 × (2 DOUT + 1 DCLK + 1 FCLK) × 1.5 |
| ISERDESE2 | 72개 | 각 LVDS 수신 |
| IDELAYE2 | 72개 | 아이 중앙 정렬 |
| MMCM/PLL | 3~6개 | DCLK → 내부 클록 생성 |
| 블록 RAM | 12~24 블록 | 프레임 버퍼 (256채널×16비트×12칩) |
| SPI 제어기 | 1개 | 데이지체인 SPI 마스터 |
| FSM 로직 | ~1000 LUT | AFE 시퀀서 |
| CIC 보정 | ~500 LUT | 오프셋 감산 |

### 12.4 타이밍 다이어그램 (주요 신호)

```
ACLK  ─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─┬─... (예: 40 MHz)

SYNC  ──┐  (1 ACLK 폭)
         └────────────────────────────

VT    ──────────────────────────────────┐ (TFT 게이트 온)
(게이트)                                 └──

INTG  ────────────────────────────────┐  │
                                       └──┘  ← tINT

SHR   ────────────────────────────┐
                                   └─

SHS   ──────────────────────────────────────┐
                                             └─

DCLK  ────────────────────────────────────────┬┬┬┬┬┬...
DOUT  ────────────────────────────────────────XXXX데이터X...
FCLK  ────────────────────────────────────────┐   (1 프레임 펄스)
                                               └─
```

### 12.5 권장 AFE 운영 절차

```
1. 시스템 초기화
   ├── 전원 시퀀스 준수
   ├── RESET 어시트/해제
   ├── ACLK 안정화
   └── SPI 설정 로드

2. 캘리브레이션
   ├── 오프셋 캘리브레이션 (다크 프레임 × 8)
   ├── CIC 프로파일 계산 (AFE2256)
   ├── 이득 캘리브레이션 (플랫 필드 × 16)
   └── 배드 픽셀 맵 생성

3. 정상 촬영
   ├── X선 트리거 → SYNC 브로드캐스트
   ├── 피드 라인 스캔 (tLINE 주기)
   ├── LVDS 데이터 수신 및 역직렬화
   ├── 오프셋 감산 (캘리브레이션 맵 적용)
   └── 이득 정규화 → 이미지 메모리 저장

4. 주기적 유지보수
   ├── 온도 변화 ±3°C 시 오프셋 재캘리브레이션
   ├── X선 세션 간 다크 프레임 갱신
   └── CIC 프로파일 월간 재설정
```

---

## 참조 문헌 및 데이터 소스

1. [TI AFE2256 데이터시트](https://www.ti.com/product/AFE2256) — 256채널 X-ray FPD AFE, 스캔 타임, LVDS, SAR ADC, CIC 기능
2. [TI AFE0064 데이터시트 (유사 구조)](https://www.ti.com/lit/gpn/AFE0064) — CDS 타이밍 파라미터, SHR/SHS/INTG 시퀀스
3. [Analog Devices ADAS1256 제품 페이지](https://www.analog.com/en/products/adas1256.html) — ADAS1256 (AD71124급): 22µs 라인타임, 560 e⁻ 노이즈, ACLK, SDO 데이지체인
4. [ADI ADAS1256 기술 자료 (iczhiku 미러)](https://picture.iczhiku.com/resource/eetop/WHkgHhSrzSgfpxmb.pdf) — 상세 블록 다이어그램, SPI 구성
5. [ADI 의료 X선 이미징 솔루션](https://www.analog.com/media/cn/technical-documentation/apm-pdf/adi-medical-x-ray-imaging-solutions_en.pdf) — ADAS1256 DR 신호 체인, ADN4670 LVDS 팬아웃
6. [TI AFE2256EVM 사용자 가이드](https://www.ti.com/lit/pdf/sbau253) — EVM 구성, FPGA 인터페이스
7. [특허 US20150256765A1 — TFT 차지 인젝션 보상](https://patents.google.com/patent/US20150256765A1/en) — CIC 알고리즘, RC 시상수, 타이밍 분석
8. [AMD/Xilinx UG953 — ISERDESE2](https://docs.amd.com/r/en-US/ug953-vivado-7series-libraries/ISERDESE2) — 역직렬화, BITSLIP, DDR 타이밍
9. [저노이즈 ROIC CDS 연구 (2024)](https://www.researching.cn/articles/OJ161cc11982f75edb) — AICDS τ 조정 노이즈 18.3 e⁻ 달성
10. [OSTI 전면 전자 이미징 검출기 자료](https://www.osti.gov/servlets/purl/767295) — CDS 원리, kTC 노이즈, CTIA 설계
11. [ams AS5852B 데이터시트](https://ams-osram.com/products/sensor-solutions/x-ray-sensors/ams-as5852b-digital-x-ray-flat-panel-readout-ic) — 경쟁 디바이스: 라인타임 80µs, CIC 3주기, LPF 선택
12. [RPI a-Si TFT 열 특성 논문](https://dspace.rpi.edu/bitstreams/f9359065-9d50-4fad-9cba-67d81b6b276e/download) — 온도 vs. 다크 전류 특성

---

*연구 완료: 2026-03-18 | 작성: AI Research Agent*
