# FPGA 구동 알고리즘 최종 설계서
## X-ray Flat Panel Detector (FPD) 시스템

**문서 번호**: FPD-FPGA-DRV-001  
**버전**: 1.0  
**작성일**: 2026-03-18  
**분류**: 설계 사양서 (Design Specification)  
**적용 대상**: R1717, R1714, X239AW1-102 패널 기반 X-ray 검출기 시스템  

---

## 목차

1. [개요 (Executive Summary)](#1-개요-executive-summary)
2. [a-Si TFT 패널 물리 특성과 구동 제약 조건](#2-a-si-tft-패널-물리-특성과-구동-제약-조건)
3. [전체 시스템 구동 상태 머신 (System-Level FSM)](#3-전체-시스템-구동-상태-머신-system-level-fsm)
4. [패널 초기화 및 안정화 알고리즘](#4-패널-초기화-및-안정화-알고리즘)
5. [IDLE 상태 구동 알고리즘](#5-idle-상태-구동-알고리즘)
6. [Gate IC 최적 제어 알고리즘](#6-gate-ic-최적-제어-알고리즘)
7. [AFE/ROIC 최적 구동 알고리즘](#7-aferoic-최적-구동-알고리즘)
8. [영상 획득 시퀀스 (Acquisition Sequences)](#8-영상-획득-시퀀스-acquisition-sequences)
9. [이미지 래그(Lag) 보정 알고리즘](#9-이미지-래그lag-보정-알고리즘)
10. [보정 알고리즘 (Calibration Pipeline)](#10-보정-알고리즘-calibration-pipeline)
11. [멀티 AFE 대형 패널 특수 알고리즘 (C6/C7)](#11-멀티-afe-대형-패널-특수-알고리즘-c6c7)
12. [SystemVerilog 파라미터 및 레지스터 맵 설계](#12-systemverilog-파라미터-및-레지스터-맵-설계)
13. [성능 목표 및 검증 계획](#13-성능-목표-및-검증-계획)

---

## 1. 개요 (Executive Summary)

### 1.1 설계 목적

본 문서는 a-Si TFT 기반 X-ray Flat Panel Detector(FPD) 시스템의 FPGA 구동 알고리즘 최종 설계서이다. 3종의 패널, 2종의 Gate IC, 3종의 AFE/ROIC를 조합한 총 7가지 부품 조합(C1–C7)에 대해 통합 구동이 가능한 FPGA 아키텍처를 정의하며, 각 조합별 최적화된 타이밍 파라미터와 구동 시퀀스를 명시한다.

본 설계서의 핵심 목표는 다음과 같다:

1. **물리 기반 타이밍 최적화**: a-Si TFT의 RC 지연, charge trapping, dark current 특성을 정량적으로 반영한 구동 파라미터 도출
2. **하드웨어 추상화**: Gate IC(NV1047/NT39565D)와 AFE(AD71124/AD71143/AFE2256)의 차이를 `gate_ic_driver`/`afe_ctrl_if` 모듈로 캡슐화
3. **래그 보정**: Forward Bias 하드웨어 방법과 LTI/NLCSC 소프트웨어 재귀 필터의 병행 적용
4. **실시간 보정 파이프라인**: 오프셋, 게인, 결함 화소 보정을 FPGA 내 3단계 파이프라인으로 처리
5. **대형 패널 지원**: 3072×3072 패널(X239AW1-102)의 12-chip AFE 동기화 및 30fps 형광투시 구동

### 1.2 시스템 구성

```
┌─────────────────────────────────────────────────────────────────┐
│                    X-ray FPD 시스템 블록도                        │
│                                                                 │
│  ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌───────────┐ │
│  │ X-ray    │    │  Panel   │    │  FPGA    │    │  Host PC  │ │
│  │Generator │───▶│  + Gate  │◀──▶│ (Main    │◀──▶│ Detector  │ │
│  │          │    │  IC +    │    │  Ctrl)   │    │  SDK      │ │
│  └──────────┘    │  AFE     │    │    ▲     │    └───────────┘ │
│                  └──────────┘    │    │     │                   │
│                                  │  ┌─┴──┐  │                   │
│                                  │  │MCU │  │                   │
│                                  │  │(SPI)│  │                   │
│                                  │  └────┘  │                   │
│                                  └──────────┘                   │
└─────────────────────────────────────────────────────────────────┘
```

### 1.3 부품 조합 매트릭스 (C1–C7)

| 조합ID | 패널 | Gate IC | AFE/ROIC | 용도 |
|--------|------|---------|----------|------|
| **C1** | R1717 (17×17") | NV1047 | AD71124 | 표준 정지상 (17×17) |
| **C2** | R1717 (17×17") | NV1047 | AD71143 | 저전력 17×17 (배터리/모바일) |
| **C3** | R1717 (17×17") | NV1047 | AFE2256 | 고화질 17×17 (저노이즈, CIC) |
| **C4** | R1714 (17×14") | NV1047 | AD71124 | 비정방형 17×14 |
| **C5** | R1714 (17×14") | NV1047 | AFE2256 | 고화질 17×14 |
| **C6** | X239AW1-102 (43×43cm) | NT39565D ×6 | AD71124 ×12 | 대형 43×43 (다중 AFE) |
| **C7** | X239AW1-102 (43×43cm) | NT39565D ×6 | AFE2256 ×12 | 대형 43×43 고화질 |

> **설계 원칙**: 모든 타이밍 파라미터는 `reg_bank`에서 런타임 설정 가능하며, 하드코딩을 금지한다. 조합별 차이는 Gate IC 드라이버와 AFE 제어 인터페이스 모듈의 선택으로만 구분한다.

### 1.4 핵심 설계 원칙

1. **파라미터 기반 설계**: 모든 타이밍/모드 설정은 MCU가 SPI를 통해 레지스터에 기록
2. **물리 제약 준수**: τ_TFT, τ_RC 기반으로 최소 게이트 시간 자동 계산
3. **안전 우선**: 과노출 타임아웃, 전원 시퀀스 위반 감지, 강제 gate-off 보호 회로
4. **재사용성**: `detector_core.sv`는 모든 조합에서 공통 사용, 조합별 top-level만 변경

---

## 2. a-Si TFT 패널 물리 특성과 구동 제약 조건

### 2.1 픽셀 구조와 전하 축적 원리

a-Si:H TFT FPD의 각 픽셀은 세 가지 기능 요소로 구성된다:

1. **광변환 소자**: CsI:Tl 신틸레이터 + a-Si:H PIN 포토다이오드 (간접 변환 방식)
2. **축적 캐패시터**: 적분된 전하 보관 (Cpixel ≈ 1.9–5 pF, 196 µm 피치 기준)
3. **TFT 스위칭 소자**: Gate-ON 시 전하를 데이터 라인으로 전송

```
X-ray 광자
    ↓
CsI:Tl 신틸레이터 (600–1000 µm)
    ↓ 가시광 (550 nm peak, 2000–4000 photon/X-ray)
a-Si:H PIN 포토다이오드 (QE ~70–80% @ 550 nm)
    ↓ 전자-정공 쌍 생성
축적 캐패시터 (Cpd + Cs = 1.9–5 pF)
    ↓ TFT Gate-ON 시
데이터 라인 → AFE/ROIC → FPGA
```

**동작 모드**: 적분 모드(Integration Mode) — X-ray 조사 중 TFT 게이트 OFF를 유지하여 포토다이오드에 전하를 축적한 뒤, 행 순차 스캔으로 전하를 읽어낸다.

### 2.2 a-Si:H TFT 전기적 파라미터

| 파라미터 | 기호 | 값 | 출처 |
|---------|------|-----|------|
| 전계효과 이동도 | µ_FE | 0.3–1.1 cm²/(V·s) | [Liu 2013](https://swh.princeton.edu/~sturmlab/theses/Ting_Liu_Thesis_Part1.pdf) |
| 문턱 전압 | V_th | 0.7–2.0 V | [Liu 2013](https://swh.princeton.edu/~sturmlab/theses/Ting_Liu_Thesis_Part1.pdf) |
| ON/OFF 전류비 | I_ON/I_OFF | 10⁷–10⁹ | [Nathan 2000](https://www.fisica.unisa.it/antonio.dibartolomeo/MaterialeDidatticoFisicaSemiconduttori/Articoli/A-Si_andTFTtechnology%20for%20Xray%20detection.pdf) |
| OFF 상태 누설 | I_OFF | < 0.1 pA | [Nathan 2000](https://www.fisica.unisa.it/antonio.dibartolomeo/MaterialeDidatticoFisicaSemiconduttori/Articoli/A-Si_andTFTtechnology%20for%20Xray%20detection.pdf) |
| 서브스레숄드 기울기 | SS | 235–300 mV/dec | [Liu 2013](https://swh.princeton.edu/~sturmlab/theses/Ting_Liu_Thesis_Part1.pdf) |
| 게이트 유전체 비유전율 (SiNx) | ε_r | 6–7 | [Nathan 2000](https://www.fisica.unisa.it/antonio.dibartolomeo/MaterialeDidatticoFisicaSemiconduttori/Articoli/A-Si_andTFTtechnology%20for%20Xray%20detection.pdf) |

### 2.3 Charge Trapping 및 ΔV_th 메커니즘

a-Si:H TFT의 게이트 바이어스 스트레스(Bias Stress)에 의한 문턱전압 이동은 두 단계로 진행된다:

**Stage I — SiNx 전하 트래핑**:
- ΔV_th,max: 0.06–0.31 V
- 시상수 τ₀ at 20°C: 0.7–69.5 s
- 분산 파라미터 β: 0.15–0.52
- **회복**: 수 분 내 대부분 회복 (가역적)

**Stage II — a-Si:H 결함 생성**:
- ΔV_th,max: 최대 3.8 V
- 시상수 τ₀: 5.2 × 10⁸ s
- 활성화 에너지: 0.89–0.90 eV
- **회복**: 수 년 (비가역적 요소 포함)

```
V_th 이동 모델 (stretched-exponential):

  ΔV_th(t) = ΔV_th,∞ × [1 - exp(-(t/τ₀)^β)]

  여기서:
    ΔV_th,∞ = 최대 V_th 이동값
    τ₀ = 특성 시상수 (온도 의존)
    β = 분산 계수 (0 < β < 1)
```

**구동 알고리즘 함의**:
- Gate-ON 전압(VGH)은 V_th 이동을 수용할 수 있도록 여유를 두어야 한다: VGH ≥ V_th + ΔV_th,max + V_overdrive
- IDLE 모드에서 주기적 게이트 스캔으로 바이어스 스트레스를 분산해야 한다
- 양방향 스캔(bidirectional scan)으로 비대칭 V_th 이동을 완화한다

### 2.4 TFT RC 지연과 게이트 라인 타이밍 계산

#### 2.4.1 TFT 스위칭 RC 시상수

```
TFT 전하 전송 시상수:
  τ_TFT = R_on × (C_pixel + C_line)

  R_on = L / (W × µ_FE × C_ox × (V_GS - V_th))
       ≈ 0.1–3 MΩ (a-Si TFT, 패널 설계에 따라)

  C_pixel = C_pd + C_s ≈ 1–5 pF
  C_line  = 데이터 라인 기생 캐패시턴스 (패널 크기에 따라 10–50 pF)

  τ_TFT ≈ 3–9 µs (대표값)
```

**전하 전송률 vs 게이트 ON 시간**:

| 게이트 ON 시간 (× τ_TFT) | 전하 전송률 | 잔류 전하 |
|--------------------------|-----------|---------|
| 1 × τ_TFT | 63.2% | 36.8% |
| 3 × τ_TFT | 95.0% | 5.0% |
| 5 × τ_TFT | **99.3%** | **0.7%** |
| 7 × τ_TFT | 99.9% | 0.1% |

> **설계 요건**: T_gate_on ≥ 5 × τ_TFT = 5 × 9 µs = **45 µs** (최악 조건). AD71124의 최소 라인 타임 22 µs는 τ_TFT < 4.4 µs인 패널에만 적합하다.

#### 2.4.2 게이트 라인 RC 지연 — 3072 라인 패널

3072×3072 패널(X239AW1-102, 140 µm 피치)의 게이트 라인 RC 계산:

```
게이트 라인 물리 파라미터:
  라인 길이: L_gate = 3072 × 140 µm = 430 mm
  게이트 금속 (알루미늄): 면저항 ~0.1 Ω/□, 라인 폭 ~5 µm
  게이트 라인 저항: R_gate ≈ 0.1 × (430,000/5) = 8,600 Ω ≈ 9 kΩ

  픽셀당 게이트-드레인 오버랩 캐패시턴스: C_gd ≈ 50–190 fF (대표값 100 fF)
  총 게이트 라인 캐패시턴스: C_gate ≈ 100 fF × 3072 = 307 pF

  분포 RC 시상수:
    τ_RC = R × C / 3 ≈ 9 kΩ × 307 pF / 3 ≈ 0.92 µs
```

**게이트 신호 안정화 요건**:

| 안정화 조건 | 시간 | 잔류 오차 |
|-----------|------|---------|
| 3 × τ_RC | 2.76 µs | 5% |
| 5 × τ_RC | **4.60 µs** | **0.7%** |
| 7 × τ_RC | 6.44 µs | 0.1% |

> **구동 제약**: 3072 패널에서 게이트 ON 후 원단(far end)까지 신호가 안정되려면 최소 3–5 × τ_RC = **2.8–4.6 µs**의 안정화 시간이 필요하다. 이 시간은 T_gate_on 내에 포함되어야 한다.

#### 2.4.3 구동 알고리즘에 미치는 영향

```
┌────────────────────────────────────────────────────────────────────┐
│              타이밍 제약 종합 (3072 패널, 30fps 기준)                │
├────────────────────────────────────────────────────────────────────┤
│  프레임 주기: T_frame = 1/30 = 33.33 ms                           │
│  가용 리드아웃 시간: T_readout = 33.33 × 0.9 = 30.0 ms           │
│  라인 타임 예산: T_line = 30.0 ms / 3072 ≈ 9.77 µs               │
│                                                                    │
│  필요 T_gate_on ≥ 5 × τ_TFT                                      │
│    τ_TFT ≈ 3 µs (빠른 TFT) → T_gate_on ≥ 15 µs (초과!)          │
│    → 해결: AFE2256 파이프라인 모드로 T_line = 8 µs 달성            │
│    → 또는 2×2 비닝으로 유효 행 수 1536으로 감소                     │
│                                                                    │
│  게이트 안정화: T_settle = 5 × τ_RC = 4.6 µs                      │
│  AFE 변환 시간: T_adc ≤ 라인 타임 잔여                              │
└────────────────────────────────────────────────────────────────────┘
```

### 2.5 다크 전류 온도 의존성 (Arrhenius 모델)

a-Si:H PIN 포토다이오드의 다크 전류는 [Arrhenius 법칙](https://www.mhoheisel.de/docs/ISCMP91996112.pdf)을 따른다:

```
I_dark(T) = I_0 × exp(-E_a / kT)

  E_a ≈ 0.55–0.80 eV (공핍 영역 지배, a-Si:H)
  배증 온도: ΔT ≈ 8–10°C

  예시 (기준 25°C, I_dark = 1 pA):
    35°C → I_dark ≈ 2 pA   (×2)
    45°C → I_dark ≈ 4 pA   (×4)
    55°C → I_dark ≈ 8 pA   (×8)
```

**구동 알고리즘 함의**:
- 다크 프레임 보정은 온도 변화 ΔT > 2°C 시 재실행
- 온도 센서(NTC/디지털) 데이터를 FPGA가 모니터링하여 자동 트리거
- 장시간 적분(>100 ms) 시 다크 전류 누적이 IFS 범위를 침범할 수 있으므로 IFS를 한 단계 상향 조정

### 2.6 이미지 래그(Lag) 물리

**래그 정의**: 이전 프레임의 신호가 후속 프레임에 잔류하는 현상. a-Si:H FPD에서의 주요 원인은 포토다이오드 내 전하 트래핑이다.

```
래그 시간 상수 (N=4 지수 모델, Varian 4030CB, 15 fps):

  n=1 (최느림): τ₁ = 26.7 s,  b₁ = 7.1×10⁻⁶
  n=2:          τ₂ = 3.2 s,   b₂ = 1.1×10⁻⁴
  n=3:          τ₃ = 0.4 s,   b₃ = 1.7×10⁻³
  n=4 (최빠름): τ₄ = 87 ms,   b₄ = 1.8×10⁻²
```

| 프레임 | 비보정 래그 | FB 보정 후 | LTI 보정 후 | NLCSC 보정 후 |
|--------|-----------|----------|-----------|-------------|
| 1st | 2.4–3.7% | < 0.3% | 0.25% | **0.25%** |
| 50th | 0.28–0.96% | — | 0.0038% | **−0.003%** |

*출처: [Starman et al., Medical Physics 2012](https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/)*

---


### 2.7 픽셀 크로스토크 분석

인접 픽셀 간 크로스토크는 MTF(Modulation Transfer Function) 저하의 주요 원인이다. 세 가지 경로가 존재한다.

#### 2.7.1 광학적 크로스토크 (CsI:Tl 신틸레이터 산란)

CsI:Tl 주상정(columnar) 구조에서 빛은 주상정 내부에서 전반사로 가이드되지만, 일부 광자가 인접 주상정으로 산란된다.

| 파라미터 | 값 | 비고 |
|----------|-----|------|
| 주상정 지름 | 5–10 µm | [Tredwell 2009](https://www.imagesensors.org/Past%20Workshops/2009%20Workshop/2009%20Papers/065_paper_tredwell_invited.pdf) |
| 신틸레이터 두께 | 600–1000 µm | 두꺼울수록 감도↑, 해상도↓ |
| Nyquist 주파수 (196 µm pitch) | 2.55 lp/mm | — |
| MTF @ Nyquist (600 µm CsI) | ~18% | [Hoheisel 1996](https://www.mhoheisel.de/docs/ISCMP91996112.pdf) |
| MTF @ Nyquist (150 µm CsI) | ~60% | 얇은 신틸레이터 |

```
광학 크로스토크 모델:
  PSF(r) ≈ (1/2πσ²) × exp(-r²/2σ²)
  σ ≈ 0.4 × t_CsI (경험식, t_CsI = 신틸레이터 두께)

  600 µm CsI:  σ ≈ 240 µm → FWHM ≈ 565 µm (약 2.9 픽셀 @196µm)
  1000 µm CsI: σ ≈ 400 µm → FWHM ≈ 942 µm (약 4.8 픽셀 @196µm)
```

#### 2.7.2 전기적 크로스토크 (데이터 라인 커플링)

```
인접 데이터 라인 간 기생 커패시턴스:
  C_coupling ≈ ε₀ × ε_r × L / d

  ε_r ≈ 7 (SiNx 유전체)
  L = 패널 높이 ≈ 430 mm (3072행 패널)
  d = 데이터 라인 간격 ≈ 140 µm (피치) − 라인 폭

  C_coupling ≈ 8.854e-12 × 7 × 0.43 / 100e-6 ≈ 267 pF (최악)
  실제 배선 구조 고려: ~0.5–2 pF 정도

크로스토크 비율:
  XT = C_coupling / (C_coupling + C_data_line)
  C_data_line ≈ 30–50 pF → XT ≈ 1–4%
```

**FPGA 설계 시 대책:**
- 인접 채널 순차 읽기 시 가드 타임 삽입 (최소 1 µs)
- 차동 데이터 라인 구조 적용 시 크로스토크 90%+ 억제
- AFE 내 CDS가 공통 모드 노이즈 제거 → 실효 크로스토크 < 0.5%

#### 2.7.3 전하 확산 크로스토크

a-Si:H 포토다이오드 내 캐리어 확산:
- 확산 길이: L_diff ≈ 100–200 nm (a-Si:H 결함 밀도 높아 매우 짧음)
- 140 µm 픽셀 피치 대비 무시 가능 (L_diff / pitch < 0.2%)
- c-Si 대비 확산 크로스토크가 극히 낮은 것이 a-Si 의 구조적 장점

### 2.8 X-ray 선량에 따른 패널 응답 특성

#### 2.8.1 선형성 (Linearity)

a-Si:H FPD는 동작 범위 대부분에서 우수한 선형성을 보인다.

| 파라미터 | 값 | 출처 |
|----------|-----|------|
| 선형성 오차 (풀스케일 80% 이내) | < 0.3% | [Hoheisel 1996](https://www.mhoheisel.de/docs/ISCMP91996112.pdf) |
| 선형 응답 범위 | 풀스케일 25%까지 99%+ | [AMFPI 연구 1999](https://pubmed.ncbi.nlm.nih.gov/10501053/) |
| 포화 근접 비선형 시작 | 풀스케일 ~80% 이상 | — |

#### 2.8.2 다이나믹 레인지

| 파라미터 | 일반값 |
|----------|--------|
| ADC 분해능 | 14–16 bit |
| 다이나믹 레인지 (포화/노이즈 바닥) | 76.9:1 (단일 노출) ~ 166.7:1 (이중 노출) |
| 최대 선형 선량 | ~88 µGy ([PRORAD 사양](https://fcc.report/FCC-ID/2A7E500001/6084598.pdf)) |
| 감도 (CsI) | 574 LSB/µGy |
| 다크 노이즈 | 2.7 LSB |

#### 2.8.3 선량 이력 효과 (Dose History Effect)

래그 신호는 직전 프레임뿐 아니라 **누적 선량 이력**에 의존한다:
- 고선량 촬영(1 µGy) 후 저선량 형광투시(15 nGy/frame) 전환 시, 1차 래그 프레임에서 **최대 355 detector counts** 고스트 신호 발생
- EPID 응답은 선량률/PRF 범위에 따라 **최대 8%** 변동 ([PubMed 2004](https://pubmed.ncbi.nlm.nih.gov/15000614/))
- CBCT에서 래그 히스토리 차이가 **20–35 HU** shading artifact 유발

### 2.9 구동 알고리즘 설계 마진 종합표

| 파라미터 | 최소값 | 권장값 | 최대값 | 근거 |
|----------|--------|--------|--------|------|
| T_gate_on | 5×τ_TFT = 15 µs | 25 µs | 50 µs | 99.3% 전하 전송 |
| T_gate_settle | 3×τ_RC = 2.8 µs | 5 µs | 10 µs | 게이트 라인 안정화 |
| T_line (C1, C4) | 22 µs | 32 µs | 70 µs | AD71124 최소 라인타임 |
| T_line (C2) | 60 µs | 70 µs | 100 µs | AD71143 최소 라인타임 |
| T_line (C6, C7 @30fps) | 8 µs | 9.77 µs | 10.85 µs | 프레임 레이트 제약 |
| V_gate_on | Vth + 10V = 12V | +20V | +35V (NV1047) | TFT ON 전류 확보 |
| V_gate_off | −5V | −8V | −15V | OFF 누설 억제 |
| N_dummy_frames (콜드) | 50 | 100 | 200 | 트랩 안정화 |
| N_dummy_frames (웜) | 3 | 5 | 10 | 빠른 전환 |
| T_warmup | 30 min | 45 min | 60 min | 딥 트랩 평형 |

---

## 3. 전체 시스템 구동 상태 머신 (System-Level FSM)

### 3.1 최상위 상태 전이도

```
                     ┌──────────────────────────────────────────────┐
                     │          System-Level State Machine           │
                     └──────────────────────────────────────────────┘

  ┌──────────┐                                         ┌──────────┐
  │ POWER_OFF│                                         │  ERROR   │
  └────┬─────┘                                         └─────▲────┘
       │ VCC 인가                                            │ 에러 감지
       ▼                                                     │ (어느 상태에서든)
  ┌──────────┐    전원 시퀀스 완료                              │
  │ POWER_ON │─────────────────▶┌──────────────┐            │
  │ SEQUENCE │                  │ INITIALIZE   │────────────┤
  └──────────┘                  │ (AFE+Gate IC │            │
                                │  SPI 설정)   │            │
                                └──────┬───────┘            │
                                       │ 초기화 완료          │
                                       ▼                     │
                                ┌──────────────┐            │
                                │  STABILIZE   │────────────┤
                                │ (Dummy Scan  │            │
                                │  30-100 프레임)│            │
                                └──────┬───────┘            │
                                       │ 안정화 완료          │
                                       ▼                     │
                                ┌──────────────┐            │
                          ┌────▶│  CALIBRATE   │────────────┤
                          │     │ (Dark+Gain+  │            │
                          │     │  BadPixel)   │            │
                          │     └──────┬───────┘            │
                          │            │ 캘리브레이션 완료     │
                          │            ▼                     │
                 재캘리브  │     ┌──────────────┐            │
                 (ΔT>2°C) │     │    IDLE      │────────────┤
                          │     │ (Dummy Scan  │            │
                          └─────│  + 모니터링)  │            │
                                └──────┬───────┘            │
                                       │ MCU: START 명령     │
                                       ▼                     │
                                ┌──────────────┐            │
                          ┌────▶│   ACQUIRE    │────────────┘
                          │     │ (Reset→Integ │
                 Fluoro   │     │  →Readout)   │
                 연속모드  │     └──────┬───────┘
                          │            │ 프레임 완료
                          │            ▼
                          │     ┌──────────────┐
                          └─────│    DONE      │──▶ IDLE 복귀
                                │ (MCU IRQ)    │
                                └──────────────┘
```

### 3.2 각 상태별 상세 정의

#### 3.2.1 POWER_ON_SEQUENCE

**진입 조건**: 시스템 전원(VCC) 인가 감지  
**동작**:
1. 전원 레일 순차 인가 (VGL → VGH → AFE 아날로그 → 디지털)
2. Inrush 전류 제한: ~2.5 A peak, soft-start 적용
3. 각 레일 안정화 대기: ≥ 10 ms

```systemverilog
// Power Sequence Controller
typedef enum logic [3:0] {
    PWR_OFF         = 4'h0,
    PWR_VGL_EN      = 4'h1,  // VGL (-5V ~ -15V) 먼저 인가
    PWR_VGL_WAIT    = 4'h2,  // 10 ms 안정화 대기
    PWR_VGH_EN      = 4'h3,  // VGH (+20V ~ +35V) 인가
    PWR_VGH_WAIT    = 4'h4,  // 10 ms 안정화
    PWR_AFE_AVDD    = 4'h5,  // AFE 아날로그 전원 인가
    PWR_AFE_DVDD    = 4'h6,  // AFE 디지털 전원 인가
    PWR_AFE_WAIT    = 4'h7,  // 100 µs 클록 안정화
    PWR_DONE        = 4'h8
} pwr_state_t;
```

**전이 조건**: 모든 전원 레일 PG(Power Good) 확인 → INITIALIZE  
**보호**: 임의 레일 PG 실패 시 역순 전원 차단 → ERROR

> **중요**: VGL은 반드시 VGH보다 먼저 인가해야 한다. VGH가 먼저 인가되면 TFT가 순간적으로 ON 상태가 되어 포토다이오드에 역전압이 걸리지 않는 상황이 발생할 수 있다.

#### 3.2.2 INITIALIZE

**진입 조건**: POWER_ON_SEQUENCE 완료  
**동작**:
1. Gate IC 하드웨어 리셋 (RST 핀 Low→High, 1 ms)
2. AFE 하드웨어 리셋 (RESET 핀 Low→High, 1 ms)
3. AFE 클록 인가 (ACLK 또는 MCLK, 100 µs 안정화)
4. Gate IC SPI 초기화 (채널 모드, 스캔 방향 설정)
5. AFE SPI 초기화 (IFS, CDS, LPF, 전력 모드 설정)
6. AFE2256 전용: CIC 프로파일 로드 (사전 캘리브레이션 값)

**전이 조건**: SPI 초기화 응답 확인 → STABILIZE  
**소요 시간**: ~50–200 ms (SPI 설정 + 안정화)

#### 3.2.3 STABILIZE

**진입 조건**: INITIALIZE 완료  
**동작**:
- Dummy 스캔 실행: 30–100 프레임 (cold start 시)
- 트랩 상태 평형화: a-Si:H 포토다이오드의 charge trap을 채워서 안정 상태 도달
- 패널 온도 안정화: 연속 스캔에 의한 자가 가열로 열평형 접근

```
Dummy 스캔 권장 횟수:
  Cold start (전원 OFF > 30분): 100 프레임 + 30분 warm-up 대기
  Warm restart (전원 OFF < 5분): 30 프레임
  IDLE에서 복귀: 5–10 프레임
```

**전이 조건**: 지정된 dummy 프레임 수 완료 → CALIBRATE

#### 3.2.4 CALIBRATE

**진입 조건**: STABILIZE 완료 또는 재캘리브레이션 트리거  
**동작**:
1. 다크 프레임 획득 (N=64 프레임, X-ray 차단 상태)
2. 오프셋 맵 계산 (64 프레임 평균)
3. [선택] 플랫 필드 획득 (N=32–64 프레임, 균일 X-ray 조사)
4. 게인 맵 계산
5. 결함 화소 검출 (mean ± 3σ 기준)
6. AFE2256 전용: CIC 캘리브레이션 업데이트

**전이 조건**: 모든 캘리브레이션 맵 저장 완료 → IDLE

#### 3.2.5 IDLE

**진입 조건**: CALIBRATE 완료 또는 DONE 후 복귀  
**동작**:
- 연속 Dummy 스캔 (동일 프레임 레이트로 유지)
- 온도 모니터링 (ΔT > 2°C 시 재캘리브레이션 트리거)
- Scrubbing mode: 다크 프레임 롤링 업데이트 (α = 0.1)

**전이 조건**: MCU START 명령 수신 → ACQUIRE

#### 3.2.6 ACQUIRE

**진입 조건**: MCU START 명령 + IDLE 상태  
**동작**: (모드별 세분화, §8에서 상세 기술)
1. Pre-exposure 리셋 (3–8회)
2. X-ray 트리거 동기화
3. 적분 시간 관리
4. 행 순차 리드아웃
5. 보정 파이프라인 적용

**전이 조건**: 프레임 리드아웃 완료 → DONE (정지상) 또는 ACQUIRE 반복 (형광투시)

#### 3.2.7 DONE

**진입 조건**: ACQUIRE 프레임 완료  
**동작**:
- REG_STATUS[DONE] = 1 설정
- MCU 인터럽트 발생
- 프레임 데이터를 출력 버퍼에 준비

**전이 조건**: MCU ACK → IDLE (정지상) 또는 ACQUIRE (연속 모드)

### 3.3 전원 시퀀싱 상세

```
전원 인가 순서 (반드시 준수):

  ┌──────────────────────────────────────────────────────────┐
  │ 시간 →                                                   │
  │                                                          │
  │ VGL (-5V~-15V)  ████████████████████████████████████████ │
  │                  ↑ 0ms                                   │
  │                                                          │
  │ VGH (+20V~+35V)      ████████████████████████████████   │
  │                       ↑ 10ms                             │
  │                                                          │
  │ AFE AVDD (5V/3.3V)        █████████████████████████████ │
  │                            ↑ 20ms                        │
  │                                                          │
  │ AFE DVDD (1.85V/2.5V)           ██████████████████████  │
  │                                  ↑ 30ms                  │
  │                                                          │
  │ ACLK/MCLK                            ██████████████████ │
  │                                       ↑ 31ms            │
  │                                                          │
  │ SPI 초기화 시작                             ████████████ │
  │                                             ↑ 31.1ms    │
  └──────────────────────────────────────────────────────────┘
```

**AD71124/AD71143 전원 순서**:
1. AVDD5B (아날로그 바이어스 5V)
2. AVDD5F (프런트엔드 아날로그 5V)
3. AVDDI (ADC 아날로그)
4. DVDD5 (디지털 5V)
5. DVDD (디지털 코어)
6. IOVDD (I/O 전압)

**AFE2256 전원 순서**:
1. AVDD2 = 3.3 V (아날로그)
2. AVDD1 = 1.85 V (ADC 코어) — AVDD2보다 늦게 인가 (역전압 방지)
3. 디지털 I/O 전원

---

## 4. 패널 초기화 및 안정화 알고리즘

### 4.1 전원 ON 시퀀스 — 단계별 타이밍

```systemverilog
// 전원 시퀀스 제어 모듈
module power_sequence_ctrl #(
    parameter SYS_CLK_HZ = 100_000_000,
    parameter T_VGL_STABLE_MS  = 10,
    parameter T_VGH_STABLE_MS  = 10,
    parameter T_AFE_STABLE_MS  = 10,
    parameter T_CLK_STABLE_US  = 100,
    parameter T_RESET_WIDTH_MS = 1
)(
    input  logic clk, rst_n,
    input  logic power_en,
    output logic vgl_en, vgh_en, afe_avdd_en, afe_dvdd_en,
    output logic afe_reset_n, afe_clk_en,
    output logic pwr_good
);
    // 내부 카운터 기반 시퀀스 실행
    localparam CNT_10MS = SYS_CLK_HZ / 100;      // 10 ms
    localparam CNT_100US = SYS_CLK_HZ / 10_000;  // 100 µs
    localparam CNT_1MS  = SYS_CLK_HZ / 1_000;    // 1 ms

    typedef enum logic [3:0] {
        S_OFF, S_VGL, S_VGL_WAIT, S_VGH, S_VGH_WAIT,
        S_AFE_AVDD, S_AFE_DVDD, S_AFE_WAIT,
        S_RESET_LO, S_RESET_HI, S_CLK_START, S_CLK_WAIT,
        S_DONE
    } state_t;

    state_t state;
    logic [23:0] cnt;

    always_ff @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            state <= S_OFF;
            {vgl_en, vgh_en, afe_avdd_en, afe_dvdd_en} <= 4'b0;
            afe_reset_n <= 1'b0;
            afe_clk_en <= 1'b0;
            pwr_good <= 1'b0;
            cnt <= '0;
        end else begin
            case (state)
                S_OFF: if (power_en) begin
                    state <= S_VGL;
                    vgl_en <= 1'b1;
                    cnt <= CNT_10MS;
                end
                S_VGL: begin  // VGL 안정화 대기
                    cnt <= cnt - 1;
                    if (cnt == 0) begin
                        state <= S_VGH;
                        vgh_en <= 1'b1;
                        cnt <= CNT_10MS;
                    end
                end
                S_VGH: begin  // VGH 안정화 대기
                    cnt <= cnt - 1;
                    if (cnt == 0) begin
                        state <= S_AFE_AVDD;
                        afe_avdd_en <= 1'b1;
                        cnt <= CNT_10MS;
                    end
                end
                S_AFE_AVDD: begin
                    cnt <= cnt - 1;
                    if (cnt == 0) begin
                        afe_dvdd_en <= 1'b1;
                        cnt <= CNT_1MS;
                        state <= S_RESET_LO;
                    end
                end
                S_RESET_LO: begin  // AFE RESET 어시트 (1 ms)
                    afe_reset_n <= 1'b0;
                    cnt <= cnt - 1;
                    if (cnt == 0) begin
                        afe_reset_n <= 1'b1;
                        cnt <= CNT_100US;
                        state <= S_CLK_START;
                    end
                end
                S_CLK_START: begin  // ACLK/MCLK 인가
                    afe_clk_en <= 1'b1;
                    cnt <= cnt - 1;
                    if (cnt == 0) begin
                        state <= S_DONE;
                        pwr_good <= 1'b1;
                    end
                end
                S_DONE: ; // 전원 시퀀스 완료
            endcase
        end
    end
endmodule
```

### 4.2 a-Si TFT 트랩 안정화 — Dummy Scan

전원 인가 후 패널의 a-Si:H 트랩 상태가 평형에 도달하기 전까지 정상 영상 획득이 불가능하다.

**안정화 프로토콜**:

| 조건 | Dummy 스캔 횟수 | Warm-up 시간 | 비고 |
|------|---------------|-------------|------|
| Cold start (> 30분 OFF) | 100 프레임 | 30–60분 | 심부 트랩 포화 필요 |
| Warm restart (< 5분 OFF) | 30 프레임 | 5분 | 얕은 트랩만 재충전 |
| IDLE 복귀 | 5–10 프레임 | 즉시 | 트랩 상태 유지됨 |
| 고선량 노출 후 | 3–8 리셋 스캔 | 30초 | 잔류 전하 제거 |

*출처: [Starman 2011](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/); [AAPM DR Review](https://www.aapm.org/meetings/03am/pdf/9877-37003.pdf)*

```systemverilog
// Dummy Scan Controller
module dummy_scan_ctrl #(
    parameter MAX_DUMMY_FRAMES = 255
)(
    input  logic clk, rst_n,
    input  logic start_dummy,
    input  logic [7:0] n_dummy_frames,  // REG_DUMMY_N
    input  logic frame_done,             // 한 프레임 완료 펄스
    output logic dummy_active,
    output logic dummy_complete
);
    logic [7:0] frame_cnt;

    always_ff @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            frame_cnt <= '0;
            dummy_active <= 1'b0;
            dummy_complete <= 1'b0;
        end else if (start_dummy) begin
            frame_cnt <= '0;
            dummy_active <= 1'b1;
            dummy_complete <= 1'b0;
        end else if (dummy_active && frame_done) begin
            if (frame_cnt >= n_dummy_frames - 1) begin
                dummy_active <= 1'b0;
                dummy_complete <= 1'b1;
            end else begin
                frame_cnt <= frame_cnt + 1;
            end
        end
    end
endmodule
```

### 4.3 Gate IC 초기화

#### 4.3.1 NV1047 초기화 (C1–C5)

```
NV1047 초기화 시퀀스:
  1. RST = Low (최소 10 µs) → High
  2. MD[1:0] 핀 설정: 채널 수 선택
     - MD=00: 300ch, MD=01: 263ch, MD=10: 256ch, MD=11: 예비
  3. L/R 핀: 스캔 방향 (0=상향, 1=하향)
  4. OE = High (초기 상태: 전체 Gate OFF = VEE)
  5. ONA = High (초기 상태: 전체 Gate 정상 동작)
  6. CLK = Low (정지 상태)
  7. SD1 = Low (데이터 초기값)
```

#### 4.3.2 NT39565D 초기화 (C6–C7)

```
NT39565D 초기화 시퀀스:
  1. 하드웨어 리셋 (전원 사이클 또는 전용 RST 핀)
  2. MODE1/MODE2 핀 설정: 채널 모드 선택
     - 2G 모드: STV1/STV2 교번 → 541ch
     - Normal: STV1만 → 385ch (대표)
  3. LR 핀: 스캔 방향 (H=상향, L=하향)
  4. OE1 = High, OE2 = High (초기: 전체 Gate OFF)
  5. CPV = Low (정지)
  6. STV1 = Low, STV2 = Low (스타트 펄스 비활성)
  
  칩 캐스케이드 (6개 IC):
  7. IC[0] STVD → IC[1] STVU (상향 캐스케이드)
  8. IC[5] STVD → IC[4] STVU (하향 캐스케이드)
  
  양방향 동시 스캔 시:
  9. STV1 → IC[0] (상단 시작)
  10. STV2 → IC[5] (하단 시작, 역방향)
```

### 4.4 AFE 초기화

#### 4.4.1 AD71124/AD71143 초기화 (ACLK 기반)

```systemverilog
// AD71124/AD71143 AFE SPI 초기화 시퀀스
task automatic afe_ad711xx_init(
    input logic [5:0] ifs_code,     // IFS 설정 (0~63)
    input logic [3:0] lpf_code,     // LPF 시상수
    input logic [1:0] power_mode    // 전력 모드
);
    // Step 1: RESET 핀 Low → High (1 ms)
    afe_reset_n = 1'b0;
    #(1_000_000);  // 1 ms
    afe_reset_n = 1'b1;
    #(1_000_000);  // 1 ms 대기

    // Step 2: ACLK 인가 시작 (40 MHz)
    aclk_enable = 1'b1;
    #(100_000);  // 100 µs 안정화

    // Step 3: SPI 레지스터 설정
    spi_write(REG_IFS, {2'b00, ifs_code});      // IFS 범위 설정
    spi_write(REG_LPF, {4'b0000, lpf_code});    // LPF 차단 주파수
    spi_write(REG_POWER, {6'b000000, power_mode}); // 전력 모드
    spi_write(REG_CDS, 8'h01);                   // CDS 듀얼 뱅킹 활성화
    spi_write(REG_OUTPUT, 8'h03);                 // LVDS DDR 모드

    // Step 4: 더미 프레임 대기 (2 라인 이상)
    wait_frames(2);

    // Step 5: 오프셋 캘리브레이션 준비 완료
endtask
```

| 타이밍 이벤트 | 지연 | 비고 |
|-------------|------|------|
| 전원 안정 → RESET | 10 ms | 모든 레일 안정 후 |
| RESET 폭 | 1 ms | 내부 POR |
| RESET 해제 → ACLK | 100 µs | 클록 도메인 안정 |
| ACLK → SPI 시작 | 10 클록 | 내부 POR 완료 |
| SPI 완료 → SYNC | 10 µs | 레지스터 반영 |

#### 4.4.2 AFE2256 초기화 (MCLK 기반)

```
AFE2256 SPI 초기화 순서:
  1. MCLK 인가 (32 MHz, 내부 TG 소스)
  2. SEN = High (SPI 비활성화 확인)
  3. 소프트 리셋: REG_SOFT_RESET ← 0x01
     → 100 µs 대기
  4. 클록 모드 설정: REG_CLOCK_CONFIG ← MCLK_DIV_4
  5. IFS 설정: REG_IFS_SELECT ← 원하는 코드 (0.6~9.6 pC)
  6. CDS 설정: REG_CDS_CONFIG ← CDS_DUAL_BANK | CDS_NORMAL
  7. CIC 활성화: REG_CIC_ENABLE ← 0x01
  8. CIC 프로파일 로드: 채널별 256개 값 SPI 전송
  9. 파이프라인 모드: REG_PIPELINE_EN ← PIPELINE_ENABLE
  10. LVDS 출력: REG_OUTPUT_FORMAT ← LVDS_DDR_MODE
  11. 파워다운 해제: REG_POWER_MODE ← NORMAL_OPERATION
  12. 안정화 대기: 500 µs
```

### 4.5 CIC 캘리브레이션 절차 (AFE2256 전용)

TFT 게이트 전압 전환 시 기생 용량(C_gd)을 통한 전하 주입(Charge Injection)은 AFE의 유효 다이나믹 레인지를 심각하게 감소시킨다.

```
전하 주입 메커니즘:
  ΔV_gate = VGH - VGL ≈ 25V (예: +20V - (-5V))
  C_gd ≈ 20 fF (TFT 게이트-드레인 오버랩)
  Q_inj = ΔV_gate × C_gd = 25V × 20fF = 0.5 pC (전형적)

  ROIC 입력 오프셋: ΔV = Q_inj / C_F = 0.5pC / 0.5pF = 1V
  → AFE 출력 클리핑 또는 비선형 영역 진입 가능

CIC 보상 효과:
  보상 전: 사용 가능 다이나믹 레인지 ~50%
  보상 후: 사용 가능 다이나믹 레인지 ~92%
```

*출처: [US20150256765A1](https://patents.google.com/patent/US20150256765A1/en)*

```systemverilog
// CIC 캘리브레이션 플로우 (AFE2256)
module cic_calibrator #(
    parameter N_CHANNELS = 256,
    parameter N_DARK_FRAMES = 8
)(
    input  logic clk, rst_n,
    input  logic start_cal,
    input  logic frame_valid,
    input  logic [15:0] pixel_data [N_CHANNELS-1:0],
    output logic [7:0] cic_profile [N_CHANNELS-1:0],
    output logic cal_done,
    // SPI 인터페이스
    output logic spi_wr_en,
    output logic [7:0] spi_addr,
    output logic [7:0] spi_wdata
);
    typedef enum {S_IDLE, S_CAPTURE, S_AVERAGE, S_SPI_WRITE, S_DONE} state_t;
    state_t state;

    logic [31:0] sum [N_CHANNELS-1:0];
    logic [3:0] frame_cnt;
    logic [8:0] ch_idx;

    always_ff @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            state <= S_IDLE;
            cal_done <= 1'b0;
        end else case (state)
            S_IDLE: if (start_cal) begin
                state <= S_CAPTURE;
                frame_cnt <= '0;
                for (int i = 0; i < N_CHANNELS; i++) sum[i] <= '0;
            end
            S_CAPTURE: if (frame_valid) begin
                for (int i = 0; i < N_CHANNELS; i++)
                    sum[i] <= sum[i] + pixel_data[i];
                if (frame_cnt >= N_DARK_FRAMES - 1)
                    state <= S_AVERAGE;
                else
                    frame_cnt <= frame_cnt + 1;
            end
            S_AVERAGE: begin
                for (int i = 0; i < N_CHANNELS; i++)
                    cic_profile[i] <= sum[i][23:16]; // ÷ 256 (N=8의 근사)
                state <= S_SPI_WRITE;
                ch_idx <= '0;
            end
            S_SPI_WRITE: begin
                spi_wr_en <= 1'b1;
                spi_addr  <= 8'h80 + ch_idx[7:0]; // CIC_PROFILE 레지스터
                spi_wdata <= cic_profile[ch_idx];
                if (ch_idx >= N_CHANNELS - 1) state <= S_DONE;
                else ch_idx <= ch_idx + 1;
            end
            S_DONE: begin
                cal_done <= 1'b1;
                spi_wr_en <= 1'b0;
            end
        endcase
    end
endmodule
```

### 4.6 Refresh/Prepare Cycle

[Carestream EP2148500A1](https://patents.google.com/patent/EP2148500A1/en) 방식을 기반으로 한 패널 준비 사이클:

```
Refresh/Prepare Cycle 알고리즘:
  ┌─────────────────────────────────────────┐
  │ 1. Refresh Phase (트랩 리프레시)         │
  │    - 전체 게이트 라인 순차 ON/OFF        │
  │    - 잔류 전하 완전 방전                  │
  │    - 횟수: 3–5 패스                      │
  │    - 소요 시간: 3 × 61 ms = 183 ms       │
  │      (3072 rows × 20 µs/line × 3 pass)  │
  ├─────────────────────────────────────────┤
  │ 2. Prepare Phase (안정화)                │
  │    - X-ray 없이 정상 스캔 모드 실행       │
  │    - 다크 프레임 수집 시작                │
  │    - 다크 전류 평형 도달 대기             │
  │    - 횟수: 5–10 프레임                   │
  ├─────────────────────────────────────────┤
  │ 3. Calibration Phase                    │
  │    - 오프셋 맵 갱신 (64 프레임 평균)      │
  │    - 온도 메타데이터 기록                 │
  └─────────────────────────────────────────┘
```

---

## 5. IDLE 상태 구동 알고리즘

### 5.1 IDLE Dummy Scan

IDLE 모드에서는 패널의 TFT 바이어스 상태와 다크 전류 평형을 유지하기 위해 연속적인 dummy 스캔을 실행한다.

**목적**:
1. **트랩 평형 유지**: 주기적 게이트 펄스로 TFT 운동 상태 유지, 트랩 부분 충전 상태 보존
2. **다크 전류 평형**: 포토다이오드 전하 축적이 순방향 바이어스로 전환되는 것을 방지
3. **V_th 드리프트 억제**: 교번 바이어스로 지속적 스트레스 축적 방지
4. **즉시 촬영 준비**: 예측 가능한 초기 상태 보장

```systemverilog
// IDLE Mode Controller
module idle_mode_ctrl (
    input  logic clk, rst_n,
    input  logic idle_active,
    input  logic [15:0] reg_tline,       // 라인 타임 (10ns 단위)
    input  logic [11:0] reg_nrows,       // 유효 행 수
    output logic gate_scan_en,           // 게이트 스캔 활성화
    output logic idle_frame_done,        // IDLE 프레임 완료 펄스
    output logic data_discard            // 데이터 버림 표시
);
    logic [11:0] row_cnt;
    logic [15:0] line_timer;

    always_ff @(posedge clk or negedge rst_n) begin
        if (!rst_n || !idle_active) begin
            row_cnt <= '0;
            gate_scan_en <= 1'b0;
            idle_frame_done <= 1'b0;
        end else begin
            data_discard <= 1'b1;  // IDLE 중 데이터 항상 폐기
            gate_scan_en <= 1'b1;

            if (line_timer >= reg_tline) begin
                line_timer <= '0;
                if (row_cnt >= reg_nrows - 1) begin
                    row_cnt <= '0;
                    idle_frame_done <= 1'b1;
                end else begin
                    row_cnt <= row_cnt + 1;
                    idle_frame_done <= 1'b0;
                end
            end else begin
                line_timer <= line_timer + 1;
            end
        end
    end
endmodule
```

### 5.2 Scrubbing Mode (다크 프레임 롤링 업데이트)

[GE US5452338A](https://patents.google.com/patent/US5452338A) 방식을 참조한 온라인 다크 맵 갱신:

```
Scrubbing 알고리즘:
  IDLE 스캔 중 획득한 프레임을 다크 맵에 점진적으로 반영

  D_map_new(i,j) = α × D_current(i,j) + (1-α) × D_map_old(i,j)

  α = 0.1 (권장): 노이즈 저감과 추적 속도의 균형
  α = 0.3: 빠른 온도 변화 추적 (portable 환경)

  업데이트 주기: 매 10 프레임마다 1회 (노이즈 평균화)
  메모리 요구: 16-bit × 패널 크기 (R1717: 4 MB, X239AW1-102: 18 MB)
```

```systemverilog
// Dark Map Rolling Update
module dark_scrubber #(
    parameter ALPHA_Q8 = 8'd26  // α ≈ 0.1 in Q8 format (26/256)
)(
    input  logic clk,
    input  logic update_en,
    input  logic [15:0] current_dark,     // 현재 IDLE 프레임 픽셀값
    input  logic [15:0] stored_dark,      // 저장된 다크 맵 값
    output logic [15:0] updated_dark      // 갱신된 다크 맵 값
);
    logic [23:0] term_new, term_old;

    always_comb begin
        if (update_en) begin
            term_new = ALPHA_Q8 * current_dark;              // α × D_new
            term_old = (8'd256 - ALPHA_Q8) * stored_dark;    // (1-α) × D_old
            updated_dark = (term_new + term_old) >> 8;       // Q8 → Q0
        end else begin
            updated_dark = stored_dark;
        end
    end
endmodule
```

### 5.3 NAP Mode vs. Full Active IDLE

| 모드 | AFE 상태 | 게이트 스캔 | 전력 소비 | 복귀 시간 | 적용 조건 |
|------|---------|-----------|---------|---------|---------|
| **Full Active IDLE** | 정상 동작 | 연속 스캔 | 100% | 즉시 (~ms) | 빈번한 촬영 예상 |
| **NAP Mode** | NAP (저전력) | 중단 | 30–50% | 10 µs + 2 프레임 | 5분 이상 미사용 |
| **Power Down** | 전체 OFF | 중단 | < 5% | 전원 시퀀스 (~50 ms) | 장기 미사용 |

**NAP → Active 전환 프로토콜**:
1. AFE NAP 모드 해제 명령 (SPI)
2. 최소 10 µs 대기 (LVDS 출력 재안정화)
3. 2 프레임 더미 스캔 (LVDS 잠금 확인)
4. Preconditioning: 5–10 프레임 IDLE 스캔
5. 정상 ACQUIRE 가능

### 5.4 IDLE → Active 전환 시 Preconditioning

```
전환 시퀀스:
  ┌──────────────────────────────────────────────────────┐
  │ IDLE → ACQUIRE 전환 Preconditioning                   │
  │                                                      │
  │ Step 1: NAP 해제 (해당 시)         0 µs              │
  │ Step 2: LVDS 재잠금               10 µs              │
  │ Step 3: Dummy scan × N            N × T_frame        │
  │         N = 5 (짧은 IDLE 후)                          │
  │         N = 10 (NAP 복귀 후)                          │
  │         N = 100 (Power Down 복귀 후)                  │
  │ Step 4: 오프셋 보정 확인          선택적               │
  │ Step 5: 촬영 준비 완료            MCU에 통보          │
  └──────────────────────────────────────────────────────┘
```

---


### 5.5 IDLE 모드 전용 SystemVerilog 구현

#### 5.5.1 IDLE FSM 상세 설계

```systemverilog
// idle_mode_controller.sv — IDLE 모드 전용 제어 FSM
module idle_mode_controller #(
    parameter SYS_CLK_HZ     = 100_000_000,
    parameter MAX_ROWS       = 3072,
    parameter SCRUB_INTERVAL = 32'd100_000_000  // 1초 간격 (100MHz 기준)
)(
    input  logic              clk,
    input  logic              rst_n,
    input  logic              idle_en,            // IDLE 모드 진입 허가
    input  logic              acq_request,        // 획득 모드 전환 요청
    input  logic [1:0]        idle_mode_sel,      // 00=FULL_ACTIVE, 01=NAP, 10=SCRUB
    input  logic [15:0]       t_idle_line,        // IDLE 스캔 라인 타임 (clk 단위)
    input  logic [7:0]        n_precond_frames,   // Preconditioning 프레임 수
    // Gate IC 인터페이스
    output logic              gate_scan_start,
    output logic [11:0]       gate_row_idx,
    output logic              gate_on_pulse,
    // AFE 인터페이스
    output logic              afe_drain_mode,     // 1=전하 폐기 모드
    // 상태 출력
    output logic [2:0]        idle_state,
    output logic              ready_for_acq,      // 획득 준비 완료
    // 온도 모니터링
    input  logic [11:0]       temp_adc_val,       // ADC 온도 측정값
    output logic              dark_update_req      // 다크 맵 업데이트 필요
);

    // 상태 정의
    typedef enum logic [2:0] {
        IDLE_ENTRY     = 3'd0,
        IDLE_SCAN      = 3'd1,
        IDLE_NAP       = 3'd2,
        IDLE_SCRUB     = 3'd3,
        PRECONDITION   = 3'd4,
        READY          = 3'd5
    } idle_state_t;

    idle_state_t state, next_state;
    logic [31:0] scrub_timer;
    logic [11:0] row_cnt;
    logic [15:0] line_timer;
    logic [7:0]  precond_cnt;
    logic [11:0] temp_last_cal;
    logic [11:0] temp_delta;

    // 온도 변화 감지 (ΔT > 2°C 시 다크 맵 업데이트)
    // 12-bit ADC: 1 LSB ≈ 0.0625°C 가정 → 2°C = 32 LSB
    localparam TEMP_THRESHOLD = 12'd32;

    always_ff @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            state        <= IDLE_ENTRY;
            row_cnt      <= '0;
            line_timer   <= '0;
            precond_cnt  <= '0;
            scrub_timer  <= '0;
            temp_last_cal <= '0;
        end else begin
            state <= next_state;

            case (state)
                IDLE_ENTRY: begin
                    row_cnt     <= '0;
                    line_timer  <= '0;
                    scrub_timer <= '0;
                    temp_last_cal <= temp_adc_val;
                end

                IDLE_SCAN: begin
                    if (line_timer == t_idle_line - 1) begin
                        line_timer <= '0;
                        row_cnt <= (row_cnt == MAX_ROWS - 1) ? '0 : row_cnt + 1;
                    end else begin
                        line_timer <= line_timer + 1;
                    end
                end

                IDLE_SCRUB: begin
                    scrub_timer <= scrub_timer + 1;
                end

                PRECONDITION: begin
                    if (line_timer == t_idle_line - 1) begin
                        line_timer <= '0;
                        if (row_cnt == MAX_ROWS - 1) begin
                            row_cnt <= '0;
                            precond_cnt <= precond_cnt + 1;
                        end else begin
                            row_cnt <= row_cnt + 1;
                        end
                    end else begin
                        line_timer <= line_timer + 1;
                    end
                end

                default: ;
            endcase
        end
    end

    always_comb begin
        next_state     = state;
        gate_scan_start = 1'b0;
        gate_on_pulse  = 1'b0;
        afe_drain_mode = 1'b1;  // IDLE 중 기본 폐기 모드
        ready_for_acq  = 1'b0;
        dark_update_req = 1'b0;

        // 온도 변화량 계산
        temp_delta = (temp_adc_val > temp_last_cal) ?
                     (temp_adc_val - temp_last_cal) :
                     (temp_last_cal - temp_adc_val);

        if (temp_delta > TEMP_THRESHOLD)
            dark_update_req = 1'b1;

        case (state)
            IDLE_ENTRY: begin
                if (idle_en) begin
                    case (idle_mode_sel)
                        2'b00:   next_state = IDLE_SCAN;
                        2'b01:   next_state = IDLE_NAP;
                        2'b10:   next_state = IDLE_SCRUB;
                        default: next_state = IDLE_SCAN;
                    endcase
                end
            end

            IDLE_SCAN: begin
                gate_scan_start = 1'b1;
                gate_on_pulse   = (line_timer < t_idle_line / 2);
                if (acq_request)
                    next_state = PRECONDITION;
            end

            IDLE_NAP: begin
                // NAP 모드: AFE 저전력, Gate 스캔 정지
                if (acq_request)
                    next_state = PRECONDITION;
            end

            IDLE_SCRUB: begin
                // 주기적 다크 프레임 수집
                if (scrub_timer >= SCRUB_INTERVAL) begin
                    gate_scan_start = 1'b1;
                    next_state = IDLE_SCAN;
                end
                if (acq_request)
                    next_state = PRECONDITION;
            end

            PRECONDITION: begin
                gate_scan_start = 1'b1;
                gate_on_pulse   = (line_timer < t_idle_line / 2);
                afe_drain_mode  = 1'b1;
                if (precond_cnt >= n_precond_frames)
                    next_state = READY;
            end

            READY: begin
                ready_for_acq = 1'b1;
                afe_drain_mode = 1'b0;
            end
        endcase
    end

    assign idle_state  = state;
    assign gate_row_idx = row_cnt;

endmodule
```

#### 5.5.2 IDLE → Acquisition 전환 타이밍 다이어그램

```
시간 →
                                                              
ACQ_REQUEST  ────────────┐
                         │
IDLE_SCAN  ═══════════╗  ▼
                      ╚═══╗
PRECONDITION              ║ N_precond 프레임 실행
  Frame 0  ├─────────────┤│
  Frame 1  ├─────────────┤│
    ...                   ││
  Frame N-1├─────────────┤│
                          ╚═══╗
READY_FOR_ACQ                  ║  ◀── 여기서 panel_ctrl_fsm에 시작 신호
                               ╚══════════════════
                                                  
GATE_ON    ┌─┐ ┌─┐ ┌─┐   ┌─┐ ┌─┐ ┌─┐     (정상 스캔으로 전환)
───────────┘ └─┘ └─┘ └───┘ └─┘ └─┘ └─────

AFE_MODE   [DRAIN]──────────[DRAIN]────────[ACTIVE]
                                            ▲
                                            │ PRECONDITION 완료 시 전환
```

#### 5.5.3 조합별 IDLE 모드 파라미터

| 파라미터 | C1/C4 | C2 | C3/C5 | C6/C7 |
|----------|-------|-----|-------|-------|
| IDLE 스캔 주기 | 7.5 fps | 5 fps | 7.5 fps | 15 fps |
| T_idle_line | 32 µs | 70 µs | 32 µs | 10 µs |
| N_precond (콜드→촬영) | 10 | 10 | 8 | 5 |
| N_precond (웜→촬영) | 3 | 3 | 3 | 2 |
| NAP 모드 전류 | 50 mA | 20 mA | 45 mA | 200 mA |
| Full Active 전류 | 200 mA | 80 mA | 180 mA | 1.2 A |
| 온도 모니터링 주기 | 1 s | 1 s | 1 s | 0.5 s |

### 5.6 열 관리 및 다크 전류 보상 전략

#### 5.6.1 온도 기반 자동 캘리브레이션 트리거

```
온도 모니터링 절차 (IDLE 모드 중):

  1. 매 1초마다 온도 ADC 읽기
  2. 마지막 다크 캘리브레이션 온도와 비교
  3. ΔT > 2°C → dark_update_req 플래그 설정
  4. panel_ctrl_fsm이 다음 IDLE 프레임에서 자동 다크 프레임 수집
  5. 롤링 평균 업데이트: D_new = α×D_current + (1−α)×D_old (α=0.1~0.3)

다크 전류 온도 의존성:
  I_dark(T) = I_0 × exp(−ΔE/kT)
  ΔE ≈ 0.55–1.1 eV (a-Si:H)
  
  실용적 근사: 다크 전류는 매 8°C 온도 상승마다 약 2배 증가
  
  예시:
    T = 25°C: I_dark = 0.7 nA/cm²
    T = 33°C: I_dark ≈ 1.4 nA/cm²
    T = 41°C: I_dark ≈ 2.8 nA/cm²
```

#### 5.6.2 다크 맵 롤링 업데이트 SystemVerilog 인터페이스

```systemverilog
// dark_map_updater.sv — 롤링 다크 맵 업데이트 제어
module dark_map_updater #(
    parameter ALPHA_Q8  = 8'd26,    // α ≈ 0.1 in Q8 (26/256)
    parameter N_PIXELS  = 3072*3072
)(
    input  logic        clk,
    input  logic        rst_n,
    input  logic        update_start,
    input  logic [15:0] new_dark_pixel,   // 새 다크 프레임 픽셀
    input  logic [15:0] old_dark_pixel,   // 기존 다크 맵 픽셀
    output logic [15:0] updated_pixel,    // 업데이트된 다크 맵 픽셀
    output logic        pixel_valid,
    output logic        update_done
);
    logic [23:0] product_new;   // α × new
    logic [23:0] product_old;   // (1-α) × old
    logic [23:0] sum;

    always_ff @(posedge clk) begin
        if (update_start) begin
            // D_new = α×D_current + (1−α)×D_old
            product_new <= ALPHA_Q8 * new_dark_pixel;           // Q8 × Q0 = Q8
            product_old <= (8'd256 - ALPHA_Q8) * old_dark_pixel; // Q8 × Q0 = Q8
        end
    end

    assign sum = product_new + product_old;
    assign updated_pixel = sum[23:8];  // Q8 → Q0 변환 (8비트 우측 시프트)

endmodule
```

---

## 6. Gate IC 최적 제어 알고리즘

### 6.1 공통 Gate 스캔 알고리즘

#### 6.1.1 T_gate_on 계산 — 5×τ_TFT 조건

게이트 ON 시간은 픽셀 전하의 99.3% 이상을 데이터 라인으로 전송하기 위해 최소 5×τ_TFT 이상이어야 한다:

```
T_gate_on ≥ 5 × τ_TFT

τ_TFT = R_on × (C_pixel + C_line)

  R_on = L / (W × µ_FE × C_ox × (V_GS - V_th))

  대표 계산 예 (R1717 패널):
    R_on ≈ 1 MΩ (a-Si, V_GS - V_th = 15V)
    C_pixel ≈ 3 pF
    C_line ≈ 20 pF (2048 rows 패널)
    τ_TFT = 1 MΩ × (3 + 20) pF = 23 µs  → T_gate_on ≥ 115 µs

  최적화 계산 (V_GS 높임):
    R_on ≈ 0.3 MΩ (V_GS - V_th = 25V)
    τ_TFT = 0.3 MΩ × 23 pF = 6.9 µs  → T_gate_on ≥ 34.5 µs

  빠른 TFT (소형 패널):
    R_on ≈ 0.5 MΩ
    C_pixel + C_line ≈ 6 pF
    τ_TFT ≈ 3 µs → T_gate_on ≥ 15 µs
```

#### 6.1.2 T_gate_settle 계산

게이트 전압 전환(VGG → VEE) 후 안정화 시간:

```
T_gate_settle ≥ 5 × τ_RC (게이트 라인)
  τ_RC ≈ 0.92 µs (3072 패널)
  T_gate_settle ≥ 4.6 µs

소형 패널 (R1717, ~2048 columns):
  τ_RC ≈ 0.4 µs → T_gate_settle ≥ 2 µs
```

#### 6.1.3 Gate ON 전압 최적화

```
V_GH 최적화:
  V_GH ≥ V_th + ΔV_th,max + V_overdrive + V_margin

  V_th = 2 V (초기)
  ΔV_th,max = 3.8 V (최악, 장기 스트레스)
  V_overdrive = 10 V (충분한 R_on 감소)
  V_margin = 5 V (공정 마진)
  → V_GH ≥ 20.8 V → 설정: +25 V (표준)

  R_on vs V_GH 관계:
    R_on ∝ 1/(V_GH - V_th)
    V_GH 높을수록 R_on 감소 → τ_TFT 감소 → 더 빠른 스캔 가능
    단, V_GH 과대 시 TFT 게이트 절연막 신뢰성 저하

V_GL 최적화:
  V_GL ≤ -(|V_th| + V_margin)
  V_GL = -5 V ~ -10 V (I_off < 10⁻¹³ A 보장)
```

### 6.2 NV1047 전용 알고리즘

#### 6.2.1 SD1/CLK 시프트 시퀀스

```
NV1047 Gate 스캔 시퀀스:

  CLK   ─┐ ┌─┐ ┌─┐ ┌─┐ ┌─┐ ┌─┐ ┌─
          └─┘ └─┘ └─┘ └─┘ └─┘ └─┘

  SD1   ─┐                              (1 CLK 폭 하이 펄스)
          └──────────────────────────

  OE    ──┐                    ┌──       (Gate ON 구간)
           └────────────────────┘

  GATE  ──┐  ROW0               ┌──┐  ROW1
  OUT[0]   └─ VGH ──────────────┘VEE └── ...
  OUT[1] ──────────────┐  ROW1  ┌──── ...
                        └──VGH──┘

동작 원리:
  1. SD1에 1-CLK 폭 시작 펄스 인가
  2. 매 CLK 상승 에지에서 시프트 레지스터 1비트 전진
  3. OE = Low: 현재 활성 행이 VGH 출력
  4. OE = High: 전체 출력 VEE (Gate OFF)
```

```systemverilog
// NV1047 Gate Driver Controller
module gate_nv1047 #(
    parameter MAX_ROWS = 2048
)(
    input  logic clk, rst_n,
    input  logic scan_start,
    input  logic [10:0] n_rows,          // 유효 행 수
    input  logic [11:0] t_gate_on,       // Gate ON 카운트
    input  logic [7:0]  t_gate_settle,   // Gate settle 카운트
    input  logic scan_dir,               // 0=정방향, 1=역방향
    input  logic reset_all,              // 전체 리셋 요청

    output logic nv_sd1,                 // Start Data
    output logic nv_clk,                 // Shift Clock
    output logic nv_oe,                  // Output Enable (Active Low)
    output logic nv_ona,                 // All ON (Active Low)
    output logic nv_lr,                  // Left/Right (scan direction)
    output logic nv_rst,                 // Reset

    output logic row_active,             // 현재 행 활성 표시
    output logic [10:0] current_row,     // 현재 행 인덱스
    output logic scan_done               // 전체 스캔 완료
);
    typedef enum logic [3:0] {
        S_IDLE, S_RESET, S_START_PULSE, S_CLK_HIGH, S_CLK_LOW,
        S_GATE_ON, S_GATE_SETTLE, S_NEXT_ROW, S_DONE
    } state_t;
    state_t state;

    logic [11:0] timer;
    logic [10:0] row_idx;

    always_ff @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            state <= S_IDLE;
            {nv_sd1, nv_clk, nv_oe, nv_ona} <= 4'b0010; // OE=H (off)
            nv_lr <= 1'b0;
            nv_rst <= 1'b1;
            scan_done <= 1'b0;
        end else case (state)

            S_IDLE: begin
                nv_oe <= 1'b1;           // Gate OFF
                nv_ona <= 1'b1;          // 정상 모드
                nv_lr <= scan_dir;
                scan_done <= 1'b0;

                if (reset_all) begin
                    nv_ona <= 1'b0;      // 전체 VGH (리셋)
                    state <= S_RESET;
                    timer <= t_gate_on;
                end else if (scan_start) begin
                    row_idx <= '0;
                    state <= S_START_PULSE;
                end
            end

            S_RESET: begin               // 전체 리셋 모드
                if (timer == 0) begin
                    nv_ona <= 1'b1;      // 리셋 해제
                    state <= S_IDLE;
                end else timer <= timer - 1;
            end

            S_START_PULSE: begin         // SD1 시작 펄스
                nv_sd1 <= 1'b1;
                state <= S_CLK_HIGH;
            end

            S_CLK_HIGH: begin
                nv_clk <= 1'b1;
                nv_sd1 <= 1'b0;          // 1 CLK 후 SD1 해제
                state <= S_CLK_LOW;
            end

            S_CLK_LOW: begin
                nv_clk <= 1'b0;
                nv_oe <= 1'b0;           // Gate ON (현재 행)
                timer <= t_gate_on;
                row_active <= 1'b1;
                current_row <= row_idx;
                state <= S_GATE_ON;
            end

            S_GATE_ON: begin
                if (timer == 0) begin
                    nv_oe <= 1'b1;       // Gate OFF
                    row_active <= 1'b0;
                    timer <= {4'b0, t_gate_settle};
                    state <= S_GATE_SETTLE;
                end else timer <= timer - 1;
            end

            S_GATE_SETTLE: begin
                if (timer == 0) state <= S_NEXT_ROW;
                else timer <= timer - 1;
            end

            S_NEXT_ROW: begin
                if (row_idx >= n_rows - 1) begin
                    state <= S_DONE;
                    scan_done <= 1'b1;
                end else begin
                    row_idx <= row_idx + 1;
                    state <= S_CLK_HIGH;  // 다음 행 CLK
                end
            end

            S_DONE: state <= S_IDLE;
        endcase
    end
endmodule
```

#### 6.2.2 OE 타이밍 최적화

```
OE 신호 타이밍:
  OE = Low (Active): 현재 행 VGH 출력 → Gate ON
  OE = High (Inactive): 전체 VEE 출력 → Gate OFF

  T_oe_low = T_gate_on (= 게이트 ON 시간)
  T_oe_high = T_gate_settle + T_clk (= 다음 행 전환 시간)

  최적 T_oe:
    C1 (AD71124, 22µs line): T_oe_low = 15 µs, T_oe_high = 7 µs
    C2 (AD71143, 60µs line): T_oe_low = 45 µs, T_oe_high = 15 µs
    C3 (AFE2256, 20µs line): T_oe_low = 12 µs, T_oe_high = 8 µs
```

#### 6.2.3 ONA 리셋

```
ONA 리셋 시퀀스:
  ONA = Low → 전체 출력 = VGG (Gate ON all rows)
  → 모든 픽셀의 전하를 동시에 방전
  → 패널 리셋에 사용

  타이밍: T_ona = T_gate_on × 1 (1회 스캔 분량)
  반복: 3–8회 연속 ONA 리셋으로 잔류 전하 완전 제거
```

### 6.3 NT39565D 전용 알고리즘

#### 6.3.1 STV1/STV2 펄스 시퀀스

```
NT39565D 3072행 스캔 시퀀스 (Dual-STV):

  STV1  ─┐                                (Top → 하향 스캔)
           └──────────────────────────

  STV2  ──────────────────────────┐        (Bottom → 상향 스캔)
                                   └──

  CPV   ─┐ ┌─┐ ┌─┐ ┌─┐ ┌─┐ ┌─┐ ┌─     (102.4 kHz @ 30fps)
          └─┘ └─┘ └─┘ └─┘ └─┘ └─┘

  OE1   ──┐    ┌──┐    ┌──               (홀수 행)
           └────┘  └────┘

  OE2   ────┐    ┌──┐    ┌──             (짝수 행)
             └────┘  └────┘

  동시 스캔:
    STV1 → IC[0]~IC[2]: 행 0~1535 (상단 절반, 하향)
    STV2 → IC[5]~IC[3]: 행 3071~1536 (하단 절반, 상향)
    → 리드아웃 시간 50% 단축 가능 (30fps 달성 지원)
```

#### 6.3.2 CPV 클럭

```
CPV 주파수 계산:
  30 fps, 3072 rows:
    T_frame = 33.33 ms
    T_active = 30 ms (90% duty)
    T_line = 30 ms / 3072 = 9.77 µs
    f_CPV = 1/T_line = 102.4 kHz

  15 fps, 3072 rows:
    T_line = 60 ms / 3072 = 19.5 µs
    f_CPV = 51.3 kHz

  FPGA 생성: 100 MHz / 977 ≈ 102.4 kHz (오차 < 0.002%)
```

#### 6.3.3 OE1/OE2 홀짝 분리

NT39565D는 OE1과 OE2를 독립적으로 제어하여 홀수/짝수 채널을 분리할 수 있다:

```
OE1/OE2 분리 장점:
  1. 인접 행 간 크로스토크 저감 (한 번에 1행만 활성)
  2. 게이트-드레인 커플링 노이즈 저감
  3. ROI 스캔 시 특정 행 그룹만 활성화 가능

타이밍:
  Row[n] (OE1 활성):  Gate ON → Integration → SHS → Gate OFF
  Row[n+1] (OE2 활성): ........Gate ON → Integration → SHS → Gate OFF
  → 인접 행의 게이트 전환이 시간적으로 분리됨
```

### 6.4 패널 리셋 알고리즘

#### 6.4.1 다중 리셋 패스

```
패널 리셋 알고리즘:
  목적: X-ray 노출 전/후 픽셀의 잔류 전하를 완전히 제거

  ┌──────────────────────────────────────────────┐
  │ for (pass = 0; pass < N_RESET; pass++) {     │
  │   for (row = 0; row < N_ROWS; row++) {       │
  │     Gate_ON(row, T_gate_on);                 │
  │     // AFE가 전하를 흡수하여 버림              │
  │     Gate_OFF(row);                           │
  │   }                                          │
  │ }                                            │
  └──────────────────────────────────────────────┘

  N_RESET 권장값:
    정지상 촬영 전: 3–5 패스
    고선량 노출 후: 5–8 패스
    형광투시 프레임 간: 1 패스 (자동 루프)

  소요 시간 (3072 rows, 20 µs/line):
    1 패스: 3072 × 20 µs = 61.4 ms
    5 패스: 307 ms
    8 패스: 491 ms
```

#### 6.4.2 Forward Bias 방법

[Starman 2011](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/) 방식의 하드웨어 래그 저감:

```
Forward Bias 파라미터:
  인가 전압: +4 V (순방향)
  주입 주파수: 100 kHz
  주입 전하: 20 pC/diode (목표)
  픽셀당 시간: ~40 µs
  총 오버헤드: ~30 ms (행 그룹 8개씩 동시 처리)

  래그 저감 효과:
    비보정 1st frame lag: 2.4–3.7%
    FB 보정 후 1st frame lag: < 0.3%
    래그 저감률: 88–95%
```

### 6.5 양방향 스캔 (Bidirectional Scanning)

```
양방향 스캔 알고리즘:
  Frame[2k]:   행 0 → 행 N-1 (정방향, L/R=0 또는 LR=H)
  Frame[2k+1]: 행 N-1 → 행 0 (역방향, L/R=1 또는 LR=L)

  장점:
  1. 래그 비대칭 평균화: 선행 행의 래그가 후행 행에 더 많이 나타남
     → 방향 교번으로 이 비대칭을 프레임 간 평균화
  2. V_th 드리프트 대칭화: 항상 같은 방향으로 스캔 시 비대칭 누적
  3. 형광투시에서 ghost 패턴 완화

  FPGA 구현:
    scan_dir <= frame_count[0];  // LSB로 방향 교번
```

```systemverilog
// Bidirectional Scan Director
module bidir_scan_ctrl (
    input  logic clk, rst_n,
    input  logic frame_start,
    input  logic bidir_enable,      // REG 설정
    output logic scan_direction,    // 0=정방향, 1=역방향
    output logic [31:0] frame_count
);
    always_ff @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            frame_count <= '0;
            scan_direction <= 1'b0;
        end else if (frame_start) begin
            frame_count <= frame_count + 1;
            scan_direction <= bidir_enable ? frame_count[0] : 1'b0;
        end
    end
endmodule
```

### 6.6 ROI (부분 스캔) — 프레임 레이트 향상

```
ROI 스캔 알고리즘:
  전체 3072행 중 일부만 스캔하여 프레임 레이트를 높인다.

  예시: 3072×3072 패널에서 중앙 1024×1024 ROI
    스캔 행: row[1024] ~ row[2047]
    T_readout = 1024 × 9.77 µs = 10.0 ms
    가능 프레임 레이트: ~100 fps (리드아웃만 고려)

  FPGA 구현:
    REG_ROI_START = 1024
    REG_ROI_END   = 2047
    Gate IC는 ROI 시작 행까지 빠르게 스킵 (CLK만 인가, OE=High)
    ROI 행에서만 OE=Low (Gate ON) + AFE 리드아웃 실행
```

---

## 7. AFE/ROIC 최적 구동 알고리즘

### 7.1 CDS 타이밍 최적화

#### 7.1.1 SHR → INTG → SHS 시퀀스

```
CDS (Correlated Double Sampling) 시퀀스:

  IRST  ┌─────┐
  ──────┘     └──────────────────────────

  SHR        ┌┐         (리셋 레벨 샘플)
  ────────────┘└─────────────────────────

  INTG           ├── tINT ──┤   (TFT ON)
  ───────────────┐          └────────────

  SHS                        ┌┐  (신호 레벨 샘플)
  ────────────────────────────┘└──────────

  V_signal = V_SHS - V_SHR
           = (리셋노이즈 + 신호) - (리셋노이즈)
           = 순수 신호  (kTC 노이즈 상쇄)
```

**전하 전송 조건**:
```
tINT ≥ 5 × τ_TFT = 5 × R_on × (C_pixel + C_line)

조합별 최소 tINT:
  C1/C4 (AD71124): τ_TFT ≈ 4 µs → tINT ≥ 20 µs → tLINE_min = 22 µs ✓
  C2 (AD71143):    τ_TFT ≈ 4 µs → tINT ≥ 20 µs → tLINE_min = 60 µs ✓
  C3/C5 (AFE2256): τ_TFT ≈ 3 µs → tINT ≥ 15 µs → Pipeline 모드 활용
  C6/C7 (대형):    τ_TFT ≈ 9 µs → tINT ≥ 45 µs → Pipeline 필수
```

#### 7.1.2 CDS 유효 시간 최적화

CDS 간격(τ_CDS = SHS 시점 - SHR 시점)을 최소화하면 1/f 노이즈가 효과적으로 억제된다:

```
τ_CDS 최적화:
  τ_CDS → 최소화 시: 노이즈 39 e⁻ → 18.3 e⁻ (저노이즈 ROIC 연구)
  단, τ_CDS > tINT (전하 전송 시간)은 보장해야 함

  실용적 τ_CDS = tINT + margin (1–2 µs)
```

*출처: [저노이즈 ROIC 연구, 2024](https://www.researching.cn/articles/OJ161cc11982f75edb)*

### 7.2 AD71124/AD71143 구동

#### 7.2.1 ACLK 주파수 설정

```
ACLK 주파수와 라인 타임 관계:
  ADAS1256(AD71124): 내부 시퀀서가 ACLK 기반으로 동작
  
  일반적 설정: ACLK = 40 MHz (25 ns 주기)
  tLINE = 22 µs (최소): 22 µs / 25 ns = 880 ACLK 사이클
  tLINE = 60 µs (AD71143): 60 µs / 25 ns = 2400 ACLK 사이클

ACLK 분배:
  FPGA PLL 출력 → ADN4670 (1:12 LVDS 팬아웃)
  출력 스큐: < 30 ps (전형적)
  PCB 트레이스 매칭: ±5 mm 이내
```

#### 7.2.2 IFS 최적 선택 기준

```
IFS (Full-Scale) 선택 공식:
  필요 IFS = (Q_signal_max + Q_CIC) × 1.2

  Q_signal_max: 최대 예상 신호 전하
  Q_CIC: TFT 게이트 전하 주입 (≈ 0.5 pC)
  1.2: 20% 마진

  예시: Q_signal_max = 3 pC, Q_CIC = 0.5 pC
  → IFS ≥ (3 + 0.5) × 1.2 = 4.2 pC → IFS4 (4.8 pC) 선택
```

**AD71124 IFS 선택 가이드**:

| IFS 설정 | 풀스케일 범위 | 노이즈 (추정) | 적용 분야 |
|---------|------------|------------|---------|
| IFS0 | ~0.5 pC | ~300 e⁻ | 고감도 저선량 |
| IFS1 | ~1 pC | ~400 e⁻ | 일반 진단 |
| **IFS2** | **2 pC** | **560 e⁻** | **기준 최적값** |
| IFS3 | ~4 pC | ~700 e⁻ | 고선량 |
| IFS4~N | ~8–32 pC | ~800+ e⁻ | 특수 응용 |

**AFE2256 IFS 선택 가이드**:

| 코드 | 풀스케일 | 노이즈 | 적용 분야 |
|-----|---------|-------|---------|
| 000 | 0.6 pC | 최소 | 맘모그래피 |
| **001** | **1.2 pC** | **750 e⁻** | **기본 선택** |
| 010 | 2.4 pC | — | 일반 방사선 |
| 011 | 4.8 pC | — | 고선량 |
| 100 | 7.2 pC | — | 형광투시 |
| 101 | 9.6 pC | — | 최대 범위 |

#### 7.2.3 SYNC 타이밍

```
SYNC 신호 요구사항:
  펄스 최소 폭: ≥ 1 ACLK 주기 (25 ns @ 40 MHz)
  모든 AFE 간 수신 스큐: < 5 ns (PCB 매칭)
  SYNC → 적분 시작: AFE 내부 시퀀서에 의해 결정 (~수 ACLK)

SYNC 브로드캐스트 아키텍처:
  FPGA GPIO → LVDS 팬아웃 버퍼 → 12× AFE SYNC 핀
  (ADN4670 사용 시 스큐 < 30 ps)
```

### 7.3 AFE2256 구동

#### 7.3.1 MCLK 주파수 설정

```
AFE2256 MCLK 기반 타이밍:
  MCLK = 32 MHz → 1 MCLK 주기 = 31.25 ns

  적분 윈도우: 256 / 32 MHz = 8 µs ✓ (30fps 요건 충족)
  ADC 변환: 파이프라인으로 적분과 동시 실행
  LVDS 직렬 출력: MCLK 기반 내부 분주

  MCLK 분배:
    단일 TCXO (32 MHz) → LVDS 팬아웃 → 12× AFE MCLK
    스큐 요건: ±7 ns (= ±MCLK/4 @ 32 MHz)
    PCB 트레이스 길이 매칭: 170 ps/mm → 41 mm 허용 차
```

#### 7.3.2 CIC 활성화

```
CIC 활성화 절차:
  1. SPI: REG_CIC_ENABLE ← 0x01
  2. SPI: CIC_PROFILE 레지스터 × 256ch 프로그래밍
  3. 게이트 라인 ON 신호와 동기하여 반대 극성 전하 자동 주입
  4. 채널별 보상량: CIC_PROFILE[ch] (8비트, 사전 캘리브레이션)

  보상 결과:
    보상 전: 유효 다이나믹 레인지 ~50%
    보상 후: 유효 다이나믹 레인지 ~92%
```

#### 7.3.3 Pipeline 모드

```
Pipeline (IWR: Integrate-While-Read) 모드:

  비파이프라인 (ITR):
    tLINE = tINT + tREAD (직렬)
    예: tINT=30µs + tREAD=20µs = 50 µs

  파이프라인 (IWR):
    tLINE = max(tINT, tREAD) (병렬)
    예: max(30, 20) = 30 µs → 40% 향상

  PIPELINE_EN = 1 설정 시:
    뱅크 A: 적분 (현재 행)
    뱅크 B: 이전 행 결과 출력
    다음 사이클에서 A↔B 역할 교환

  주의: 첫 번째 프레임은 유효하지 않음 (프라임 프레임)
  FPGA에서 1프레임 지연 보상 필요
```

### 7.4 LPF 최적화

```
최적 LPF 차단 주파수:
  f_LPF_opt = 1 / (2π × t_INT)

  t_INT = 100 µs  → f_LPF = 1.59 kHz
  t_INT = 1 ms    → f_LPF = 159 Hz
  t_INT = 10 ms   → f_LPF = 15.9 Hz

  노이즈 대역폭: BW = f_LPF / 2 (1차 필터)

  짧은 적분: AFE 노이즈 자체는 감소하나, SNR은 입사 방사선량에 의존
  긴 적분: 다크 전류 누적이 지배적 → SNR 감소
```

### 7.5 멀티 AFE SYNC — 브로드캐스트 방식

```
12-chip AFE SYNC 브로드캐스트:

  FPGA SYNC Generator
         │
    ┌────▼────┐
    │ LVDS    │  (ADN4670 또는 CDCLVP1208)
    │ Fan-out │
    └─┬─┬─┬──┘
      │ │ │ ... (12 equal-length traces)
    AFE1 AFE2 ... AFE12

  요구사항:
    모든 AFE SYNC 수신 스큐: < 1 MCLK 주기 = 31.25 ns @ 32 MHz
    PCB 트레이스 매칭: ±7 ns (= 41 mm 길이 차)

  권장:
    LVDS 레벨 SYNC 분배 (LVCMOS보다 타이밍 정밀)
    FPGA MMCM 기반 SYNC 생성 (MCLK과 위상 동기)
```

### 7.6 조합별 AFE 구동 파라미터 비교

| 파라미터 | AD71124 (C1/C4/C6) | AD71143 (C2) | AFE2256 (C3/C5/C7) |
|---------|-------------------|-------------|-------------------|
| 클록 | ACLK (40 MHz) | ACLK (40 MHz) | MCLK (32 MHz) |
| 최소 라인 타임 | 22 µs | 60 µs | ~20 µs (pipeline) |
| IFS 범위 | 0.5–32 pC (6-bit) | 0.5–16 pC (5-bit) | 0.6–9.6 pC (6단계) |
| 대표 노이즈 | 560 e⁻ @ 2 pC | ~580 e⁻ | 240 e⁻ rms |
| CDS | 내장, 듀얼 뱅킹 | 내장 | 내장 듀얼 뱅킹 |
| CIC | 없음 | 없음 | **내장** |
| Pipeline | IWR 지원 | 제한적 | PIPELINE_EN |
| LVDS | DOUTA/DOUTB (2쌍) | DOUTA/DOUTB | DOUT ×4 (4쌍) |
| SPI | SCK/SDI/SDO/CS | SCK/SDI/SDO/CS | SCLK/SDATA/SDOUT/SEN |
| NAP 모드 | ✓ | ✓ | ✓ |

---


### 7.7 LVDS 인터페이스 상세 설계

#### 7.7.1 ADI (AD71124/AD71143) LVDS 수신

AD71124/AD71143의 LVDS 출력은 자기동기식(self-clocked) 구조이다.

```
AD71124 LVDS 출력 구조:
  ┌──────────────┐
  │   AD71124    │
  │              │──── DOUTA_P/M ──→ FPGA (256채널 중 짝수)
  │              │──── DOUTB_P/M ──→ FPGA (256채널 중 홀수)
  │              │──── DCLKH_P/M ──→ FPGA (데이터 클럭)
  │              │──── DCLKL_P/M ──→ FPGA (저속 클럭 = 프레임 동기)
  └──────────────┘

데이터 포맷:
  - 16-bit straight binary, MSB first
  - 256채널 × 16bit = 4096 bits per row
  - DOUTA: ch[0], ch[2], ch[4], ... ch[254] (128채널)
  - DOUTB: ch[1], ch[3], ch[5], ... ch[255] (128채널)
  - 각 LVDS 페어에 128 × 16 = 2048 bits 직렬 전송
```

#### 7.7.2 TI (AFE2256) LVDS 수신

```
AFE2256 LVDS 출력 구조:
  ┌──────────────┐
  │   AFE2256    │
  │              │──── DOUT_P/M [3:0] ──→ FPGA (4-lane LVDS)
  │              │──── DCLK_P/M       ──→ FPGA (데이터 클럭)
  │              │──── FCLK_P/M       ──→ FPGA (프레임 클럭)
  └──────────────┘

데이터 포맷:
  - 내부 4개 SAR ADC가 256채널을 4:1 MUX로 분배
  - 각 ADC → 64채널 × 16bit = 1024 bits
  - 4개 LVDS lane에 동시 출력 → 총 4096 bits per row
  - DCLK 기반 DDR 전송 시 유효 비트레이트 = 2 × DCLK
```

#### 7.7.3 FPGA LVDS 수신기 SystemVerilog 구현

```systemverilog
// lvds_deserializer.sv — 범용 LVDS 데이터 역직렬화 모듈
module lvds_deserializer #(
    parameter DATA_WIDTH  = 16,        // 픽셀 당 비트 수
    parameter N_CHANNELS  = 128,       // LVDS 레인 당 채널 수
    parameter DDR_MODE    = 0          // 0=SDR, 1=DDR
)(
    input  logic              clk_sys,       // 시스템 클럭
    input  logic              rst_n,
    // LVDS 입력 (IBUFDS 후 싱글엔드)
    input  logic              lvds_dclk,     // 데이터 클럭
    input  logic              lvds_data,     // 직렬 데이터
    input  logic              lvds_fclk,     // 프레임 클럭 (옵션)
    // 역직렬화 출력
    output logic [DATA_WIDTH-1:0] pixel_data,
    output logic              pixel_valid,
    output logic [7:0]        channel_idx
);

    logic [DATA_WIDTH-1:0] shift_reg;
    logic [3:0]            bit_cnt;
    logic [7:0]            ch_cnt;

    // ISERDES2 인스턴스 (Xilinx 7-series / UltraScale)
    // 실제 구현 시 ISERDES2 프리미티브 사용
    always_ff @(posedge lvds_dclk or negedge rst_n) begin
        if (!rst_n) begin
            shift_reg <= '0;
            bit_cnt   <= '0;
            ch_cnt    <= '0;
        end else begin
            shift_reg <= {shift_reg[DATA_WIDTH-2:0], lvds_data};
            if (bit_cnt == DATA_WIDTH - 1) begin
                bit_cnt <= '0;
                ch_cnt  <= (ch_cnt == N_CHANNELS - 1) ? '0 : ch_cnt + 1;
            end else begin
                bit_cnt <= bit_cnt + 1;
            end
        end
    end

    assign pixel_data  = shift_reg;
    assign pixel_valid = (bit_cnt == DATA_WIDTH - 1);
    assign channel_idx = ch_cnt;

endmodule
```

#### 7.7.4 Xilinx IDELAY2/ISERDES2 활용 (UltraScale)

대형 패널(C6/C7)에서 12개 AFE의 LVDS 데이터를 수신할 때, 각 AFE의 DCLK 위상이 미세하게 다를 수 있다. IDELAY2를 사용한 per-lane 위상 보정이 필수적이다.

```
IDELAY2 설정:
  - 모드: VAR_LOAD (가변 딜레이)
  - 탭 수: 512 탭 (UltraScale+)
  - 탭 해상도: ~2.5 ps/tap
  - 총 딜레이 범위: ~1.28 ns
  
ISERDES2 설정:
  - 모드: MEMORY (DDR) 또는 NETWORKING (SDR)
  - 직렬화 비율: 8:1 또는 10:1
  - 비트슬립: 자동 정렬 (training pattern 사용)

위상 캘리브레이션 절차:
  1. AFE에 training pattern 전송 요청 (SPI 커맨드)
  2. IDELAY2 탭을 0에서 511까지 스윕
  3. 각 탭에서 training pattern 일치 여부 확인
  4. Eye diagram의 중심 탭 값 결정
  5. 최적 탭 값을 레지스터에 저장
  6. 정상 동작 모드 전환
```

### 7.8 채널 간 미스매치 보정

#### 7.8.1 AFE 채널 오프셋/게인 편차

| AFE | 채널 오프셋 편차 (typ) | 채널 게인 편차 (typ) | 보정 방법 |
|-----|----------------------|--------------------|-----------| 
| AD71124 | ±200 LSB | ±0.5% | 외부 오프셋/게인 캘리브레이션 |
| AD71143 | ±150 LSB | ±0.4% | 외부 오프셋/게인 캘리브레이션 |
| AFE2256 | ±100 LSB | ±0.3% | 내장 디지털 오프셋 보정 + 외부 게인 보정 |

#### 7.8.2 멀티-AFE 칩 간 오프셋 계단 (C6/C7)

12개 AFE 칩의 경계에서 256채널 단위로 오프셋 계단(step)이 발생한다.

```
칩 간 오프셋 계단 보정 절차:

  1. 균일 조사(Flat Field) 상태에서 전체 프레임 캡처
  2. 각 AFE 칩의 256채널 평균값 계산: M_chip[k] (k=0..11)
  3. 전체 평균: M_global = Σ M_chip[k] / 12
  4. 칩별 보정 오프셋: ΔM[k] = M_global - M_chip[k]
  5. 실시간 보정: pixel_out = pixel_raw + ΔM[chip_idx]
  
  칩 경계 전이 특성:
    ┌──────────────────────────────────────────────────┐
    │       AFE_0        │       AFE_1        │       │
    │ ch0 ─────── ch255  │ ch256 ──── ch511   │  ...  │
    │    ▲ +ΔM[0]        │    ▲ +ΔM[1]        │       │
    │    원래 = 계단 형태 │                     │       │
    └──────────────────────────────────────────────────┘
    보정 후: 균일한 오프셋 레벨
```

### 7.9 AFE 노이즈 분석 및 최적화

#### 7.9.1 노이즈 소스 분해

| 노이즈 소스 | AD71124 (e⁻ rms) | AFE2256 (e⁻ rms) | 비고 |
|-------------|-------------------|-------------------|------|
| kTC 리셋 노이즈 | ~360 | ~150 | CDS로 상쇄 |
| AFE 읽기 노이즈 | 560 | 240 | 데이터시트 대표값 |
| 양자화 노이즈 (16bit) | ~50 | ~50 | LSB/√12 |
| 다크 전류 샷 노이즈 | 가변 | 가변 | √(I_dark × t_int) |
| TFT 열잡음 | ~100 | ~100 | kT/C 기여분 |
| **총 읽기 노이즈 (RSS)** | **~585** | **~280** | CDS 후 |

#### 7.9.2 조합별 DQE 영향 분석

```
DQE(f) = G² × MTF²(f) / [G × MTF²(f) × NPS_X(f) + NPS_elec(f)]

여기서:
  G = 변환 이득 (e⁻/X-ray)
  MTF(f) = 공간 주파수 f에서의 변조 전달 함수
  NPS_X(f) = X-ray 양자 노이즈 스펙트럼
  NPS_elec(f) = 전자 노이즈 스펙트럼

저선량 극한 (읽기 노이즈 지배):
  DQE ∝ 1 / σ_read²
  
  AD71124 (560 e⁻) → DQE_rel = 1.0 (기준)
  AFE2256 (240 e⁻) → DQE_rel = (560/240)² = 5.4 (5.4배 DQE 개선)
  
  → 저선량 맘모그래피/소아과 응용에서 AFE2256 선택 당위성
```

---

## 8. 영상 획득 시퀀스 (Acquisition Sequences)

### 8.1 정지상 획득 (Radiography Mode)

#### 8.1.1 전체 플로우

```
┌──────────────────────────────────────────────────────────────────────┐
│                정지상 획득 시퀀스 (Static Radiography)                 │
│                                                                      │
│  MCU                FPGA               Gate IC          AFE          │
│   │                  │                    │               │          │
│   │─ MODE=STATIC ──▶│                    │               │          │
│   │─ TINTEG 설정 ──▶│                    │               │          │
│   │─ START ────────▶│                    │               │          │
│   │                  │                    │               │          │
│   │         ┌────────┼── Pre-Reset (3-8 pass) ──────────▶│          │
│   │         │        │   ONA=L (전체 VGH)  │               │          │
│   │         │        │   ...반복...        │               │          │
│   │         │        │   ONA=H (복귀)      │               │          │
│   │         └────────┤                    │               │          │
│   │                  │                    │               │          │
│   │     ┌────────────┼── Prep-Request ───▶ X-ray Gen     │          │
│   │     │            │   (TRIG_OUT 핀)    │               │          │
│   │     │            │                    │               │          │
│   │  X-ray│          │── INTEGRATE ──────▶│ Gate OFF      │          │
│   │  조사 │          │   (T_INTEG 대기)   │ (전체 VEE)    │          │
│   │     │            │                    │               │          │
│   │     │   X-ray    │                    │               │          │
│   │     └──Enable────│                    │               │          │
│   │                  │                    │               │          │
│   │                  │── READOUT ────────▶│               │          │
│   │                  │   row=0:           │ CLK+OE        │          │
│   │                  │     Gate ON ───────▶│ Row0 VGH     │          │
│   │                  │     SYNC ──────────────────────────▶│ CDS     │
│   │                  │     (T_LINE)       │               │ ADC     │
│   │                  │   ◀─ LVDS ─────────────────────────│ DOUT    │
│   │                  │     → BufRAM       │               │          │
│   │                  │   row=1...N-1      │               │          │
│   │                  │                    │               │          │
│   │                  │── DONE ───────────▶│               │          │
│   │◀── IRQ ──────────│   STATUS[DONE]=1   │               │          │
│   │                  │                    │               │          │
└──────────────────────────────────────────────────────────────────────┘
```

#### 8.1.2 Pre-exposure Reset

```
Pre-exposure 리셋 상세:
  목적: 이전 노출의 잔류 전하를 완전 제거
  방법: 전체 행 순차 스캔 × N 회

  권장 횟수:
    일반 정지상: N_reset = 3
    이전 고선량 노출 후: N_reset = 5–8
    형광투시 → 정지상 전환: N_reset = 5

  소요 시간 (R1717, 2048 rows, 22 µs/line):
    1 pass: 2048 × 22 µs = 45 ms
    3 pass: 135 ms
    5 pass: 225 ms
```

#### 8.1.3 X-ray 트리거 동기화

```
핸드셰이크 프로토콜:
  FPGA → Generator: PREP_REQUEST (준비 요청)
  Generator → FPGA: X_RAY_READY (준비 완료)
  FPGA → Generator: X_RAY_ENABLE (조사 허가)
  Generator → FPGA: EXPOSURE_DONE (조사 완료)

  FPGA 타이밍:
    1. Pre-reset 완료 → PREP_REQUEST 어시트
    2. X_RAY_READY 수신 대기 (타임아웃: 5초)
    3. X_RAY_ENABLE 어시트 → INTEGRATE 상태 진입
    4. T_INTEG 카운트 또는 EXPOSURE_DONE 수신 (먼저 도달 시)
    5. READOUT 상태 진입
```

```systemverilog
// X-ray Trigger Handshake FSM
module xray_trigger_ctrl #(
    parameter TIMEOUT_CNT = 32'd500_000_000  // 5초 @ 100MHz
)(
    input  logic clk, rst_n,
    input  logic trigger_start,
    input  logic xray_ready,            // Generator → FPGA
    input  logic exposure_done,          // Generator → FPGA
    input  logic [23:0] t_integ,         // 적분 시간 (10ns 단위)
    output logic prep_request,           // FPGA → Generator
    output logic xray_enable,            // FPGA → Generator
    output logic integrate_active,       // 적분 중 표시
    output logic readout_start,          // 리드아웃 시작 트리거
    output logic timeout_error
);
    typedef enum logic [2:0] {
        S_IDLE, S_PREP, S_WAIT_READY, S_EXPOSE, S_INTEGRATE, S_DONE
    } state_t;
    state_t state;
    logic [31:0] timer;

    always_ff @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            state <= S_IDLE;
            {prep_request, xray_enable, integrate_active} <= '0;
            readout_start <= 1'b0;
            timeout_error <= 1'b0;
        end else case (state)
            S_IDLE: if (trigger_start) begin
                prep_request <= 1'b1;
                timer <= TIMEOUT_CNT;
                state <= S_WAIT_READY;
            end
            S_WAIT_READY: begin
                if (xray_ready) begin
                    xray_enable <= 1'b1;
                    integrate_active <= 1'b1;
                    timer <= {8'b0, t_integ};
                    state <= S_INTEGRATE;
                end else if (timer == 0) begin
                    timeout_error <= 1'b1;
                    state <= S_IDLE;
                end else timer <= timer - 1;
            end
            S_INTEGRATE: begin
                if (exposure_done || timer == 0) begin
                    xray_enable <= 1'b0;
                    integrate_active <= 1'b0;
                    readout_start <= 1'b1;
                    state <= S_DONE;
                end else timer <= timer - 1;
            end
            S_DONE: begin
                readout_start <= 1'b0;
                prep_request <= 1'b0;
                state <= S_IDLE;
            end
        endcase
    end
endmodule
```

### 8.2 다크 프레임 획득 (Dark Frame Mode)

```
다크 프레임 획득 프로토콜:
  1. X-ray 소스 완전 차단 (셔터 닫힘 또는 전원 OFF)
  2. N 프레임 연속 획득 (동일 적분 시간, 동일 게인 모드)
  3. 픽셀별 평균화 → 다크 맵 생성

  권장 N 값:
    빠른 캘리브레이션: N = 16 (4× 노이즈 저감)
    표준 캘리브레이션: N = 64 (8× 노이즈 저감, 권장)
    고정밀 캘리브레이션: N = 256 (16× 노이즈 저감)

  노이즈 저감 효과:
    σ_dark_map = σ_single / √N
    N=64: σ → σ/8 = 12.5% 잔류

  메타데이터 기록 (필수):
    - 적분 시간 (T_INTEG)
    - 패널 온도 (T_panel)
    - AFE 게인 모드 (IFS 코드)
    - 전력 모드
    - 획득 시각

  주기적 업데이트 조건:
    - ΔT > 2°C (온도 변화)
    - 1시간 경과
    - 모드 전환 후
    - 이전 고선량 노출 30초 경과 후
```

### 8.3 플랫 필드 획득 (Flat Field Mode)

```
플랫 필드 획득 프로토콜:
  1. X-ray 경로에서 모든 물체 제거 (균일 조사 조건)
  2. 빔 퀄리티: 임상 사용과 동일 (예: RQA-5: 74 kVp)
  3. 선량: 선형 범위 내 (포화 근처 회피)
  4. N 프레임 획득 (N = 32–64 권장)

  게인 맵 계산:
    F_avg(i,j) = (1/N) × Σ F_n(i,j)       // 평균
    F_corr(i,j) = F_avg(i,j) - D_map(i,j)  // 다크 보정
    M = mean(F_corr)                         // 전체 평균
    G(i,j) = M / F_corr(i,j)                // 정규화 게인 맵

  픽셀 간 게인 변동:
    전형적 변동: ±10–20%
    원인: 신틸레이터 두께 변동(±5%), 포토다이오드 QE(±3–5%),
          리드아웃 앰프 게인(±2–5%)
    보정 후 잔류: < 0.5–1%

  메모리 요구:
    3072×3072 × 16bit = 18.9 MB per map (dark + gain = 37.8 MB)
```

### 8.4 형광투시 연속 모드 (Fluoroscopy Mode)

#### 8.4.1 DONE → RESET 자동 루프

```
형광투시 연속 모드 FSM:

  ┌─────────────────────────────────────────────────────────────┐
  │  REG_MODE = CONTINUOUS                                      │
  │                                                             │
  │  ACQUIRE ─┬─ RESET (1 pass) ─── INTEGRATE ─── READOUT ──┐ │
  │           │                                               │ │
  │           └───────────────────────────────────────────────┘ │
  │           (자동 반복, MCU ABORT까지)                         │
  │                                                             │
  │  프레임 레이트 = 1 / (T_RESET + T_INTEGRATE + T_READOUT)   │
  └─────────────────────────────────────────────────────────────┘
```

#### 8.4.2 30fps 달성 조건 (3072 패널)

```
30fps 타이밍 예산 (X239AW1-102):
  T_frame = 33.33 ms
  T_reset = 0 (Rolling Reset 사용 시)
  T_vertical_blank = 3.33 ms (10%)
  T_readout = 30.0 ms
  T_line = 30.0 ms / 3072 = 9.77 µs

  AFE2256 Pipeline 모드 필수:
    tINT = 8 µs (MCLK = 32 MHz, 256 채널)
    tREAD = 파이프라인으로 tINT와 병렬
    T_line = 8 µs + overhead ≈ 9.77 µs ✓

  NT39565D CPV: 102.4 kHz (= 1/9.77 µs)
```

#### 8.4.3 Rolling Reset

```
Rolling Reset 개념:
  기존 방식: 전체 리셋 (모든 행) → 전체 적분 → 전체 리드아웃
  Rolling 방식: Row[n] 리셋 → Row[n] 적분 → Row[n] 리드아웃 (행별 파이프라인)

  타이밍:
    Row[n]:   [RESET][INTEGRATE][READOUT]
    Row[n+1]:        [RESET][INTEGRATE][READOUT]
    Row[n+2]:               [RESET][INTEGRATE][READOUT]

  장점:
    - 별도의 전체 리셋 시간 불필요
    - 프레임 간 데드 타임 제거
    - 연속 프레임 레이트 최대화

  주의:
    - 행별 적분 시작 시각이 다름 → Rolling Shutter 아티팩트 가능
    - X-ray 펄스 동기화 필요 (모든 행의 적분 윈도우 내에 펄스 포함)
```

### 8.5 트리거 모드 (Triggered Mode)

```
트리거 모드 동작:
  REG_MODE = TRIGGERED

  1. FPGA는 IDLE 상태에서 X-ray 트리거 입력 모니터링
  2. 트리거 감지 (상승 에지 또는 하강 에지, REG 설정 가능)
  3. 디바운싱: 10 µs 최소 펄스 폭 필터링
  4. 노출 윈도우 관리:
     a. 트리거 시작 → 즉시 INTEGRATE 진입
     b. T_INTEG 타이머 시작 (또는 두 번째 트리거 대기)
     c. 트리거 종료 또는 T_INTEG 만료 → READOUT 진입

  FPGA 입력:
    TRIG_IN (GPIO, Schmitt trigger 입력 권장)
    레벨: 3.3V LVCMOS 또는 5V LVTTL (레벨 변환)

  타이밍 정확도: ±1 FPGA 클록 (10 ns @ 100 MHz)
```

```systemverilog
// Trigger Input Debouncer
module trigger_debounce #(
    parameter DEBOUNCE_CNT = 1000  // 10 µs @ 100 MHz
)(
    input  logic clk, rst_n,
    input  logic trig_in_raw,
    output logic trig_rising,
    output logic trig_falling
);
    logic [9:0] cnt;
    logic trig_stable, trig_prev;

    always_ff @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            cnt <= '0;
            trig_stable <= 1'b0;
            trig_prev <= 1'b0;
        end else begin
            trig_prev <= trig_stable;
            if (trig_in_raw != trig_stable) begin
                if (cnt >= DEBOUNCE_CNT)
                    trig_stable <= trig_in_raw;
                else
                    cnt <= cnt + 1;
            end else cnt <= '0;
        end
    end

    assign trig_rising  = trig_stable & ~trig_prev;
    assign trig_falling = ~trig_stable & trig_prev;
endmodule
```

---


### 8.6 CBCT (Cone-Beam CT) 연속 획득 모드

CBCT에서는 갠트리 회전 중 수백~수천 프레임을 연속 획득한다. 래그 히스토리가 복잡해지므로 특별한 제어가 필요하다.

#### 8.6.1 CBCT 시퀀스 플로우

```
CBCT 획득 시퀀스:
═══════════════════════════════════════════════════════════════════

  Phase 1: Warm-up (100 dummy frames)
  ├── Gate scan @ 15 fps, X-ray OFF
  ├── 트랩 상태 안정화
  └── 다크 프레임 N=64 캡처 (오프셋 맵 업데이트)

  Phase 2: Dark Reference (20 frames)
  ├── X-ray OFF, 정상 리드아웃
  ├── 실시간 다크 레벨 모니터링
  └── 이상값 검출 → 경고 플래그

  Phase 3: Acquisition (360–720 projections)
  ├── X-ray ON, 연속 트리거 모드
  ├── 프레임 레이트: 15–30 fps
  ├── 각 프레임: 리셋→적분→리드아웃 자동 반복
  ├── Forward Bias 적용 (옵션): 프레임 간 20 pC/pixel 주입
  ├── DDR4 트리플 버퍼 사용 (DMA→호스트 전송 병행)
  └── 래그 보정 파이프라인 실시간 처리

  Phase 4: Post-Acquisition
  ├── X-ray OFF
  ├── 추가 래그 프레임 N=20 캡처 (잔류 래그 측정)
  └── 패널 상태 IDLE 복귀
```

#### 8.6.2 CBCT 래그 아티팩트 — Radar Artifact

```
Radar Artifact 메커니즘:
  
  CBCT 갠트리 회전 중:
  - 인체의 밀도 높은 부위(뼈, 금속 임플란트)가 특정 프로젝션에서
    높은 선량을 유발
  - 해당 픽셀에 트랩 전하가 과도 축적
  - 이후 프로젝션에서 고스트 신호로 나타남
  - 3D 재구성 시 방사형 shading artifact 형성

  정량적 영향:
    미보정: 골반 팬텀에서 20–35 HU shading ([Starman 2012](https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/))
    Forward Bias 보정 후: 7 HU 수준으로 감소 (−81%)
    최대 artifact: 51 HU (금속 임플란트 근처)
```

#### 8.6.3 CBCT용 프레임 타이밍 예산

| 파라미터 | 15 fps | 30 fps | 비고 |
|----------|--------|--------|------|
| 프레임 주기 | 66.7 ms | 33.3 ms | — |
| 리셋 시간 | 5 ms | 2 ms | 다중 리셋 1–2회 |
| X-ray 노출 | 30 ms | 15 ms | kV 조건 의존 |
| 리드아웃 | 25 ms | 12 ms | 3072행 × T_line |
| FB 오버헤드 | 5 ms | 3 ms | Forward Bias 적용 시 |
| 가용 마진 | 1.7 ms | 1.3 ms | — |

### 8.7 이중 이득(Dual-Gain) 모드

넓은 다이나믹 레인지가 필요한 응용(예: 흉부 방사선)에서 이중 이득 모드를 사용한다.

#### 8.7.1 동작 원리

```
이중 이득 모드 시퀀스:

  동일 노출에 대해 두 가지 이득으로 두 번 리드아웃:

  Step 1: 고이득(High Gain) 리드아웃
    - IFS = 1 pC (고감도, 저노이즈)
    - 저선량 영역 최적화
    - 고선량 영역 포화 가능

  Step 2: 저이득(Low Gain) 리드아웃
    - IFS = 8 pC (저감도, 넓은 범위)
    - 고선량 영역 최적화
    - 저선량 영역 노이즈 지배

  Step 3: 합성 (FPGA 또는 호스트)
    - 픽셀별 자동 선택:
      IF (HG_pixel < HG_THRESHOLD)
        output = HG_pixel × HG_gain
      ELSE
        output = LG_pixel × LG_gain
    - 합성 다이나믹 레인지: 166.7:1 (vs. 단일 76.9:1)
```

#### 8.7.2 이중 이득 SystemVerilog 합성 모듈

```systemverilog
// dual_gain_combiner.sv — 이중 이득 프레임 합성
module dual_gain_combiner #(
    parameter HG_THRESHOLD = 16'd58000,  // 고이득 포화 임계값 (~89% FS)
    parameter HG_GAIN_Q12  = 16'd4096,   // 고이득 정규화 계수 (Q12)
    parameter LG_GAIN_Q12  = 16'd16384   // 저이득 정규화 계수 (Q12)
)(
    input  logic        clk,
    input  logic [15:0] hg_pixel,     // 고이득 픽셀값
    input  logic [15:0] lg_pixel,     // 저이득 픽셀값
    input  logic        pixel_valid,
    output logic [15:0] combined,     // 합성 출력
    output logic        combined_valid,
    output logic        gain_select   // 0=HG, 1=LG (디버그용)
);

    logic [31:0] hg_scaled, lg_scaled;

    always_ff @(posedge clk) begin
        if (pixel_valid) begin
            hg_scaled <= hg_pixel * HG_GAIN_Q12;
            lg_scaled <= lg_pixel * LG_GAIN_Q12;

            if (hg_pixel < HG_THRESHOLD) begin
                combined <= hg_scaled[27:12]; // Q12 → Q0
                gain_select <= 1'b0;
            end else begin
                combined <= lg_scaled[27:12];
                gain_select <= 1'b1;
            end
            combined_valid <= 1'b1;
        end else begin
            combined_valid <= 1'b0;
        end
    end

endmodule
```

### 8.8 X-ray 선원 동기화 상세

#### 8.8.1 외부 트리거 신호 처리

```
X-ray 제너레이터 인터페이스:

  신호명       방향      레벨        설명
  ──────────────────────────────────────────────────────
  PREP_IN      입력      3.3V CMOS   준비(Prep) 신호 — 고전압 인가 대기
  XRAY_IN      입력      3.3V CMOS   X-ray ON 동기 신호
  XRAY_REQ     출력      3.3V CMOS   X-ray 조사 요청 (FPGA → Generator)
  SYNC_OUT     출력      LVDS        프레임 동기 출력 (진단 장비용)
  DOSE_STOP    출력      3.3V CMOS   선량 제한 도달 시 즉시 중단

타이밍 시퀀스:
  ┌───────────────────────────────────────────────────────┐
  │ FPGA                                                  │
  │  1. 리셋 완료 확인                                     │
  │  2. XRAY_REQ = HIGH (X-ray 요청)                      │
  │  3. PREP_IN 상승 에지 대기 (Generator 고전압 충전)      │
  │  4. XRAY_IN 상승 에지 수신 → T_integ 카운터 시작       │
  │  5. T_integ 만료 OR DOSE_STOP 조건 → XRAY_REQ = LOW  │
  │  6. XRAY_IN 하강 에지 확인 → 리드아웃 시작             │
  └───────────────────────────────────────────────────────┘
```

#### 8.8.2 과노출 보호 타이머

```systemverilog
// xray_safety_timer.sv — 과노출 안전 타이머
module xray_safety_timer #(
    parameter SYS_CLK_HZ   = 100_000_000,
    parameter MAX_EXPOSURE_MS = 5000     // 최대 5초 (안전 제한)
)(
    input  logic        clk,
    input  logic        rst_n,
    input  logic        xray_active,      // X-ray 조사 중
    input  logic [15:0] max_integ_ms,     // 설정된 최대 적분 시간 (ms)
    output logic        timeout_flag,     // 타임아웃 발생
    output logic        force_gate_off    // 강제 Gate OFF
);

    localparam CLK_PER_MS = SYS_CLK_HZ / 1000;
    logic [31:0] timer_cnt;
    logic [31:0] limit;

    assign limit = (max_integ_ms < MAX_EXPOSURE_MS) ?
                   max_integ_ms * CLK_PER_MS :
                   MAX_EXPOSURE_MS * CLK_PER_MS;

    always_ff @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            timer_cnt     <= '0;
            timeout_flag  <= 1'b0;
            force_gate_off <= 1'b0;
        end else if (xray_active) begin
            timer_cnt <= timer_cnt + 1;
            if (timer_cnt >= limit) begin
                timeout_flag  <= 1'b1;
                force_gate_off <= 1'b1;
            end
        end else begin
            timer_cnt     <= '0;
            timeout_flag  <= 1'b0;
            force_gate_off <= 1'b0;
        end
    end

endmodule
```

### 8.9 비닝(Binning) 모드 — 고프레임 레이트 지원

#### 8.9.1 비닝 모드 개요

2×2 또는 4×4 비닝으로 해상도를 낮추고 프레임 레이트를 높인다.

| 비닝 모드 | 유효 해상도 | 프레임 레이트 향상 | SNR 향상 | 용도 |
|----------|-----------|------------------|---------|------|
| 1×1 (없음) | 3072×3072 | 1× (기준) | 1× | 정지상 |
| 2×2 | 1536×1536 | ~4× | 2× | 형광투시 |
| 4×4 | 768×768 | ~16× | 4× | 고속 형광투시 |

#### 8.9.2 FPGA 비닝 구현

```
2×2 비닝 구현 방법:

  방법 A: Gate IC 레벨 비닝 (하드웨어)
    - 인접 2개 Gate 라인 동시 ON
    - 전하가 합산되어 데이터 라인으로 전송
    - Gate IC에서 지원 필요 (NT39565D: OE1/OE2 분리로 가능)
    - 행 방향 2× 비닝 구현

  방법 B: AFE 레벨 비닝 (소프트웨어/FPGA)
    - 인접 2개 컬럼 디지털 합산
    - FPGA에서 리드아웃 후 실시간 합산
    - 열 방향 2× 비닝 구현

  방법 A + B 결합 → 2×2 비닝 완성

FPGA 비닝 합산기:
  pixel_binned = pixel[r][c] + pixel[r][c+1]
               + pixel[r+1][c] + pixel[r+1][c+1]
  
  또는 평균: pixel_avg = pixel_binned >> 2
```

---

## 9. 이미지 래그(Lag) 보정 알고리즘

### 9.1 래그 물리와 정량적 수치

a-Si:H FPD의 이미지 래그는 포토다이오드 내 전하 트래핑에 의해 발생하며, N=4 지수 성분으로 모델링된다.

**래그 감쇠 함수 (FSRF)**:

```
S_lag(t) = Σ_{n=1}^{4} b_n × S_0 × exp(-a_n × t)

표준 파라미터 (Varian 4030CB, 27% 포화, 15 fps):

  ┌─────────┬──────────────┬──────────────┬────────────────┐
  │ 성분    │ a_n (fr⁻¹)   │ b_n          │ τ_n (초)       │
  ├─────────┼──────────────┼──────────────┼────────────────┤
  │ n=1     │ 2.5×10⁻³     │ 7.1×10⁻⁶    │ 26.7 s         │
  │ n=2     │ 2.1×10⁻²     │ 1.1×10⁻⁴    │ 3.2 s          │
  │ n=3     │ 1.6×10⁻¹     │ 1.7×10⁻³    │ 0.4 s (400 ms) │
  │ n=4     │ 7.6×10⁻¹     │ 1.8×10⁻²    │ 87 ms          │
  └─────────┴──────────────┴──────────────┴────────────────┘
```

*출처: [Starman et al., Medical Physics 2012](https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/)*

### 9.2 하드웨어 방법 — Forward Bias

```
Forward Bias 래그 저감 파라미터:
  ┌──────────────────────────────────────────────────────┐
  │ 순방향 바이어스 전압: +4 V                            │
  │ 주입 주파수: 100 kHz                                  │
  │ 주입 전하: 20 pC/diode (>= 5.8 pC로 95% 저감)       │
  │ 픽셀당 시간: ~40 µs                                   │
  │ 행 그룹 크기: 8행 동시 (전류 앰프 제한)               │
  │ 총 오버헤드: ~30 ms/frame                             │
  └──────────────────────────────────────────────────────┘

  성능:
  ┌──────────────────┬──────────┬────────────┐
  │ 모드             │ 2nd 래그  │ 100th 래그  │
  ├──────────────────┼──────────┼────────────┤
  │ Standard         │ 355 cnt  │ 44 cnt     │
  │ Forward Bias     │ 42 cnt   │ 13 cnt     │
  │ 저감률           │ -88%     │ -70%       │
  └──────────────────┴──────────┴────────────┘
```

### 9.3 LTI 재귀 필터 (Hsieh 알고리즘)

#### 9.3.1 알고리즘 수식

```
보정 출력 (래그 프리 신호 추정):
  x̂(k) = [y(k) - Σ_{n=1}^{N} b_n × S_{n,k} × exp(-a_n)] / [Σ_{n=0}^{N} b_n]

상태 변수 업데이트:
  S_{n,k+1} = x̂(k) + S_{n,k} × exp(-a_n)

초기 조건: S_{n,1} = 0

특성:
  - 현재 프레임 y(k)와 N개 상태 변수만 필요
  - 이력 버퍼 불필요 (O(N) 메모리/픽셀)
  - N=4 성분: 8회 곱셈 + 4회 덧셈/픽셀
```

*출처: [Hsieh US5249123A](https://patents.google.com/patent/US5249123A); [Starman 2012](https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/)*

#### 9.3.2 고정소수점 FPGA 구현

```systemverilog
// LTI Recursive Lag Correction (N=4 components)
// 고정소수점: Q16 (16비트 소수부)
module lag_correction_lti #(
    parameter N_COMP = 4,
    parameter DATA_W = 16,
    parameter FRAC_W = 16  // Q16 fixed-point
)(
    input  logic clk, rst_n,
    input  logic pixel_valid,
    input  logic frame_start,
    input  logic [DATA_W-1:0] raw_pixel,        // y(k)

    // 사전 계산된 계수 (Q16)
    input  logic [FRAC_W-1:0] coeff_b [N_COMP],  // b_n
    input  logic [FRAC_W-1:0] coeff_exp [N_COMP], // exp(-a_n)
    input  logic [FRAC_W-1:0] norm_factor,        // 1/Σb_n

    output logic [DATA_W-1:0] corrected_pixel,   // x̂(k)
    output logic corrected_valid
);
    // 상태 변수 (BRAM에 저장)
    // S[n][row][col] — 프레임 간 유지
    logic [DATA_W+FRAC_W-1:0] state_var [N_COMP]; // 현재 픽셀의 상태

    logic [DATA_W+FRAC_W-1:0] lag_estimate;
    logic [DATA_W+FRAC_W-1:0] x_hat;

    always_ff @(posedge clk) begin
        if (frame_start) begin
            // 프레임 시작 시 BRAM에서 상태 로드 (별도 주소 제어)
        end

        if (pixel_valid) begin
            // Step 1: 래그 추정치 계산
            lag_estimate = 0;
            for (int n = 0; n < N_COMP; n++) begin
                lag_estimate = lag_estimate +
                    ((coeff_b[n] * state_var[n]) >> FRAC_W) *
                    coeff_exp[n];
            end
            lag_estimate = lag_estimate >> FRAC_W;

            // Step 2: 보정 출력
            x_hat = ({raw_pixel, {FRAC_W{1'b0}}} - lag_estimate) *
                    norm_factor;
            x_hat = x_hat >> FRAC_W;

            corrected_pixel = x_hat[DATA_W-1:0];
            corrected_valid = 1'b1;

            // Step 3: 상태 변수 업데이트
            for (int n = 0; n < N_COMP; n++) begin
                state_var[n] = x_hat + 
                    (state_var[n] * coeff_exp[n]) >> FRAC_W;
            end
            // → BRAM에 상태 저장 (별도 주소 제어)
        end
    end
endmodule
```

#### 9.3.3 시간 상수값 테이블 (Q16 변환)

| 성분 | τ (초) | a_n (fr⁻¹) | exp(-a_n) | Q16 값 | b_n | Q16 값 |
|------|--------|-----------|-----------|--------|-----|--------|
| n=1 | 26.7 | 0.0025 | 0.99750 | 65,372 | 7.1e-6 | 0 |
| n=2 | 3.2 | 0.021 | 0.97921 | 64,173 | 1.1e-4 | 7 |
| n=3 | 0.4 | 0.16 | 0.85214 | 55,837 | 1.7e-3 | 111 |
| n=4 | 0.087 | 0.76 | 0.46767 | 30,652 | 1.8e-2 | 1,180 |

### 9.4 NLCSC (비선형 보정)

```
NLCSC 알고리즘 요점:
  LTI 모델의 한계: 래그 계수가 노출량에 의존 (비선형)
  
  노출량 의존 임펄스 응답:
    h(k, x_k) = b₀(x_k)·δ(k) + Σ b_n(x_k) × exp(-a_n(x_k) × k)

  노출량 의존 래그 레이트:
    a_n(x) = a_{1,n} + c₁ × (1 - exp(-c₂ × x))

  캘리브레이션:
    1. 기본 래그 레이트: 27% 포화에서 FSRF 피팅 (4개 성분)
    2. 최대 저장 전하 Q_n(x): 9개 노출 레벨에서 FSRF 적분
    3. 노출 의존 레이트: 전역 탐색 최적화

  성능 (2% 포화 노출):
    LTI: 1st frame 잔류 1.4%
    NLCSC: 1st frame 잔류 0.25% (5.6× 개선)

  FPGA 구현:
    x_k 룩업 테이블 (256 entry): a_n(x), b_n(x) 사전 계산
    추가 리소스: ~500 LUT, 1 BRAM (LUT 저장)
```

*출처: [Starman et al., Medical Physics 2012](https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/)*

### 9.5 FPGA 구현 리소스 추정

| 리소스 | LTI (N=4) | NLCSC | AR(1) 간이 |
|--------|----------|-------|-----------|
| DSP48E1 | 8 | 12 | 2 |
| BRAM (36Kbit) | 4 × (행 × 열 / 512) | + 1 (LUT) | 1 × (행 × 열 / 512) |
| LUT | ~800 | ~1,500 | ~200 |
| FF | ~600 | ~1,000 | ~150 |
| 처리 속도 | 1 pixel/clk | 2 pixel/clk (반복) | 1 pixel/clk |

**R1717 (2048×2048)**: 상태 변수 BRAM = 4 × 2048 × 2048 × 2B = 32 MB → DDR4 필요  
**X239AW1-102 (3072×3072)**: 상태 변수 = 4 × 3072 × 3072 × 2B = 72 MB → DDR4 필요

---


### 9.6 AR(1) 자기회귀 모델 기반 래그 보정

#### 9.6.1 AR(1) 모델 정의

LTI 다중 지수 모델의 대안으로, 1차 자기회귀 모델 AR(1)을 적용할 수 있다:

```
AR(1) 래그 모델:
  y(k) = x(k) + φ × y(k-1)

여기서:
  y(k) = 측정된 프레임 (래그 포함)
  x(k) = 실제 신호 (래그 없음)
  φ    = 자기회귀 계수 (0 < φ < 1)

역변환 (래그 제거):
  x̂(k) = y(k) - φ × y(k-1)

장점:
  - 계산 복잡도 극히 낮음 (곱셈 1회, 뺄셈 1회/픽셀)
  - 상태 변수 1개만 필요 (이전 프레임)
  - FPGA 리소스 최소

단점:
  - 단일 시상수만 모델링 → 장시간 래그 보정 불완전
  - φ는 노출 이력에 무관 → 비선형 래그 미대응
```

#### 9.6.2 AR(1) FPGA 구현

```systemverilog
// ar1_lag_corrector.sv — 단순 AR(1) 래그 보정
module ar1_lag_corrector #(
    parameter PHI_Q16 = 16'd3277  // φ ≈ 0.05 (Q16 = 0.05 × 65536)
)(
    input  logic        clk,
    input  logic        rst_n,
    input  logic        frame_start,
    input  logic [15:0] pixel_in,        // 현재 프레임 y(k)
    input  logic [15:0] pixel_prev,      // 이전 프레임 y(k-1)
    input  logic        pixel_valid,
    output logic [15:0] pixel_corrected, // 보정 결과 x̂(k)
    output logic        out_valid
);

    logic [31:0] lag_estimate;  // φ × y(k-1)
    logic signed [16:0] corrected_raw;

    always_ff @(posedge clk) begin
        if (pixel_valid) begin
            lag_estimate   <= PHI_Q16 * pixel_prev;            // Q16 × Q0 = Q16
            corrected_raw  <= $signed({1'b0, pixel_in})
                            - $signed({1'b0, lag_estimate[31:16]}); // Q0
            // 클리핑: 음수 방지
            pixel_corrected <= (corrected_raw < 0) ? 16'd0 :
                               corrected_raw[15:0];
            out_valid <= 1'b1;
        end else begin
            out_valid <= 1'b0;
        end
    end

endmodule
```

### 9.7 딥러닝 기반 래그 보정 (최신 동향 — 2025)

#### 9.7.1 U-Net 기반 시간 래그 보정

최근 연구에서 CNN(Convolutional Neural Network)을 활용한 래그 보정이 제안되었다.

```
딥러닝 래그 보정 아키텍처:

  입력: 연속 5프레임 → [y(k-2), y(k-1), y(k), y(k+1), y(k+2)]
  네트워크: U-Net (인코더-디코더, skip connections)
  출력: 보정된 프레임 x̂(k)

  특성:
    - 비선형 래그 자연스럽게 학습
    - 노출 이력 의존성 암묵적 모델링
    - Ground truth: Forward Bias + 다중 지수 보정 결과 사용
    - 학습 데이터: 형광투시 팬텀 1000 프레임 이상

  제한사항:
    - 실시간 FPGA 구현 어려움 (추론 시간 > 프레임 시간)
    - GPU 기반 호스트 처리에 적합
    - CBCT 재구성 파이프라인에 후처리로 통합
```

#### 9.7.2 FPGA 호환 경량 모델

```
1D-CNN 기반 경량 래그 보정:
  - 입력: 픽셀의 시간 시계열 (최근 8프레임)
  - 구조: Conv1D(8→16) → ReLU → Conv1D(16→1)
  - 파라미터 수: ~300개 (FPGA BRAM에 저장 가능)
  - 추론: 곱셈-누적(MAC) 300회/픽셀
  - 처리량: 100MHz에서 333K pixels/s
    → 3072² = 9.4M pixels → 28ms (35fps에서 처리 가능)
```

### 9.8 래그 보정 알고리즘 비교 종합표

| 알고리즘 | 1차 래그 잔차 | 50차 래그 잔차 | FPGA 리소스 | 비선형 대응 | 실시간 | 복잡도 |
|---------|-------------|-------------|-----------|-----------|-------|--------|
| 미보정 | 2.4–3.7% | 0.28–0.96% | 없음 | — | — | — |
| Forward Bias (HW) | 0.3% | 0.1% | 낮음 | 부분 | ✓ | 회로 추가 |
| LTI N=4 (Hsieh) | 0.25% | 0.01% | 중간 | ✗ | ✓ | 상태×4 |
| NLCSC | 0.15% | 0.005% | 높음 | ✓ | ✓ | LUT+상태 |
| AR(1) | 0.8% | 0.15% | 매우 낮음 | ✗ | ✓ | 곱셈 1회 |
| FB + LTI N=4 | 0.1% | 0.003% | 중간 | 부분 | ✓ | 최적 조합 |
| U-Net (GPU) | < 0.05% | < 0.001% | GPU 필요 | ✓ | △ | 대규모 |

> **권장 구현**: Forward Bias 하드웨어 + LTI N=4 재귀 필터 조합이 비용 대비 효과 최적. CBCT 고정밀 응용에는 NLCSC 추가 적용.

### 9.9 래그 보정 검증 방법

#### 9.9.1 FSRF (Falling Step-Response Function) 측정

```
FSRF 측정 프로토콜:

  1. 패널 안정화 (100+ 더미 프레임)
  2. 균일 X-ray 조사 100 프레임 (트랩 충전)
  3. X-ray OFF → 300 프레임 연속 리드아웃 (래그 감쇠 측정)
  4. 각 프레임의 ROI 평균값 기록
  5. 정규화: L(k) = [S(k) - Dark] / [S_xray - Dark]

  검증 기준:
    - 보정 후 1차 래그: < 0.3% (목표)
    - 보정 후 50차 래그: < 0.01% (목표)
    - CBCT radar artifact: < 10 HU (목표)
```

#### 9.9.2 시뮬레이션 테스트벤치

```systemverilog
// tb_lag_correction.sv — 래그 보정 검증 테스트벤치
module tb_lag_correction;
    // 4-성분 래그 모델로 입력 생성
    parameter real TAU[4] = '{0.087, 0.4, 3.2, 26.7};  // 초
    parameter real B[4]   = '{0.018, 0.0017, 0.00011, 0.0000071};
    parameter real FPS    = 15.0;

    logic [15:0] x_ideal, y_lagged, x_corrected;
    real lag_state[4];
    integer frame;

    initial begin
        // Step function: 100 frames ON, then 300 frames OFF
        for (frame = 0; frame < 400; frame++) begin
            if (frame < 100)
                x_ideal = 16'd40000;  // X-ray ON
            else
                x_ideal = 16'd0;      // X-ray OFF

            // 래그 모델 적용
            y_lagged = x_ideal;
            for (int n = 0; n < 4; n++) begin
                lag_state[n] = x_ideal + lag_state[n] *
                               $exp(-1.0 / (TAU[n] * FPS));
                y_lagged = y_lagged + B[n] * lag_state[n];
            end

            // DUT에 y_lagged 입력 → x_corrected 확인
            // 잔차 계산: residual = |x_corrected - x_ideal| / 40000
            @(posedge clk);
        end
    end
endmodule
```

---

## 10. 보정 알고리즘 (Calibration Pipeline)

### 10.1 2점 보정 공식

```
표준 2점 보정:
  Corrected(i,j) = [Raw(i,j) - Dark(i,j)] / [Bright(i,j) - Dark(i,j)] × M

  Raw(i,j):   비보정 픽셀값 (ADU)
  Dark(i,j):  다크 맵 (ADU)
  Bright(i,j): 플랫 필드 다크 보정값 (ADU)
  M:          전체 평균값 (스칼라)

FPGA 정수 연산 최적화 (사전 계산):
  DARK_MAP[i][j]  = D_map(i,j)                    [16-bit]
  GAIN_LUT[i][j]  = round(M / Bright[i][j] × 2¹⁶) [16-bit Q16]

  실시간 보정 (per pixel, 1 clock):
    temp = RAW[i][j] - DARK_MAP[i][j]              [17-bit signed]
    CORR[i][j] = (temp × GAIN_LUT[i][j]) >> 16     [16-bit result]
```

*출처: [EP2148500A1](https://patents.google.com/patent/EP2148500A1/en); [Wikipedia Flat-field Correction](https://en.wikipedia.org/wiki/Flat-field_correction)*

### 10.2 오프셋 보정 — 다크 맵 롤링 업데이트

```
롤링 업데이트 공식:
  D_map_new(i,j) = α × D_new(i,j) + (1-α) × D_map_old(i,j)

  α = 0.1: 노이즈 저감 우선 (고정 설치 환경)
  α = 0.3: 온도 추적 우선 (이동형 검출기)

  업데이트 트리거:
    - ΔT > 2°C (온도 변화)
    - 1시간 경과
    - 고선량 노출 후 30초 대기 후
    - 모드 전환 후
```

### 10.3 게인 보정 — 플랫 필드 정규화

```
게인 맵 생성:
  1. N 프레임 플랫 필드 평균: F_avg(i,j)
  2. 다크 보정: F_corr(i,j) = F_avg(i,j) - D_map(i,j)
  3. 전체 평균: M = mean(F_corr)
  4. 정규화 게인: G(i,j) = M / F_corr(i,j)
     (전형적 범위: 0.7–1.3)

  갱신 주기: 일 1회 또는 X-ray 시스템 변경 후
```

### 10.4 결함 화소 보정

#### 10.4.1 통계 검출

```
오프셋 기반 검출:
  μ_dark = mean(D_map)
  σ_dark = std(D_map)
  
  Hot pixel:  D(i,j) > μ_dark + k × σ_dark  (k = 3–5)
  Dead pixel: D(i,j) < μ_dark - k × σ_dark  (k = 3–5)

게인 기반 검출 (메디안 필터):
  F_smooth = median_filter(F_corr, K=5)
  Residual = F_corr - F_smooth
  μ_res = mean(Residual)
  σ_res = std(Residual)
  
  Bad pixel: |Residual(i,j)| > 3 × σ_res
```

*출처: [US5657400A](https://patents.google.com/patent/US5657400A/en)*

#### 10.4.2 Bilinear 보간 대체

```
결함 화소 보간:
  4점 보간 (N, S, E, W):
    Corr(i,j) = [P(i-1,j) + P(i+1,j) + P(i,j-1) + P(i,j+1)] / 4

  방향성 보간 (에지 보존):
    8방향 추정 후 최소 변동 방향 선택
    → FPGA 병렬 컨볼루션으로 1 clock 구현

  결함 화소 맵 저장:
    비트맵: 3072×3072 / 8 = 1.15 MB
    좌표 리스트: 0.1% 결함률 시 ~9,400 × 12B = 113 KB
```

### 10.5 온도 드리프트 보상

```
Arrhenius 기반 온도 보상:
  I_dark(T) = I_0 × exp(-ΔE / kT)

  간이 선형 모델:
    D_dark(i,j,T) = a(i,j) + b(i,j) × T

  다점 온도 캘리브레이션:
    T = 15, 20, 25, 30, 35°C에서 각각 다크 맵 획득
    픽셀별 보간 계수 저장

  재보정 트리거:
    ΔT > 2°C: 신규 다크 프레임 캡처
    ΔT > 5°C: 전체 재캘리브레이션 권장

  5°C 드리프트 시 보정 없이:
    다크 맵 오차 ~41% → 게인 보정 후 증폭 → 이미지 품질 저하
```

### 10.6 FPGA 보정 파이프라인 — 3단계 구조

```
┌────────────┐    ┌────────────┐    ┌────────────────┐    ┌──────────┐
│ Raw Pixel  │───▶│ Stage 1:   │───▶│ Stage 2:       │───▶│ Stage 3: │──▶ Output
│ from AFE   │    │ Offset     │    │ Gain           │    │ Bad Pixel│
│            │    │ Subtract   │    │ Multiply       │    │ Replace  │
└────────────┘    └────────────┘    └────────────────┘    └──────────┘
                   1 clock           1 clock (DSP48)      1 clock (LUT)

처리량 계산 (100 MHz FPGA 클록):
  파이프라인 지연: 3 클록 = 30 ns
  스루풋: 1 pixel/clock = 100 Mpixel/s

  3072×3072 @ 30fps = 283 Mpixel/s
  → 3개 병렬 파이프라인 필요 (또는 300 MHz 클록)
  → 대형 패널(C6/C7)에서는 DDR4 → 보정 → DDR4 오프라인 처리도 고려
```

```systemverilog
// 3-Stage Calibration Pipeline
module cal_pipeline #(
    parameter DATA_W = 16,
    parameter GAIN_W = 16  // Q16 게인
)(
    input  logic clk, rst_n,
    input  logic pixel_valid,
    input  logic [DATA_W-1:0] raw_pixel,
    input  logic [DATA_W-1:0] dark_map_val,      // Stage 1
    input  logic [GAIN_W-1:0] gain_map_val,      // Stage 2
    input  logic bad_pixel_flag,                   // Stage 3
    input  logic [DATA_W-1:0] interp_val,         // 보간값

    output logic [DATA_W-1:0] corrected_pixel,
    output logic corrected_valid
);
    // Stage 1: Offset Subtraction
    logic signed [DATA_W:0] stage1_out;
    logic stage1_valid;

    always_ff @(posedge clk) begin
        stage1_out <= $signed({1'b0, raw_pixel}) -
                      $signed({1'b0, dark_map_val});
        stage1_valid <= pixel_valid;
    end

    // Stage 2: Gain Multiplication (DSP48)
    logic [DATA_W-1:0] stage2_out;
    logic stage2_valid;

    always_ff @(posedge clk) begin
        if (stage1_out < 0)
            stage2_out <= '0;  // 클램프 (음수 방지)
        else
            stage2_out <= (stage1_out[DATA_W-1:0] * gain_map_val) >> GAIN_W;
        stage2_valid <= stage1_valid;
    end

    // Stage 3: Bad Pixel Replacement
    always_ff @(posedge clk) begin
        corrected_pixel <= bad_pixel_flag ? interp_val : stage2_out;
        corrected_valid <= stage2_valid;
    end
endmodule
```

---


### 10.7 다중점 게인 보정 (Multi-Point Calibration)

#### 10.7.1 비선형 응답 보상 필요성

표준 2점 보정은 선형 응답을 가정하나, a-Si:H FPD는 포화 근접 시 비선형성을 보인다.

```
다중점 게인 보정 절차:

  1. K개 노출 레벨에서 Flat Field 캡처 (K = 6–12):
     E = {0.05, 0.1, 0.5, 1.0, 2.0, 5.0, 10.0} µGy

  2. 각 픽셀(i,j)에 대해 다항식 피팅:
     Response(i,j,E) = c₀(i,j) + c₁(i,j)×E + c₂(i,j)×E²

  3. 역함수 LUT 생성:
     E_corrected = f⁻¹(Raw_ADU)

  4. FPGA에서 LUT 기반 실시간 보정:
     - 16-bit 입력 → 16-bit 출력 LUT
     - 크기: 64K × 16bit = 128 KB per pixel cluster
     - 실제: 16×16 픽셀 그룹 공유 → 총 192K 엔트리
```

#### 10.7.2 Heel Effect 보정

X-ray 튜브의 anode heel effect로 인해 anode-cathode 축 방향으로 강도 구배가 발생한다.

```
Heel Effect 특성:
  - 강도 변화: anode 측 5–20% 감소 (kVp, 각도 의존)
  - 공간 주파수: 매우 낮음 (패널 전체에 걸친 부드러운 구배)
  - 보정: Flat Field 게인 맵에 자동 포함됨

  주의사항:
    - 임상에서 환자 위치에 따라 실효 heel effect 변화
    - 적응형 보정 불필요 (Flat Field에서 이미 보정)
    - 단, 다른 kVp 사용 시 게인 맵 재취득 필요
```

### 10.8 IEC 62220-1 표준 준수 항목

#### 10.8.1 DQE (Detective Quantum Efficiency) 측정

```
IEC 62220-1-1:2015 (정지상 검출기):

  DQE(f) = [MTF(f)]² / [q × NNPS(f)]

  여기서:
    MTF(f) = 변조 전달 함수 (슬릿 또는 에지 방법)
    q = 입사 X-ray 퀀텀 수 (µGy 당 photons/mm²)
    NNPS(f) = 정규화된 노이즈 전력 스펙트럼

  측정 조건:
    - 빔 질: RQA-5 (74 kVp, 21 mm Al 필터)
    - 선량: 0.3–10 µGy (복수 레벨)
    - 보정: 다크+게인+결함화소 보정 적용 후 측정

  일반적 성능 (CsI:Tl 간접 변환):
    DQE(0) = 0.65–0.75
    DQE(Nyquist) = 0.10–0.25
```

#### 10.8.2 MTF 측정

```
MTF 측정 방법:

  에지(Edge) 방법 (IEC 권장):
    1. 텅스텐 에지 시편을 ~2–5° 기울여 배치
    2. 과표본화된 Edge Spread Function (ESF) 획득
    3. ESF 미분 → Line Spread Function (LSF)
    4. LSF 의 FFT → MTF

  수직/수평 MTF 독립 측정 필요:
    - 행 방향 MTF: gate 스캔 아티팩트 영향
    - 열 방향 MTF: 데이터 라인 크로스토크 영향
```

#### 10.8.3 NPS (Noise Power Spectrum) 측정

```
NPS 측정 조건:

  1. 균일 조사 (Flat Field)로 최소 32개 프레임 획득
  2. 보정 적용 (Dark + Gain + Bad Pixel)
  3. 128×128 또는 256×256 ROI에서 2D NPS 계산
  4. 반경 방향 평균 → 1D NPS
  5. 래그 보정 팩터(LCF) 적용: NPS_corrected = NPS × LCF

  NPS 측정 시 래그의 영향:
    - 래그는 프레임 간 시간적 상관관계를 유발
    - 미보정 시 NPS가 과소평가됨
    - LCF = 1/(1 - 2×Σ L_n) (1차 근사)
    - 일반적 LCF ≈ 1.05–1.15 (래그 5–15% 보정)
```

### 10.9 AAPM TG-150 품질 관리 권장사항

| 항목 | 주기 | 허용 기준 | FPGA 관련 |
|------|------|----------|-----------|
| 다크 이미지 균일성 | 매일 | σ/mean < 5% | 다크 맵 자동 업데이트 |
| 고스트(래그) 측정 | 월 1회 | 1차 래그 < 3% | 래그 보정 파라미터 검증 |
| 결함 화소 수 | 분기 | < 0.01% (신규 결함) | 결함 맵 자동 업데이트 |
| MTF @ Nyquist | 연 1회 | > 15% (기준 대비 ±10%) | — |
| DQE(0) | 연 1회 | > 0.55 | — |
| 다이나믹 레인지 | 연 1회 | > 14 bit 유효 | ADC 검증 |
| AEC 응답 선형성 | 월 1회 | ±10% | AEC 인터페이스 검증 |

### 10.10 보정 데이터 저장 아키텍처

```
보정 데이터 저장 구조 (3072×3072 패널 기준):

  ┌──────────────────────────────────────────┐
  │           NOR Flash (64 MB)               │
  │                                           │
  │  ┌─────────────────────────────────────┐  │
  │  │ Dark Map (16-bit)                    │  │
  │  │ 3072 × 3072 × 2 bytes = 18.9 MB    │  │
  │  │ × 3 온도 포인트 = 56.6 MB           │  │
  │  └─────────────────────────────────────┘  │
  │                                           │
  │  ┌─────────────────────────────────────┐  │
  │  │ Gain Map (16-bit Q16)               │  │
  │  │ 3072 × 3072 × 2 bytes = 18.9 MB    │  │
  │  └─────────────────────────────────────┘  │
  │                                           │
  │  ┌─────────────────────────────────────┐  │
  │  │ Bad Pixel Map (1-bit + 8-bit flag)  │  │
  │  │ 3072 × 3072 × 1 byte = 9.4 MB      │  │
  │  └─────────────────────────────────────┘  │
  │                                           │
  │  ┌─────────────────────────────────────┐  │
  │  │ Lag Correction Params (per-pixel)   │  │
  │  │ 4 states × 16bit × 9.4M = 75.5 MB  │  │
  │  │ (DDR4에 상주, Flash는 초기값만)      │  │
  │  └─────────────────────────────────────┘  │
  └──────────────────────────────────────────┘

  ┌──────────────────────────────────────────┐
  │           DDR4 SDRAM (1 GB)              │
  │                                           │
  │  Frame Buffer × 3 (Triple): 56.6 MB     │
  │  Dark Map (실시간): 18.9 MB              │
  │  Gain Map (실시간): 18.9 MB              │
  │  Lag State Arrays (4×): 75.5 MB          │
  │  Bad Pixel Map: 9.4 MB                   │
  │  작업 버퍼: 나머지                         │
  └──────────────────────────────────────────┘
```

---

## 11. 멀티 AFE 대형 패널 특수 알고리즘 (C6/C7)

### 11.1 12칩 SYNC 브로드캐스트 타이밍

```
12× AFE SYNC 시스템 구성:

  패널 컬럼 매핑:
    AFE01: col    0 –  255 (West)
    AFE02: col  256 –  511
    ...
    AFE12: col 2816 – 3071 (East)

  SYNC 분배:
    FPGA MMCM → SYNC 생성 → LVDS Fan-out (CDCLVP1208)
    → 12개 등장(equal-length) 트레이스 → 12× AFE SYNC 핀

  타이밍 요건:
    SYNC 스큐 < 1 MCLK 주기 = 31.25 ns @ 32 MHz
    트레이스 매칭: ±41 mm (170 ps/mm 전파 지연)
    팬아웃 버퍼 스큐: < 30 ps (무시 가능)

  MCLK 분배:
    단일 TCXO (32 MHz) → LVDS Fan-out → 12× AFE MCLK
    모든 AFE가 동일한 MCLK 소스 공유 (위상 동기)
```

### 11.2 NT39565D 3072행 STV 시퀀스

```
6× NT39565D 캐스케이드:

  IC[0]: 행    0 –  511 (Top, 512ch)
  IC[1]: 행  512 – 1023
  IC[2]: 행 1024 – 1535
  IC[3]: 행 1536 – 2047
  IC[4]: 행 2048 – 2559
  IC[5]: 행 2560 – 3071 (Bottom)

  단방향 스캔:
    STV1 → IC[0] → STVD→IC[1]→STVD→IC[2]→...→IC[5]
    3072행 순차 스캔

  양방향 동시 스캔 (30fps 지원):
    STV1 → IC[0]→IC[1]→IC[2]: 행 0–1535 (하향)
    STV2 → IC[5]→IC[4]→IC[3]: 행 3071–1536 (상향)
    → 각 절반만 스캔: T_readout/2 = 15 ms (3072행 기준)
    → AFE 12개가 동시에 모든 컬럼 읽기 → 30fps 충족

  CPV 분배:
    FPGA → 6개 IC에 동일 CPV 클록 분배
    CPV = 102.4 kHz (30fps 기준)
    FPGA GPIO: CPV × 1 (공통) + STV1 + STV2 + OE1/OE2 = 5핀
```

### 11.3 처리량 계산

```
30fps @ 3072×3072 처리량:

  프레임 크기:
    3072 × 3072 × 16bit = 18,874,368 bytes ≈ 18 MB

  초당 데이터:
    18 MB × 30 fps = 540 MB/s

  LVDS 대역폭:
    12 AFE × 4 DOUT 쌍 × 256 Mbps = ~12 Gbps 총 대역폭
    유효 데이터율: 540 MB/s × 8 = 4.3 Gbps → 36% 활용률

  DDR4 쓰기 대역폭:
    628 MB/s 필요 (오버헤드 포함)
    DDR4-2400 × 16bit = 4.8 GB/s → 13% 활용률 ✓

  PCIe/Host 전송:
    PCIe Gen2 ×4: 16 Gbps 이론 → 540 MB/s 충분 ✓
```

### 11.4 프레임 버퍼 아키텍처 (DDR4 Triple Buffer)

```
Triple Buffer 구성:

  Buffer A: 현재 캡처 중 프레임 (FPGA 쓰기)
  Buffer B: 전송 대기 프레임 (MCU/Host 읽기)
  Buffer C: 이전 프레임 (래그 보정용 참조)

  메모리 요구:
    단일 프레임: 18 MB
    Triple buffer: 54 MB
    보정 맵 (Dark + Gain): 36 MB
    래그 보정 상태: 72 MB (N=4, 전체 픽셀)
    합계: ~162 MB → DDR4 512 MB 권장

  DDR4 MIG 설정:
    Micron MT40A512M16LY-062E (8 Gbit)
    속도: DDR4-2400 (1200 MHz)
    데이터 폭: 32-bit (4 × ×8 chips)
    AXI 인터페이스: 256-bit @ 300 MHz = 9.6 GB/s
```

```systemverilog
// Triple Buffer Controller
module triple_buffer_ctrl #(
    parameter FRAME_SIZE = 32'd18_874_368,  // 18 MB
    parameter BASE_ADDR_A = 32'h0000_0000,
    parameter BASE_ADDR_B = 32'h0120_0000,  // 18 MB offset
    parameter BASE_ADDR_C = 32'h0240_0000   // 36 MB offset
)(
    input  logic clk, rst_n,
    input  logic frame_wr_done,    // FPGA 프레임 캡처 완료
    input  logic frame_rd_done,    // Host 프레임 읽기 완료
    output logic [31:0] wr_base_addr,   // 현재 쓰기 베이스 주소
    output logic [31:0] rd_base_addr,   // 현재 읽기 베이스 주소
    output logic [31:0] ref_base_addr,  // 참조(래그보정) 베이스 주소
    output logic frame_ready_irq        // Host에 프레임 준비 통지
);
    logic [1:0] wr_idx, rd_idx, ref_idx;
    logic [31:0] addrs [3] = '{BASE_ADDR_A, BASE_ADDR_B, BASE_ADDR_C};

    always_ff @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            wr_idx <= 2'd0;
            rd_idx <= 2'd1;
            ref_idx <= 2'd2;
            frame_ready_irq <= 1'b0;
        end else begin
            frame_ready_irq <= 1'b0;
            if (frame_wr_done) begin
                // 버퍼 회전: 쓰기→읽기→참조→쓰기
                ref_idx <= rd_idx;
                rd_idx  <= wr_idx;
                wr_idx  <= ref_idx;
                frame_ready_irq <= 1'b1;
            end
        end
    end

    assign wr_base_addr  = addrs[wr_idx];
    assign rd_base_addr  = addrs[rd_idx];
    assign ref_base_addr = addrs[ref_idx];
endmodule
```

### 11.5 에러 검출 및 복구

```
에러 감지 메커니즘:

  1. LVDS 비트 에러 검출:
     - CRC 또는 패리티 체크 (AFE 출력에 포함된 경우)
     - 트레이닝 패턴 주기적 검증 (매 프레임 시작)
     - 에러율 > 임계값 시 재정렬 트리거

  2. SYNC 동기 이탈 검출:
     - FCLK(프레임 클록) 카운트 모니터링
     - 12개 AFE의 FCLK가 동시에 발생하지 않으면 에러
     - 대응: SYNC 재브로드캐스트 + 더미 프레임

  3. DDR4 ECC 에러:
     - SECDED (Single Error Correction, Double Error Detection)
     - 단일 비트 에러: 자동 보정 + 로그
     - 이중 비트 에러: 프레임 폐기 + 재취득

  4. 온도 과열 보호:
     - T > 65°C: 경고 (MCU 통보)
     - T > 80°C: 강제 전원 차단

  복구 시퀀스:
    에러 감지 → ERROR 상태 진입
    → MCU 통보 (REG_ERR_CODE 기록)
    → 자동 복구 시도 (AFE 리셋 + 재초기화)
    → 3회 실패 시 수동 개입 요구
```

---


### 11.6 전원 시퀀싱 상세 (대형 패널)

#### 11.6.1 전원 레일 정의

| 레일 | 전압 | 전류 (typ) | 용도 | 시퀀스 순서 |
|------|------|-----------|------|-----------|
| DVDD | 1.0V | 2A | FPGA core | 1 |
| AVDD1 | 1.85V | 500mA×12 | AFE2256 아날로그 코어 | 2 |
| AVDD2 | 3.3V | 300mA×12 | AFE2256 I/O | 3 |
| VGH | +20V ~ +35V | 100mA | Gate IC 하이사이드 | 4 |
| VGL/VEE | -8V ~ -15V | 50mA | Gate IC 로우사이드 | 4 |
| VBIAS | -5V | 10mA | 포토다이오드 역바이어스 | 5 |

#### 11.6.2 전원 시퀀싱 FSM

```systemverilog
// power_sequencer.sv — 대형 패널 전원 시퀀싱
module power_sequencer #(
    parameter SYS_CLK_HZ = 100_000_000,
    parameter T_RAIL_STABLE_MS = 10,   // 각 레일 안정화 대기
    parameter T_VGH_DELAY_MS   = 50    // VGH/VGL 상승 대기
)(
    input  logic        clk,
    input  logic        rst_n,
    input  logic        power_on_req,
    input  logic        power_off_req,
    input  logic [4:0]  pg_flags,      // Power-Good 플래그 [DVDD,AVDD1,AVDD2,VGH,VBIAS]
    output logic [4:0]  en_rails,      // 각 레일 Enable
    output logic        power_ready,
    output logic        power_fault
);

    typedef enum logic [3:0] {
        PWR_OFF      = 4'd0,
        EN_DVDD      = 4'd1,
        WAIT_DVDD    = 4'd2,
        EN_AVDD1     = 4'd3,
        WAIT_AVDD1   = 4'd4,
        EN_AVDD2     = 4'd5,
        WAIT_AVDD2   = 4'd6,
        EN_VGH_VGL   = 4'd7,
        WAIT_VGH     = 4'd8,
        EN_VBIAS     = 4'd9,
        WAIT_VBIAS   = 4'd10,
        PWR_READY    = 4'd11,
        PWR_DOWN_SEQ = 4'd12,
        PWR_FAULT    = 4'd13
    } pwr_state_t;

    pwr_state_t state;
    logic [31:0] timer;
    localparam T_STABLE = SYS_CLK_HZ / 1000 * T_RAIL_STABLE_MS;
    localparam T_VGH    = SYS_CLK_HZ / 1000 * T_VGH_DELAY_MS;

    always_ff @(posedge clk or negedge rst_n) begin
        if (!rst_n) begin
            state       <= PWR_OFF;
            en_rails    <= 5'b0;
            power_ready <= 1'b0;
            power_fault <= 1'b0;
            timer       <= '0;
        end else begin
            case (state)
                PWR_OFF: begin
                    en_rails <= 5'b0;
                    power_ready <= 1'b0;
                    if (power_on_req) begin
                        state <= EN_DVDD;
                        timer <= '0;
                    end
                end

                EN_DVDD: begin
                    en_rails[0] <= 1'b1;  // DVDD
                    state <= WAIT_DVDD;
                    timer <= '0;
                end

                WAIT_DVDD: begin
                    timer <= timer + 1;
                    if (!pg_flags[0] && timer > T_STABLE * 5)
                        state <= PWR_FAULT;
                    else if (pg_flags[0] && timer > T_STABLE)
                        state <= EN_AVDD1;
                end

                EN_AVDD1: begin
                    en_rails[1] <= 1'b1;
                    state <= WAIT_AVDD1;
                    timer <= '0;
                end

                WAIT_AVDD1: begin
                    timer <= timer + 1;
                    if (pg_flags[1] && timer > T_STABLE)
                        state <= EN_AVDD2;
                    else if (timer > T_STABLE * 5)
                        state <= PWR_FAULT;
                end

                EN_AVDD2: begin
                    en_rails[2] <= 1'b1;
                    state <= WAIT_AVDD2;
                    timer <= '0;
                end

                WAIT_AVDD2: begin
                    timer <= timer + 1;
                    if (pg_flags[2] && timer > T_STABLE)
                        state <= EN_VGH_VGL;
                    else if (timer > T_STABLE * 5)
                        state <= PWR_FAULT;
                end

                EN_VGH_VGL: begin
                    en_rails[3] <= 1'b1;
                    state <= WAIT_VGH;
                    timer <= '0;
                end

                WAIT_VGH: begin
                    timer <= timer + 1;
                    if (pg_flags[3] && timer > T_VGH)
                        state <= EN_VBIAS;
                    else if (timer > T_VGH * 3)
                        state <= PWR_FAULT;
                end

                EN_VBIAS: begin
                    en_rails[4] <= 1'b1;
                    state <= WAIT_VBIAS;
                    timer <= '0;
                end

                WAIT_VBIAS: begin
                    timer <= timer + 1;
                    if (pg_flags[4] && timer > T_STABLE)
                        state <= PWR_READY;
                    else if (timer > T_STABLE * 5)
                        state <= PWR_FAULT;
                end

                PWR_READY: begin
                    power_ready <= 1'b1;
                    if (power_off_req)
                        state <= PWR_DOWN_SEQ;
                    // 런타임 PG 모니터링
                    if (pg_flags != 5'b11111)
                        state <= PWR_FAULT;
                end

                PWR_DOWN_SEQ: begin
                    // 역순 셧다운: VBIAS → VGH → AVDD2 → AVDD1 → DVDD
                    en_rails <= 5'b0;
                    power_ready <= 1'b0;
                    state <= PWR_OFF;
                end

                PWR_FAULT: begin
                    en_rails <= 5'b0;
                    power_fault <= 1'b1;
                    power_ready <= 1'b0;
                end
            endcase
        end
    end

endmodule
```

### 11.7 열 관리 설계

#### 11.7.1 열 분포 분석 (대형 패널)

```
43×43 cm 패널 열 분포:

  ┌─────────────────────────────────────────────┐
  │                  패널 상단                    │
  │  ┌──────────────────────────────────────┐   │
  │  │         28°C          29°C           │   │
  │  │                                      │   │
  │  │    27°C    중앙: 32°C    28°C        │   │ ← 온도 분포
  │  │                                      │   │
  │  │         30°C          31°C           │   │
  │  └──────────────────────────────────────┘   │
  │  [AFE0][AFE1][AFE2][AFE3][AFE4][AFE5]      │ ← 발열원
  │  [AFE6][AFE7][AFE8][AFE9][AFE10][AFE11]    │
  │                  패널 하단                    │
  │            Gate IC × 6 (발열원)              │
  └─────────────────────────────────────────────┘

열 불균일성:
  - 중앙-주변 온도차: 3–5°C (정상 동작)
  - AFE 칩 근처: 추가 2–3°C 상승
  - 총 ΔT: 최대 8°C → 다크 전류 4배 차이 가능
```

#### 11.7.2 구역별 다크 보정 전략

```
패널을 4개 구역으로 분할하여 독립 다크 보정:

  Zone 0 (좌상): 센서 T0 → Dark_Map_0
  Zone 1 (우상): 센서 T1 → Dark_Map_1
  Zone 2 (좌하): 센서 T2 → Dark_Map_2
  Zone 3 (우하): 센서 T3 → Dark_Map_3

각 구역별 다크 맵 업데이트 트리거: ΔT > 2°C

SystemVerilog 구현:
  // 구역별 온도 센서 읽기 → 독립 다크 맵 보간
  // 총 4개 다크 맵 저장 필요 → DDR4에 75.6 MB (4 × 18.9 MB)
```

### 11.8 EMI 대책 설계 지침

대형 패널(C6/C7)에서 12개 AFE + 6개 Gate IC의 동시 스위칭은 상당한 EMI를 발생시킨다.

| EMI 소스 | 주파수 | 대책 |
|----------|--------|------|
| Gate IC CPV 스위칭 | 100–200 kHz | 슬루율 제한 (tr > 1 µs), 직렬 저항 삽입 |
| AFE MCLK | 32 MHz | LVDS 차동 전송, 그라운드 가드 트레이스 |
| LVDS 데이터 | 128 MHz | 임피던스 매칭 (100Ω 차동), 전용 그라운드 플레인 |
| FPGA 코어 스위칭 | 100–300 MHz | 디커플링 캐패시터 (100nF + 1µF/핀) |
| DDR4 인터페이스 | 1200 MHz | 매칭 종단, 길이 매칭 ±10 mil |

```
PCB 레이아웃 가이드라인:

  1. LVDS 트레이스: 12개 AFE → FPGA 차동 쌍
     - 임피던스: 100Ω 차동 ±10%
     - 길이 매칭: 각 AFE 내 DOUT/DCLK ±5 mil
     - 칩 간 길이 차이: ≤41 mm (7ns MCLK 스큐 허용)

  2. 전원 플레인 분리:
     - DVDD (1.0V 디지털) — 별도 플레인
     - AVDD (1.85V/3.3V 아날로그) — 별도 플레인
     - VGH/VGL (고전압) — 격리된 레이어

  3. 디지털-아날로그 분리:
     - FPGA 디지털 I/O와 AFE 아날로그 입력 간 최소 10mm 간격
     - 그라운드 슬릿 금지 (연속 그라운드 플레인 유지)
```

---

## 12. SystemVerilog 파라미터 및 레지스터 맵 설계

### 12.1 레지스터 맵

모든 타이밍 파라미터는 `reg_bank`에서 MCU가 SPI를 통해 읽고 쓸 수 있다:

```
주소  이름              비트폭  R/W  설명
─────────────────────────────────────────────────────────────────
0x00  REG_CTRL          8      R/W  [0]=START, [1]=ABORT, [2]=IRQ_EN
0x01  REG_STATUS        8      R    [0]=BUSY, [1]=DONE, [2]=ERROR, [3]=LINE_RDY
0x02  REG_MODE          4      R/W  구동 모드 (STATIC/CONTINUOUS/TRIGGERED/DARK/RESET)
0x03  REG_COMBO         4      R/W  부품 조합 (C1~C7)
0x04  REG_NROWS        12      R/W  유효 행 수 (최대 3072)
0x05  REG_NCOLS        12      R/W  유효 열 수 (최대 3072)
0x06  REG_TLINE        16      R/W  라인 타임 (10ns 단위, 최소 22µs=2200)
0x07  REG_TRESET       16      R/W  리셋 시퀀스 시간 (10ns 단위)
0x08  REG_TINTEG       24      R/W  적분 시간 (10ns 단위)
0x09  REG_TGATE_ON     12      R/W  Gate ON 펄스 폭 (FPGA clk 단위)
0x0A  REG_TGATE_SETTLE  8      R/W  Gate 안정화 대기 (FPGA clk 단위)
0x0B  REG_AFE_IFS       6      R/W  AFE 풀스케일 코드
0x0C  REG_AFE_LPF       4      R/W  LPF 시상수 코드
0x0D  REG_AFE_PMODE     2      R/W  AFE 전력 모드
0x0E  REG_CIC_EN        1      R/W  AFE2256 CIC 활성화
0x0F  REG_CIC_PROFILE   4      R/W  AFE2256 CIC 프로파일
0x10  REG_SCAN_DIR      1      R/W  스캔 방향 (0=정방향, 1=역방향)
0x11  REG_BIDIR_EN      1      R/W  양방향 스캔 활성화
0x12  REG_GATE_MODE     2      R/W  Gate IC 채널 모드 (MD/MODE 설정)
0x13  REG_AFE_NCHIP     4      R/W  AFE 체인 수 (1~12)
0x14  REG_SYNC_DLY      8      R/W  SYNC 딜레이 조정 (10ns 단위)
0x15  REG_NRESET        4      R/W  Pre-exposure 리셋 횟수 (0~15)
0x16  REG_NDUMMY        8      R/W  Dummy 스캔 횟수 (0~255)
0x17  REG_ROI_START    12      R/W  ROI 시작 행
0x18  REG_ROI_END      12      R/W  ROI 끝 행
0x19  REG_PIPELINE_EN   1      R/W  AFE 파이프라인 모드 활성화
0x1A  REG_TEMP         12      R    현재 온도 (ADC 값)
0x1B  REG_LINE_IDX     12      R    현재 스캔 중인 행 인덱스
0x1C  REG_FRAME_CNT    16      R    프레임 카운터
0x1D  REG_ERR_CODE      8      R    에러 코드
0x1E  REG_SCRUB_ALPHA   8      R/W  Scrubbing 알파 (Q8, 기본=26)
0x1F  REG_VERSION       8      R    FPGA 펌웨어 버전
```

### 12.2 조합별 권장 레지스터 기본값

| 레지스터 | C1 (NV1047+AD71124) | C2 (NV1047+AD71143) | C3 (NV1047+AFE2256) | C6 (NT39565D+AD71124×12) | C7 (NT39565D+AFE2256×12) |
|---------|---------------------|---------------------|---------------------|-------------------------|-------------------------|
| REG_COMBO | 0x1 | 0x2 | 0x3 | 0x6 | 0x7 |
| REG_NROWS | 2048 | 2048 | 2048 | 3072 | 3072 |
| REG_NCOLS | 2048 | 2048 | 2048 | 3072 | 3072 |
| REG_TLINE | 2200 (22µs) | 6000 (60µs) | 2000 (20µs) | 977 (9.77µs) | 800 (8µs) |
| REG_TGATE_ON | 1500 (15µs) | 4500 (45µs) | 1200 (12µs) | 500 (5µs) | 500 (5µs) |
| REG_TGATE_SETTLE | 200 (2µs) | 200 (2µs) | 200 (2µs) | 500 (5µs) | 500 (5µs) |
| REG_AFE_IFS | 0x02 (2pC) | 0x02 (2pC) | 0x01 (1.2pC) | 0x02 (2pC) | 0x01 (1.2pC) |
| REG_CIC_EN | 0 | 0 | 1 | 0 | 1 |
| REG_AFE_NCHIP | 1 | 1 | 1 | 12 | 12 |
| REG_PIPELINE_EN | 0 | 0 | 1 | 1 | 1 |
| REG_NRESET | 3 | 3 | 3 | 5 | 5 |
| REG_NDUMMY | 30 | 30 | 30 | 50 | 50 |

### 12.3 SystemVerilog 파라미터 구조

```systemverilog
// detector_core.sv — 최상위 구동 코어
module detector_core #(
    // ─── 부품 조합 선택 ───
    parameter string GATE_IC_TYPE = "NV1047",    // "NV1047" / "NT39565D"
    parameter string AFE_TYPE     = "AD71124",   // "AD71124" / "AD71143" / "AFE2256"

    // ─── 패널 해상도 ───
    parameter int MAX_ROWS    = 2048,
    parameter int MAX_COLS    = 2048,
    parameter int N_AFE_CHIPS = 1,               // AFE 체인 수 (1~12)

    // ─── 클럭 ───
    parameter int SYS_CLK_HZ  = 100_000_000,    // FPGA 시스템 클록
    parameter int AFE_CLK_HZ  = 40_000_000,     // ACLK 또는 MCLK

    // ─── 타이밍 제약 (ns) ───
    parameter int TLINE_MIN_NS  = 22_000,        // 최소 라인 타임
    parameter int TRESET_MIN_NS = 1_000_000,     // 최소 리셋 시간

    // ─── 보정 파라미터 ───
    parameter int LAG_N_COMP = 4,                // 래그 보정 성분 수
    parameter int CAL_DARK_FRAMES = 64,          // 다크 캘리브레이션 프레임 수
    parameter int CAL_FLAT_FRAMES = 32           // 플랫 필드 프레임 수
)(
    // 시스템
    input  logic sys_clk,
    input  logic sys_rst_n,

    // MCU SPI 인터페이스
    input  logic spi_sclk, spi_mosi, spi_cs_n,
    output logic spi_miso,

    // Gate IC 출력 (NV1047 또는 NT39565D)
    output logic gate_clk,               // CLK or CPV
    output logic gate_data,              // SD1 or STV1/STV2
    output logic gate_oe,                // OE or OE1
    output logic gate_dir,               // L/R or LR
    output logic gate_rst,               // RST or ONA

    // AFE 인터페이스
    output logic afe_clk,                // ACLK or MCLK
    output logic afe_sync,               // SYNC
    output logic afe_reset_n,            // RESET
    input  logic afe_dclk_p, afe_dclk_n, // LVDS 클록
    input  logic afe_dout_p, afe_dout_n, // LVDS 데이터

    // X-ray 트리거
    input  logic xray_trig_in,
    output logic xray_prep_req,
    output logic xray_enable,

    // MCU 데이터 출력
    output logic [15:0] pixel_data,
    output logic pixel_valid,
    output logic frame_done_irq,

    // 온도 센서
    input  logic [11:0] temp_adc
);
    // ─── 내부 연결 ───
    // (모듈 인스턴스화는 조합별 fpga_top에서 수행)
endmodule
```

---


### 12.4 detector_core 최상위 모듈 인스턴스

```systemverilog
// detector_core.sv — 최상위 탐지기 코어 모듈
module detector_core #(
    parameter GATE_IC_TYPE  = "NV1047",
    parameter AFE_TYPE      = "AD71124",
    parameter MAX_ROWS      = 2048,
    parameter MAX_COLS      = 2048,
    parameter N_AFE_CHIPS   = 1,
    parameter SYS_CLK_HZ    = 100_000_000,
    parameter AFE_CLK_HZ    = 10_000_000,
    parameter TLINE_MIN_NS  = 22_000,
    parameter PIXEL_BITS    = 16
)(
    // 시스템 인터페이스
    input  logic              sys_clk,
    input  logic              sys_rst_n,
    // SPI 슬레이브 (MCU 인터페이스)
    input  logic              spi_sck,
    input  logic              spi_mosi,
    output logic              spi_miso,
    input  logic              spi_cs_n,
    // Gate IC 출력
    output logic              gate_clk,
    output logic              gate_data,
    output logic              gate_oe,
    output logic              gate_dir,
    output logic              gate_rst,
    // AFE 제어
    output logic              afe_clk,
    output logic              afe_sync,
    output logic              afe_rst_n,
    // AFE SPI
    output logic              afe_spi_sck,
    output logic              afe_spi_mosi,
    input  logic              afe_spi_miso,
    output logic              afe_spi_cs_n,
    // LVDS 데이터 입력
    input  logic [N_AFE_CHIPS-1:0] lvds_data_p,
    input  logic [N_AFE_CHIPS-1:0] lvds_data_n,
    input  logic [N_AFE_CHIPS-1:0] lvds_dclk_p,
    input  logic [N_AFE_CHIPS-1:0] lvds_dclk_n,
    // X-ray 인터페이스
    input  logic              xray_prep,
    input  logic              xray_active,
    output logic              xray_req,
    // 상태 출력
    output logic              irq_n,
    output logic              busy,
    output logic              error_flag,
    // DDR4 인터페이스 (프레임 버퍼)
    output logic [15:0]       ddr4_addr,
    output logic [2:0]        ddr4_ba,
    inout  wire  [63:0]       ddr4_dq,
    // MCU 데이터 출력
    output logic [PIXEL_BITS-1:0] pixel_out,
    output logic              pixel_valid_out,
    output logic [11:0]       row_idx_out,
    output logic [11:0]       col_idx_out
);

    // ─── 내부 신호 선언 ───
    logic [31:0] reg_data_out;
    logic [7:0]  reg_addr;
    logic        reg_wr_en, reg_rd_en;

    // 레지스터 값
    logic [2:0]  mode;
    logic [3:0]  combo_sel;
    logic [11:0] n_rows, n_cols;
    logic [15:0] t_line;
    logic [15:0] t_reset;
    logic [23:0] t_integ;
    logic [11:0] t_gate_on;
    logic [7:0]  t_gate_settle;
    logic        start_cmd, abort_cmd;

    // FSM 상태
    logic [2:0]  fsm_state;
    logic [11:0] current_row;
    logic        frame_done;

    // Gate 드라이버 인터페이스
    logic        gate_scan_start;
    logic        gate_on_pulse;
    logic [11:0] gate_row;

    // AFE 인터페이스
    logic        afe_start;
    logic [PIXEL_BITS-1:0] line_pixel;
    logic        line_pixel_valid;
    logic [7:0]  line_ch_idx;
    logic        line_complete;

    // ─── SPI 슬레이브 인스턴스 ───
    spi_slave_if u_spi (
        .clk        (sys_clk),
        .rst_n      (sys_rst_n),
        .spi_sck    (spi_sck),
        .spi_mosi   (spi_mosi),
        .spi_miso   (spi_miso),
        .spi_cs_n   (spi_cs_n),
        .reg_addr   (reg_addr),
        .reg_wdata  (reg_data_out),
        .reg_wr_en  (reg_wr_en),
        .reg_rd_en  (reg_rd_en)
    );

    // ─── 레지스터 뱅크 인스턴스 ───
    reg_bank u_regs (
        .clk        (sys_clk),
        .rst_n      (sys_rst_n),
        .addr       (reg_addr),
        .wdata      (reg_data_out),
        .wr_en      (reg_wr_en),
        // 출력 레지스터 값
        .mode       (mode),
        .combo_sel  (combo_sel),
        .n_rows     (n_rows),
        .n_cols     (n_cols),
        .t_line     (t_line),
        .t_reset    (t_reset),
        .t_integ    (t_integ),
        .t_gate_on  (t_gate_on),
        .t_gate_settle (t_gate_settle),
        .start_cmd  (start_cmd),
        .abort_cmd  (abort_cmd)
    );

    // ─── 메인 FSM 인스턴스 ───
    panel_ctrl_fsm #(
        .MAX_ROWS    (MAX_ROWS),
        .SYS_CLK_HZ (SYS_CLK_HZ)
    ) u_fsm (
        .clk            (sys_clk),
        .rst_n          (sys_rst_n),
        .start          (start_cmd),
        .abort          (abort_cmd),
        .mode           (mode),
        .n_rows         (n_rows),
        .t_line         (t_line),
        .t_reset        (t_reset),
        .t_integ        (t_integ),
        .xray_active    (xray_active),
        .line_complete  (line_complete),
        // 출력
        .fsm_state      (fsm_state),
        .current_row    (current_row),
        .gate_scan_start(gate_scan_start),
        .gate_on_pulse  (gate_on_pulse),
        .afe_start      (afe_start),
        .frame_done     (frame_done),
        .xray_req       (xray_req),
        .busy           (busy),
        .irq            (irq_n)
    );

    // ─── Gate IC 드라이버 (조합별 선택) ───
    generate
        if (GATE_IC_TYPE == "NV1047") begin : gen_gate_nv
            gate_nv1047 u_gate (
                .clk        (sys_clk),
                .rst_n      (sys_rst_n),
                .scan_start (gate_scan_start),
                .row_idx    (current_row),
                .t_gate_on  (t_gate_on),
                .t_settle   (t_gate_settle),
                .gate_clk   (gate_clk),
                .gate_data  (gate_data),
                .gate_oe    (gate_oe),
                .gate_dir   (gate_dir),
                .gate_rst   (gate_rst)
            );
        end else begin : gen_gate_nt
            gate_nt39565d u_gate (
                .clk        (sys_clk),
                .rst_n      (sys_rst_n),
                .scan_start (gate_scan_start),
                .row_idx    (current_row),
                .t_gate_on  (t_gate_on),
                .t_settle   (t_gate_settle),
                .stv1       (gate_clk),
                .cpv        (gate_data),
                .oe1        (gate_oe),
                .lr         (gate_dir),
                .stv2       (gate_rst)
            );
        end
    endgenerate

    // ─── AFE 제어 (조합별 선택) ───
    generate
        if (AFE_TYPE == "AFE2256") begin : gen_afe_ti
            afe_afe2256 u_afe (
                .clk        (sys_clk),
                .rst_n      (sys_rst_n),
                .afe_start  (afe_start),
                .mclk       (afe_clk),
                .sync       (afe_sync),
                .afe_rst_n  (afe_rst_n)
            );
        end else begin : gen_afe_adi
            afe_ad711xx u_afe (
                .clk        (sys_clk),
                .rst_n      (sys_rst_n),
                .afe_start  (afe_start),
                .aclk       (afe_clk),
                .sync       (afe_sync),
                .afe_rst_n  (afe_rst_n)
            );
        end
    endgenerate

endmodule
```

### 12.5 테스트벤치 프레임워크

#### 12.5.1 패널 에뮬레이터 모델

```systemverilog
// tb_panel_emulator.sv — a-Si TFT 패널 동작 에뮬레이터
module tb_panel_emulator #(
    parameter N_ROWS     = 2048,
    parameter N_COLS     = 2048,
    parameter PIXEL_BITS = 16
)(
    // Gate IC 입력
    input  logic        gate_clk,
    input  logic        gate_oe,
    // AFE 동기 입력
    input  logic        afe_clk,
    input  logic        afe_sync,
    // LVDS 데이터 출력 (에뮬레이션)
    output logic        lvds_data_p,
    output logic        lvds_data_n,
    output logic        lvds_dclk_p,
    output logic        lvds_dclk_n
);

    // 픽셀 메모리 (테스트 패턴 저장)
    logic [PIXEL_BITS-1:0] pixel_mem [N_ROWS][N_COLS];
    int active_row;
    int col_cnt;
    logic [PIXEL_BITS-1:0] shift_reg;

    // 초기화: 테스트 패턴 생성
    initial begin
        for (int r = 0; r < N_ROWS; r++)
            for (int c = 0; c < N_COLS; c++)
                pixel_mem[r][c] = (r * N_COLS + c) & 16'hFFFF;
    end

    // Gate 클럭으로 행 카운트
    always @(posedge gate_clk) begin
        if (!gate_oe) begin  // Gate ON
            active_row <= active_row + 1;
            col_cnt <= 0;
        end
    end

    // AFE 클럭으로 데이터 직렬 출력
    always @(posedge afe_clk) begin
        if (col_cnt < N_COLS) begin
            shift_reg <= pixel_mem[active_row][col_cnt];
            col_cnt <= col_cnt + 1;
            lvds_data_p <= shift_reg[PIXEL_BITS-1]; // MSB first
            lvds_data_n <= ~shift_reg[PIXEL_BITS-1];
        end
    end

    assign lvds_dclk_p = afe_clk;
    assign lvds_dclk_n = ~afe_clk;

endmodule
```

#### 12.5.2 자동 검증 체커

```systemverilog
// tb_checker.sv — 출력 데이터 자동 검증
module tb_checker #(
    parameter PIXEL_BITS = 16,
    parameter TOLERANCE  = 2       // 허용 오차 (LSB)
)(
    input  logic                    clk,
    input  logic [PIXEL_BITS-1:0]  dut_pixel,
    input  logic                    dut_valid,
    input  logic [PIXEL_BITS-1:0]  ref_pixel,
    input  logic [11:0]            row_idx,
    input  logic [11:0]            col_idx,
    output int                      error_count,
    output int                      total_count
);

    always @(posedge clk) begin
        if (dut_valid) begin
            total_count <= total_count + 1;
            if ($signed(dut_pixel - ref_pixel) > TOLERANCE ||
                $signed(ref_pixel - dut_pixel) > TOLERANCE) begin
                error_count <= error_count + 1;
                $display("[ERROR] Row=%0d Col=%0d: DUT=%0d REF=%0d DIFF=%0d",
                    row_idx, col_idx, dut_pixel, ref_pixel,
                    $signed(dut_pixel - ref_pixel));
            end
        end
    end

endmodule
```

### 12.6 FPGA 타이밍 제약 (XDC) 예시

```tcl
# fpga_constraints.xdc — Xilinx Kintex UltraScale 타이밍 제약

# 시스템 클럭 (100 MHz)
create_clock -name sys_clk -period 10.0 [get_ports sys_clk_p]

# AFE MCLK 출력 (32 MHz, AFE2256)
create_generated_clock -name mclk_out -source [get_pins u_pll/CLKOUT0]     -divide_by 3 [get_ports afe_mclk_p]

# AFE ACLK 출력 (10 MHz, AD71124)
create_generated_clock -name aclk_out -source [get_pins u_pll/CLKOUT1]     -divide_by 10 [get_ports afe_aclk]

# LVDS 입력 클럭 (128 MHz, 데이터 클럭)
create_clock -name lvds_dclk_0 -period 7.8125 [get_ports lvds_dclk_p[0]]
create_clock -name lvds_dclk_1 -period 7.8125 [get_ports lvds_dclk_p[1]]
# ... AFE 2-11 동일

# 클럭 도메인 간 제약
set_clock_groups -asynchronous     -group [get_clocks sys_clk]     -group [get_clocks lvds_dclk_*]

# SPI 인터페이스 (최대 25 MHz)
create_clock -name spi_clk -period 40.0 [get_ports spi_sck]
set_clock_groups -asynchronous     -group [get_clocks sys_clk]     -group [get_clocks spi_clk]

# LVDS 입력 딜레이 제약
set_input_delay -clock lvds_dclk_0 -max 1.5 [get_ports lvds_data_p[0]]
set_input_delay -clock lvds_dclk_0 -min 0.5 [get_ports lvds_data_p[0]]

# Gate IC 출력 제약 (느린 신호, 타이밍 여유 충분)
set_output_delay -clock sys_clk -max 5.0 [get_ports {gate_*}]
set_output_delay -clock sys_clk -min 0.0 [get_ports {gate_*}]

# X-ray 인터페이스 (비동기 입력)
set_false_path -from [get_ports xray_active]
set_false_path -from [get_ports xray_prep]

# DDR4 타이밍 (MIG IP 자동 생성)
# ... MIG IP에서 자동 설정
```

---

## 13. 성능 목표 및 검증 계획

### 13.1 조합별 성능 목표

| 항목 | C1 | C2 | C3 | C4 | C5 | C6 | C7 |
|------|----|----|----|----|----|----|-----|
| **해상도** | 2048² | 2048² | 2048² | 2048×1792 | 2048×1792 | 3072² | 3072² |
| **정지상 fps** | 15 | 7.5 | 15 | 15 | 15 | 10 | 10 |
| **형광투시 fps** | 30 | 15 | 30 | 30 | 30 | 15–30 | 30 |
| **라인 타임** | 22 µs | 60 µs | 20 µs | 22 µs | 20 µs | 9.77 µs | 8 µs |
| **노이즈** | 560 e⁻ | 580 e⁻ | 240 e⁻ | 560 e⁻ | 240 e⁻ | 560 e⁻ | 240 e⁻ |
| **1st lag (FB)** | < 0.3% | < 0.3% | < 0.3% | < 0.3% | < 0.3% | < 0.3% | < 0.3% |
| **다이나믹 레인지** | 14 bit | 13 bit | 15 bit | 14 bit | 15 bit | 14 bit | 15 bit |
| **CIC 보상** | N/A | N/A | 92% | N/A | 92% | N/A | 92% |
| **DDR4 필요** | 아니오 | 아니오 | 아니오 | 아니오 | 아니오 | **예** | **예** |
| **AFE 수** | 1 | 1 | 1 | 1 | 1 | 12 | 12 |
| **처리량** | 126 MB/s | 63 MB/s | 126 MB/s | 110 MB/s | 110 MB/s | 540 MB/s | 540 MB/s |

### 13.2 FPGA 리소스 추정 (Xilinx Kintex UltraScale)

| 리소스 | C1–C5 (소형) | C6/C7 (대형) |
|--------|------------|-------------|
| LUT | ~15,000 | ~45,000 |
| FF | ~12,000 | ~35,000 |
| BRAM (36Kb) | 24 | 72 |
| DSP48E2 | 8 | 24 |
| MMCM/PLL | 2 | 6 |
| LVDS RX 쌍 | 6 | 72 |
| DDR4 MIG | 미사용 | 1 |
| FPGA 추천 | XC7K160T | XCKU040 |

### 13.3 시뮬레이션/테스트벤치 항목

| 단계 | 모듈 | 검증 항목 | 방법 |
|------|------|---------|------|
| **Phase 1** | `spi_slave_if` + `reg_bank` | SPI 프로토콜, 레지스터 R/W | ModelSim UVM TB |
| **Phase 2** | `panel_ctrl_fsm` | FSM 상태 전이, 타이밍 카운터 | FSM coverage TB |
| **Phase 3** | `gate_nv1047` | SD1/CLK 시퀀스, OE 타이밍 | NV1047 BFM |
| **Phase 4** | `gate_nt39565d` | STV/CPV 시퀀스, OE1/OE2 분리 | NT39565D BFM |
| **Phase 5** | `afe_ad711xx` | ACLK, SYNC, SPI 설정 | ADAS1256 BFM |
| **Phase 6** | `afe_afe2256` | MCLK, CIC, Pipeline | AFE2256 BFM |
| **Phase 7** | `line_data_rx` | LVDS 역직렬화, BITSLIP | LVDS 모델 |
| **Phase 8** | `cal_pipeline` | 오프셋/게인/결함 보정 정확도 | Golden 모델 비교 |
| **Phase 9** | `lag_correction_lti` | N=4 재귀 필터 수치 정확도 | MATLAB 참조 비교 |
| **Phase 10** | 통합 C1 E2E | 전체 정지상 획득 시퀀스 | 패널 에뮬레이터 |
| **Phase 11** | 통합 C7 E2E | 12-AFE SYNC, DDR4, 30fps | 대형 패널 에뮬레이터 |
| **Phase 12** | HIL | 실제 보드 포팅, 실측 비교 | 하드웨어 보드 |

### 13.4 주요 검증 기준

```
정량적 합격 기준:
  1. 래그: 1st frame < 0.3% (FB 적용 후)
  2. 노이즈: AD71124 ≤ 600 e⁻, AFE2256 ≤ 280 e⁻ (시스템 레벨)
  3. 비균일성: 보정 후 잔류 < 1% (σ/mean)
  4. 결함 화소: 검출률 > 99%, 보간 MSE < 5%
  5. 프레임 레이트: 목표 fps의 100% 달성
  6. 전하 전송: > 99.3% (= 5×τ_TFT 조건)
  7. CIC 보상: 다이나믹 레인지 > 90% (AFE2256)
  8. 타이밍: 모든 셋업/홀드 타임 충족 (STA 통과)
  9. 전원 시퀀스: VGL→VGH 순서 준수, Inrush < 3A
  10. 에러 복구: 3회 이내 자동 복구 성공률 > 95%
```

---


### 13.5 조합별 상세 타이밍 검증 매트릭스

| 검증 항목 | C1 | C2 | C3 | C4 | C5 | C6 | C7 | 판정 기준 |
|----------|----|----|----|----|----|----|----|-----------| 
| T_gate_on ≥ 5×τ_TFT | 25µs | 25µs | 25µs | 25µs | 25µs | 25µs | 25µs | ≥ 15µs |
| T_line ≥ AFE min | 32µs | 70µs | 32µs | 32µs | 32µs | 10µs | 10µs | 데이터시트 준수 |
| 30fps 달성 | N/A | N/A | N/A | N/A | N/A | ✓ | ✓ | T_frame ≤ 33.3ms |
| CDS 완전 정착 | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | 잔차 < 0.1% |
| SYNC 스큐 | N/A | N/A | N/A | N/A | N/A | <7ns | <7ns | < 1 MCLK/4 |
| 전원 시퀀스 | 순방향 | 순방향 | 순방향 | 순방향 | 순방향 | 순방향 | 순방향 | PG 확인 |
| 래그 보정 잔차 | <0.3% | <0.3% | <0.3% | <0.3% | <0.3% | <0.3% | <0.3% | 1차 프레임 |
| 다크 보정 잔차 | <0.5% | <0.5% | <0.5% | <0.5% | <0.5% | <0.5% | <0.5% | 균일성 |
| 게인 보정 잔차 | <1.0% | <1.0% | <1.0% | <1.0% | <1.0% | <1.0% | <1.0% | 균일성 |

### 13.6 리스크 매트릭스

| 리스크 | 영향도 | 발생 확률 | 대응 방안 |
|--------|--------|----------|-----------|
| 30fps T_line 미달 (C6/C7) | 높음 | 중간 | 비닝 모드 대체, AFE 파이프라인 모드 활성화 |
| 멀티-AFE SYNC 스큐 초과 | 높음 | 낮음 | IDELAY2 위상 보정, PCB 트레이스 매칭 강화 |
| 다크 전류 온도 드리프트 | 중간 | 높음 | 구역별 온도 센서 4개, 자동 다크 맵 업데이트 |
| 래그 보정 비선형 실패 | 중간 | 중간 | NLCSC 알고리즘 추가, FB 하드웨어 우선 적용 |
| FPGA 리소스 부족 | 중간 | 낮음 | Kintex UltraScale+ 선정, 보정 파이프라인 시분할 |
| Gate IC Vth 열화 | 낮음 | 낮음 | 주기적 Gate 전압 캘리브레이션, ONA 리셋 활용 |
| DDR4 대역폭 병목 | 높음 | 낮음 | 트리플 버퍼링, 64-bit 인터페이스, 2400 MT/s |
| EMI 규격 위반 | 중간 | 중간 | 슬루율 제한, 차폐, LVDS 활용 |

### 13.7 개발 단계별 검증 체크리스트

#### Phase 1: 모듈 단위 시뮬레이션

```
□ spi_slave_if: SPI Mode 0/3 읽기/쓰기 100% 통과
□ reg_bank: 전체 레지스터 읽기/쓰기 검증
□ panel_ctrl_fsm: 5개 모드(STATIC/CONTINUOUS/TRIGGERED/DARK/RESET) 상태 전이 검증
□ gate_nv1047: SD1/CLK 시퀀스 NV1047 데이터시트 타이밍 일치 확인
□ gate_nt39565d: STV/CPV 시퀀스 NT39565D 데이터시트 타이밍 일치 확인
□ afe_ad711xx: ACLK/SYNC 타이밍 AD71124 데이터시트 일치 확인
□ afe_afe2256: MCLK/SYNC/FCLK 타이밍 AFE2256 데이터시트 일치 확인
□ line_data_rx: 알려진 패턴에 대해 100% 역직렬화 검증
□ prot_mon: 타임아웃, 에러 플래그 동작 검증
□ power_sequencer: 정상/비정상 시퀀스 검증
```

#### Phase 2: 통합 시뮬레이션

```
□ C1 (NV1047+AD71124): 패널 에뮬레이터 연동, 1프레임 완전 캡처
□ C3 (NV1047+AFE2256): CIC 보상 포함 캡처 검증
□ C6 (NT39565D+AD71124×12): 12-AFE 동기 캡처, 데이터 정합성
□ 연속 모드: 100프레임 연속 캡처, 데이터 무결성
□ 래그 보정 파이프라인: 알려진 래그 입력에 대한 보정 정확도
□ 보정 파이프라인: 다크+게인+결함화소 보정 end-to-end 검증
□ 이중 이득 합성: HG/LG 전환 정확성
□ 비닝 모드: 2×2 비닝 데이터 정확성
```

#### Phase 3: FPGA 보드 레벨 검증 (HIL)

```
□ SPI 통신: MCU ↔ FPGA 레지스터 읽기/쓰기
□ 전원 시퀀싱: 오실로스코프로 실제 전원 파형 검증
□ Gate IC 타이밍: 로직 분석기로 실제 Gate 파형 캡처
□ AFE SPI 초기화: AFE 레지스터 설정값 리드백 확인
□ LVDS 데이터: Eye diagram 측정 (> 60% eye opening)
□ 1프레임 캡처: 실제 패널 연결, 다크 프레임 캡처
□ Flat Field 캡처: X-ray 조사 하 균일 이미지 확인
□ 래그 측정: FSRF 프로토콜로 실측 래그 정량화
□ 온도 캘리브레이션: 온도 챔버에서 다크 보정 검증
□ EMI 측정: 전도/방사 EMI 사전 측정
```

### 13.8 문서 이력

| 버전 | 날짜 | 변경 내용 | 작성자 |
|------|------|----------|--------|
| 0.1 | 2026-03-01 | 초안 작성, 조합 매트릭스 정의 | FPGA 설계팀 |
| 0.5 | 2026-03-10 | 물리 특성, Gate IC, AFE 알고리즘 추가 | FPGA 설계팀 |
| 0.8 | 2026-03-15 | 래그 보정, 보정 파이프라인 상세화 | FPGA 설계팀 |
| 1.0 | 2026-03-18 | 최종 검토, 멀티-AFE 섹션, 검증 계획 완성 | FPGA 설계팀 |

---

## 참고 문헌

1. **Starman, J. et al.** "A forward bias method for lag correction of an a-Si flat panel detector." *Medical Physics*, 2011. [PMC3257750](https://pmc.ncbi.nlm.nih.gov/articles/PMC3257750/)
2. **Starman, J. et al.** "A nonlinear lag correction algorithm for a-Si flat-panel x-ray detectors." *Medical Physics*, 2012. [PMC3465354](https://pmc.ncbi.nlm.nih.gov/articles/PMC3465354/)
3. **Nathan, A. et al.** "Amorphous silicon detector and TFT technology for large-area X-ray imaging." *Microelectronics Journal*, 2000. [PDF](https://www.fisica.unisa.it/antonio.dibartolomeo/MaterialeDidatticoFisicaSemiconduttori/Articoli/A-Si_andTFTtechnology%20for%20Xray%20detection.pdf)
4. **Liu, T.** "Stability of Amorphous Silicon Thin Film Transistors." *Princeton University PhD Thesis*, 2013. [PDF](https://swh.princeton.edu/~sturmlab/theses/Ting_Liu_Thesis_Part1.pdf)
5. **Hoheisel, M.** "Amorphous Silicon X-Ray Detectors." *ISCMP Proceedings*, 1996. [PDF](https://www.mhoheisel.de/docs/ISCMP91996112.pdf)
6. **Hsieh, J.** "Adaptive streak artifact reduction in CT." *US Patent 5249123A*. [Google Patents](https://patents.google.com/patent/US5249123A)
7. **Carestream Health.** "Dark correction for digital X-ray detector." *EP Patent 2148500A1*. [Google Patents](https://patents.google.com/patent/EP2148500A1/en)
8. **GE Medical Systems.** "FPD refresh/prepare method." *US Patent 5452338A*. [Google Patents](https://patents.google.com/patent/US5452338A)
9. **TI.** "AFE2256 Datasheet — 256-channel X-ray FPD AFE." [TI Product Page](https://www.ti.com/product/AFE2256)
10. **Analog Devices.** "ADAS1256 (AD71124) Product Page." [ADI](https://www.analog.com/en/products/adas1256.html)
11. **TFT Charge Injection Compensation.** *US Patent 20150256765A1*. [Google Patents](https://patents.google.com/patent/US20150256765A1/en)
12. **GE Healthcare.** "Automatic identification and correction of bad pixels." *US Patent 5657400A*. [Google Patents](https://patents.google.com/patent/US5657400A/en)
13. **AMD/Xilinx.** "ISERDESE2 Primitive." *UG953*. [AMD Docs](https://docs.amd.com/r/en-US/ug953-vivado-7series-libraries/ISERDESE2)
14. **저노이즈 ROIC CDS 연구.** "AICDS noise reduction to 18.3 e⁻." 2024. [Link](https://www.researching.cn/articles/OJ161cc11982f75edb)
15. **ADI Medical X-ray Solutions.** [PDF](https://www.analog.com/media/cn/technical-documentation/apm-pdf/adi-medical-x-ray-imaging-solutions_en.pdf)
16. **Tredwell, T.** "Flat-Panel Imaging Arrays for Digital Radiography." *IISW*, 2009. [PDF](https://www.imagesensors.org/Past%20Workshops/2009%20Workshop/2009%20Papers/065_paper_tredwell_invited.pdf)

---

*문서 끝 — FPD-FPGA-DRV-001 v1.0*  
*작성: 2026-03-18*
