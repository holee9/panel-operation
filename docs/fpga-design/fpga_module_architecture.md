# FPGA 모듈 체계 — X-ray Detector 구동 알고리즘 기반 설계

## 1. 부품 조합 매트릭스

방금 분석한 데이터시트를 기반으로 실제 존재하는 구동 조합을 정의한다.

### 1.1 구성 부품 목록

| 역할 | 부품 | 제조사 | 주요 특성 |
|------|------|--------|-----------|
| 패널 (소형) | R1714AS08.0 | AUO | 17×14인치, a-Si TFT |
| 패널 (정방형 소) | R1717AS01.3 | AUO | 17×17인치, a-Si TFT |
| 패널 (정방형 대) | X239AW1-102 | InnoLux | 43×43cm, 3072×3072 px, 140μm pitch |
| 패널 (구형) | 1717 | InnoLux | 17×17인치, a-Si TFT, Preliminary |
| Gate IC (소채널) | NV1047 | New Vision | 256/263/300ch, +35V/-15V, 200kHz |
| Gate IC (대채널) | NT39565D | Novatek | 541/513/385/361ch, +40V, 200kHz |
| AFE/ROIC (ADI 고범위) | AD71124 | ADI | 256ch, 16bit, 최대 32pC, tLINE=22μs |
| AFE/ROIC (ADI 저전력) | AD71143 | ADI | 256ch, 16bit, 최대 16pC, tLINE=60μs |
| AFE/ROIC (TI 저노이즈) | AFE2256 | TI | 256ch, 16bit, 240e⁻ rms, 내장 ADC |

### 1.2 유효 조합 매트릭스

```
조합ID  패널            Gate IC     AFE/ROIC    용도
─────────────────────────────────────────────────────────────────
C1     1717/R1717      NV1047      AD71124     표준 정지상 (17×17)
C2     1717/R1717      NV1047      AD71143     저전력 17×17 (배터리/모바일)
C3     1717/R1717      NV1047      AFE2256     고화질 17×17 (저노이즈)
C4     R1714           NV1047      AD71124     비정방형 17×14
C5     R1714           NV1047      AFE2256     고화질 17×14
C6     X239AW1-102     NT39565D    AD71124×N   대형 43×43 (다중 AFE)
C7     X239AW1-102     NT39565D    AFE2256×N   대형 43×43 고화질
```

> 43×43 패널(X239AW1-102)은 3072 데이터라인 → AFE256ch 기준 12개 이상 필요.
> Gate IC도 3072 gate line → NT39565D(541ch) × 6개 필요.

---

## 2. 조합별 핵심 타이밍 파라미터

### 2.1 Gate IC 제어 파라미터

| 파라미터 | NV1047 | NT39565D |
|----------|--------|----------|
| 채널수 (기본) | 300 | 541 |
| Gate-ON 전압 | +35V max | +40V max |
| Gate-OFF 전압 | -15V max | -15V (VEE) |
| 최대 스캔 클럭 | 200kHz | 200kHz |
| 인터페이스 | SD1/SD2, CLK, L/R, OE | STV1/2, CPV, LR, OE1/OE2 |
| 스캔 방향 | 양방향 (L/R핀) | 양방향 (LR핀) |
| 인에이블 방식 | OE(H→VEE), ONA(L→전체ON) | OE1/OE2(채널별 분리) |
| 채널 선택 모드 | MD[1:0] (4종) | MODE1/MODE2 + CHIP_SEL |

### 2.2 AFE/ROIC 제어 파라미터

| 파라미터 | AD71124 | AD71143 | AFE2256 |
|----------|---------|---------|---------|
| 채널수 | 256 | 256 | 256 |
| 분해능 | 16bit | 16bit | 16bit (내장 SAR ADC) |
| 최소 라인타임 | 22μs | 60μs | 51.2μs |
| 풀스케일 범위 | 0.5~32pC | 0.5~16pC | 0.6~9.6pC |
| 노이즈 | 585e⁻ rms | 580e⁻ rms | 240e⁻ rms |
| 출력 인터페이스 | LVDS (DOUT A/B) | LVDS (DOUT A/B) | LVDS (DOUT) |
| 데이터 클럭 | DCLKH/DCLKL | DCLKH/DCLKL | DCLK_P/M |
| 설정 인터페이스 | SPI (SCK/SDI/SDO/CS) | SPI (SCK/SDI/SDO/CS) | SPI (SCLK/SDATA/SDOUT/SEN) |
| 타이밍 시퀀서 | 내장 (ACLK 기반) | 내장 (ACLK 기반) | 내장 TG (MCLK 기반) |
| 전하 보상 | 없음 | 없음 | 있음 (CIC 기능) |
| 전원 | 2.5V/5V | 2.5V/5V | 1.85V/3.3V |

### 2.3 조합별 FPGA 구동 알고리즘 차이점

| 조합 | Gate IC 제어 | AFE 제어 | 특이사항 |
|------|-------------|----------|----------|
| C1 (NV1047+AD71124) | SD1→row_clk, OE 토글 | ACLK 공급 + SPI 설정 | 표준 베이스라인 |
| C2 (NV1047+AD71143) | 동일 | ACLK 공급 + tLINE≥60μs | 라인 타임 제약 더 큼 |
| C3 (NV1047+AFE2256) | 동일 | MCLK 공급 + SYNC | CIC 보상 레지스터 설정 추가 |
| C6 (NT39565D+AD71124×N) | STV, CPV, OE1/OE2 다중 | 다중 AFE ACLK 동기화 | SYNC 멀티-AFE 데이지체인 |
| C7 (NT39565D+AFE2256×N) | 동일 | 다중 AFE MCLK/SYNC | TP_SEL 신호 추가 |

---

## 3. FPGA 모듈 계층 구조

### 3.1 최상위 구조도

```
┌─────────────────────────────────────────────────────────────────┐
│                      fpga_top (Top-Level)                       │
│                                                                 │
│  ┌──────────────┐   ┌─────────────────────────────────────────┐ │
│  │  spi_slave_if│   │         detector_core                   │ │
│  │  (MCU↔FPGA) │──▶│                                         │ │
│  └──────────────┘   │  ┌────────────┐  ┌───────────────────┐ │ │
│                      │  │ reg_bank   │  │ panel_ctrl_fsm    │ │ │
│  ┌──────────────┐   │  │ (설정/상태)│  │ (메인 구동 FSM)  │ │ │
│  │  clk_rst_mgr │──▶│  └────────────┘  └───────────────────┘ │ │
│  └──────────────┘   │                           │             │ │
│                      │           ┌───────────────┼──────────┐ │ │
│                      │           ▼               ▼          ▼ │ │
│                      │  ┌──────────────┐ ┌────────────┐ ┌──────┐│ │
│                      │  │gate_ic_driver│ │afe_ctrl_if │ │prot_│ │ │
│                      │  │ (NV1047 /    │ │(AD71124/   │ │mon  │ │ │
│                      │  │  NT39565D)   │ │ AD71143/   │ │     │ │ │
│                      │  └──────────────┘ │ AFE2256)   │ └──────┘│ │
│                      │         │         └────────────┘   │    │ │
│                      │         │                │          │    │ │
│                      │         ▼                ▼          │    │ │
│                      │  ┌──────────────┐ ┌────────────┐   │    │ │
│                      │  │ row_scan_eng │ │line_data_rx│   │    │ │
│                      │  └──────────────┘ └─────┬──────┘   │    │ │
│                      │                         │           │    │ │
│                      │                   ┌─────▼──────┐   │    │ │
│                      │                   │line_buf_ram│   │    │ │
│                      │                   └─────┬──────┘   │    │ │
│                      └─────────────────────────┼──────────┘    │ │
│                                                │                │
│  ┌──────────────┐                              ▼                │
│  │ mcu_data_if  │◀────────────────────── data_out_mux          │
│  │ (FPGA→MCU)  │                                               │
│  └──────────────┘                                               │
└─────────────────────────────────────────────────────────────────┘
```

### 3.2 모듈 목록 및 역할 정의

| 모듈명 | 계층 | 역할 | 조합 의존성 |
|--------|------|------|------------|
| `fpga_top` | L0 | 핀 매핑, 클럭 분배, 최상위 연결 | 조합별 별도 top 생성 |
| `spi_slave_if` | L1 | MCU SPI 슬레이브, 레지스터 R/W | 공통 |
| `clk_rst_mgr` | L1 | 클럭 분배(ACLK/MCLK/DCLK), 리셋 동기화 | AFE 종류에 따라 클럭 주파수 변경 |
| `reg_bank` | L1 | 설정/상태 레지스터 파일 (32개 이내) | 공통 (조합별 일부 레지스터 다름) |
| `panel_ctrl_fsm` | L1 | 메인 구동 FSM (IDLE/RESET/INTEG/READOUT/DONE) | 공통 구조, 타이밍값만 다름 |
| `gate_ic_driver` | L2 | Gate IC 종류별 시퀀스 출력 | **NV1047** / **NT39565D** 각각 구현 |
| `row_scan_eng` | L2 | 행 인덱스 카운터, Gate ON/OFF 타이밍 생성 | 공통 (최대 row수만 파라미터) |
| `afe_ctrl_if` | L2 | AFE 종류별 타이밍 제어 | **AD71124** / **AD71143** / **AFE2256** 각각 구현 |
| `line_data_rx` | L2 | LVDS 수신, 비트 역직렬화, 라인 완료 감지 | ADI LVDS / TI LVDS (포맷 다름) |
| `prot_mon` | L2 | 과노출 타임아웃, 에러 플래그, 강제 gate-off | 공통 |
| `line_buf_ram` | L2 | Block RAM 기반 라인 버퍼 (1라인 분량) | 픽셀수(컬럼수)만 파라미터 |
| `data_out_mux` | L2 | 라인 데이터 → MCU 전송용 버스 정렬 | 공통 |
| `mcu_data_if` | L1 | MCU 데이터 전송 인터페이스 (병렬/SPI 선택) | 공통 |

---

## 4. 핵심 모듈 상세 설계

### 4.1 `panel_ctrl_fsm` — 메인 구동 FSM

```
               ┌────────────────────────────────────────────┐
               │          panel_ctrl_fsm 상태도              │
               └────────────────────────────────────────────┘

   MCU: START=0           MCU: START=1
   ┌────────┐           ┌────────────┐
   │  IDLE  │──────────▶│   RESET    │
   └────────┘           └─────┬──────┘
       ▲                      │ T_reset 카운트 완료
       │                      ▼
       │              ┌──────────────┐
       │              │  INTEGRATE   │
       │              └──────┬───────┘
       │                     │ X-ray trigger OR T_integ 완료
       │                     ▼
       │           ┌──────────────────┐
       │           │ READOUT_INIT     │ (AFE SPI 설정, ACLK/MCLK 공급 시작)
       │           └────────┬─────────┘
       │                    │ AFE 준비완료
       │                    ▼
       │           ┌──────────────────┐◀──── 행 반복
       │           │  SCAN_LINE[row]  │
       │           │ Gate_ON → AFE    │
       │           │ Sample → ADC →   │
       │           │ LineData → BufRam│
       │           └────────┬─────────┘
       │                    │ row == max_row?
       │                    ▼ Yes
       │           ┌──────────────────┐
       │           │  READOUT_DONE    │ (ACLK/MCLK 중지, STATUS 업데이트)
       │           └────────┬─────────┘
       │                    │
       │                    ▼
       │           ┌──────────────────┐
       └───────────│      DONE        │──▶ MCU 인터럽트 발생
                   └──────────────────┘

   ERROR 조건 (어느 상태에서나) ──▶ IDLE + error_flag 세트
```

**FSM 구동 모드 레지스터 (REG_MODE[2:0])**

| 값 | 모드명 | 설명 |
|----|--------|------|
| 000 | STATIC | 단일 프레임 획득 후 DONE |
| 001 | CONTINUOUS | DONE→RESET 자동 반복 |
| 010 | TRIGGERED | X-ray 외부 트리거 대기 후 획득 |
| 011 | DARK_FRAME | Gate off 유지, AFE 리드아웃만 (offset 캘리브레이션용) |
| 100 | RESET_ONLY | 패널 리셋만 실행 (초기화 전용) |

---

### 4.2 `gate_ic_driver` — Gate IC 종류별 모듈

#### NV1047 드라이버 (`gate_nv1047`)

```
입력: row_index[8:0], gate_on_pulse, scan_dir, reset_all
출력: SD1, SD2, CLK, OE, ONA, L/R, RST, MD[1:0]

동작:
  1. L/R = scan_dir (0=상향, 1=하향)
  2. CLK 에지마다 SD1에 row_index에 해당하는 비트 시프트
  3. gate_on_pulse 구간: OE=0 (출력 활성)
  4. gate_on_pulse 후: OE=1 (전체 VEE)
  5. reset_all=1: ONA=0 (전체 VGG 강제)

타이밍 파라미터 (레지스터):
  T_clk_period   : CLK 주기 (기본 5us, 최소 5us = 200kHz)
  T_gate_on      : OE=0 유지 시간 (단위: clk)
  T_gate_settle  : CLK stop → Gate 안정화 대기 (단위: clk)
```

#### NT39565D 드라이버 (`gate_nt39565d`)

```
입력: row_index[9:0], gate_on_pulse, scan_dir, chip_sel[1:0], mode[1:0]
출력: STV1L, STV2L, STV1R, STV2R, CPV_L, CPV_R, LR, OE1_L, OE1_R, OE2_L, OE2_R

동작:
  1. STV1/STV2: 스타트 펄스 생성 (2G 모드 시 두 펄스 교번)
  2. CPV: 픽셀 클럭 (row 선택 진행 클럭)
  3. OE1/OE2: 홀수/짝수 채널 분리 제어
  4. CHIP_SEL[1:0]: 채널 수 선택 (Normal/2G/2G+LCS)

추가 파라미터:
  T_stv_pulse    : STV 펄스 폭 (Long=2T or Short=1T)
  T_cpv_period   : CPV 주기
  output_mode    : STV_MODE, SEL, UD 핀 설정 (2G+LCS 시)
```

---

### 4.3 `afe_ctrl_if` — AFE 종류별 모듈

#### AD71124/AD71143 제어 (`afe_ad711xx`)

```
입력: afe_start, config_done, line_idx[9:0]
출력: ACLK, RESET, IFS[5:0] (charge range), SYNC

내부 시퀀스:
  1. RESET 펄스 → AFE 초기화
  2. SPI 설정: IFS 값, LPF 시상수, 전력 모드 레지스터 작성
  3. ACLK 공급 시작 (내장 시퀀서 기동)
  4. SYNC 신호로 복수 AFE 동기화
  5. DOUT 유효 구간에 line_data_rx 활성화

AD71124 vs AD71143 차이:
  AD71124: tLINE_min=22μs, IFS 6bit(0~63), 전력 모드 4종
  AD71143: tLINE_min=60μs, IFS 5bit(0~31), 전력 모드 3종
  → 파라미터 레지스터 비트폭만 다른 동일 모듈 구조 가능 (generic 파라미터)
```

#### AFE2256 제어 (`afe_afe2256`)

```
입력: afe_start, config_done, tp_sel
출력: MCLK, SYNC, TP_SEL, FCLK_P/M (frame clock)

내부 시퀀스:
  1. SPI 설정: 전하 범위, 통합 모드(UP/DOWN), CIC 보상 파라미터
  2. MCLK 공급 시작
  3. SYNC로 내부 TG 기동
  4. TP_SEL: 타이밍 프로파일 선택 (integrate-up vs down)
  5. FCLK_P/M으로 프레임 동기
  6. DCLK_P/M 기반 LVDS 데이터 수신

추가 기능:
  CIC_EN 레지스터: Charge Injection Compensation 활성화
  CIC_PROFILE[3:0]: 동적 프로파일 전환 (Qv 보상 시)
  PIPELINE_EN: Integrate-and-Read 파이프라인 모드
```

---

### 4.4 `line_data_rx` — LVDS 데이터 수신

```
조합별 LVDS 포맷:

ADI (AD71124/AD71143):
  - LVDS 차동 쌍: DOUTAx, DOUTBx (채널별 2쌍)
  - 클럭: DCLKH(+), DCLKL(-)
  - 직렬 포맷: 16bit × 256ch, MSB first, straight binary
  - Self-clocked: DCLK 내부 생성 (ACLK 기반)

TI (AFE2256):
  - LVDS 차동 쌍: DOUT_P/M
  - 클럭: DCLK_P/M + FCLK_P/M
  - 직렬 포맷: 16bit × 256ch, 4-lane MUX 출력 (ADC[3:0])
  - FCLK: 프레임 동기

공통 수신 로직:
  1. LVDS → 싱글엔드 변환 (IBUFDS)
  2. 클럭 에지 동기화
  3. 16bit 시프트 레지스터 (per channel)
  4. 유효 채널 수(N_CH)만큼 병렬화
  5. line_valid 펄스 → line_buf_ram 쓰기
```

---

### 4.5 `reg_bank` — 레지스터 맵

```
주소  이름              비트폭  R/W  설명
─────────────────────────────────────────────────────────────────
0x00  REG_CTRL          8      R/W  [0]=START, [1]=ABORT, [2]=IRQ_EN
0x01  REG_STATUS        8      R    [0]=BUSY, [1]=DONE, [2]=ERROR, [3]=LINE_RDY
0x02  REG_MODE          4      R/W  구동 모드 선택 (5종)
0x03  REG_COMBO         4      R/W  부품 조합 선택 (C1~C7 인코딩)
0x04  REG_NROWS        12      R/W  유효 행 수 (최대 3072)
0x05  REG_NCOLS        12      R/W  유효 열 수 (최대 3072)
0x06  REG_TLINE        16      R/W  라인 타임 (단위: 10ns, 최소 22μs=2200)
0x07  REG_TRESET       16      R/W  리셋 시퀀스 시간 (단위: 10ns)
0x08  REG_TINTEG       24      R/W  적분 시간 (단위: 10ns)
0x09  REG_TGATE_ON     12      R/W  Gate ON 펄스 폭 (단위: clk)
0x0A  REG_TGATE_SETTLE  8      R/W  Gate 안정화 대기 (단위: clk)
0x0B  REG_AFE_IFS       6      R/W  AFE 풀스케일 코드 (AD711xx용)
0x0C  REG_AFE_LPF       4      R/W  LPF 시상수 코드
0x0D  REG_AFE_PMODE     2      R/W  AFE 전력 모드
0x0E  REG_CIC_EN        1      R/W  AFE2256 CIC 활성화
0x0F  REG_CIC_PROFILE   4      R/W  AFE2256 CIC 프로파일
0x10  REG_SCAN_DIR      1      R/W  스캔 방향 (0=정방향, 1=역방향)
0x11  REG_GATE_SEL      2      R/W  Gate IC 채널 선택 모드 (MD[1:0]/MODE)
0x12  REG_AFE_NCHIP     4      R/W  AFE 체인 수 (다중 AFE 시)
0x13  REG_SYNC_DLY      8      R/W  AFE SYNC 딜레이 조정
0x14  REG_LINE_IDX     12      R    현재 스캔 중인 행 인덱스 (읽기전용)
0x15  REG_ERR_CODE      8      R    에러 코드
0x1F  REG_VERSION       8      R    FPGA 펌웨어 버전
```

---

### 4.6 조합별 `fpga_top` 설정 차이

조합 선택은 REG_COMBO 레지스터로 런타임 변경 가능하도록 설계하되,
Gate IC와 AFE 핀 인터페이스 자체가 다르므로 **물리 핀 매핑은 top 레벨에서 고정**.

```
// 조합 C1: NV1047 + AD71124
fpga_top_c1.sv  →  gate_nv1047 + afe_ad711xx (IFS_MAX=6bit)

// 조합 C3: NV1047 + AFE2256
fpga_top_c3.sv  →  gate_nv1047 + afe_afe2256

// 조합 C6: NT39565D + AD71124×N
fpga_top_c6.sv  →  gate_nt39565d + afe_ad711xx × N (SYNC 멀티-AFE)

// 공통 코어는 모두 동일 (detector_core.sv)
// 조합별 차이는 gate_ic_driver + afe_ctrl_if + 핀 매핑만
```

---

## 5. 모듈 의존성 다이어그램

```
fpga_top_cX.sv
    ├── spi_slave_if.sv         [공통]
    ├── clk_rst_mgr.sv          [공통, 파라미터: ACLK_HZ/MCLK_HZ]
    ├── reg_bank.sv             [공통]
    ├── panel_ctrl_fsm.sv       [공통]
    │       ├── gate_ic_driver  [선택: gate_nv1047 / gate_nt39565d]
    │       │       └── row_scan_eng.sv  [공통]
    │       ├── afe_ctrl_if     [선택: afe_ad711xx / afe_afe2256]
    │       │       └── line_data_rx.sv  [공통, 파라미터: ADI_MODE/TI_MODE]
    │       │               └── line_buf_ram.sv  [공통]
    │       └── prot_mon.sv     [공통]
    ├── data_out_mux.sv         [공통]
    └── mcu_data_if.sv          [공통]
```

**의존 규칙:**
- `panel_ctrl_fsm`은 gate/afe 모듈에 타이밍 신호만 내려줌 (데이터 경로 미포함)
- `afe_ctrl_if`는 LVDS 데이터를 직접 받지 않고 `line_data_rx`에 위임
- 모든 타이밍 파라미터는 `reg_bank`에서 읽어옴 (하드코딩 금지)
- `spi_slave_if`와 `mcu_data_if`는 `reg_bank`/`line_buf_ram`에만 접근

---

## 6. 구동 알고리즘별 타이밍 플로우

### 6.1 표준 정지상 획득 (조합 C1: NV1047 + AD71124)

```
MCU                    FPGA                  Gate NV1047         AFE AD71124
 │                      │                        │                    │
 │─ REG_MODE=STATIC ──▶│                        │                    │
 │─ REG_TINTEG 설정 ──▶│                        │                    │
 │─ REG_CTRL[START] ──▶│                        │                    │
 │                      │─ RESET 시퀀스 ────────▶│ ONA=L (전체 VGG)  │
 │                      │─ (T_RESET 카운트) ────▶│ ONA=H 복귀        │
 │                      │                        │                    │
 │                      │─ AFE SPI 초기화 ───────────────────────────▶│ IFS/LPF 설정
 │                      │                        │                    │
 │         X-ray 조사  │─ INTEGRATE 진입         │ OE=1 (Gate OFF)   │
 │                      │─ (T_INTEG 카운트)       │                    │
 │                      │                        │                    │
 │                      │─ READOUT_INIT ─────────────────────────────▶│ ACLK 공급
 │                      │                        │                    │
 │                      │─ row=0 ──────────────▶│ CLK+SD1 시프트     │
 │                      │                        │ OE=0 (Gate ON, row0)│
 │                      │─ AFE SYNC ─────────────────────────────────▶│ CDS 시작
 │                      │  (T_LINE=22~32μs)       │                    │ ADC 변환
 │                      │◀── LVDS DOUT valid ─────────────────────────│
 │                      │─ 라인0 → BufRAM         │                    │
 │                      │─ BufRAM → MCU 전송       │                    │
 │                      │                        │                    │
 │                      │─ row=1 ... row=N-1  (반복)                  │
 │                      │                        │                    │
 │                      │─ READOUT_DONE ──────────────────────────────▶│ ACLK 중지
 │◀── IRQ ─────────────│─ REG_STATUS[DONE]=1     │                    │
 │                      │                        │                    │
```

### 6.2 연속 형광 모드 (조합 C3: NV1047 + AFE2256)

```
특이점:
1. DONE → RESET 자동 루프 (REG_MODE=CONTINUOUS)
2. AFE2256 Integrate-and-Read 파이프라인:
   - row[n] Gate ON + Integrate 동안 row[n-1] ADC 읽기 가능
   - PIPELINE_EN=1 설정 시 throughput 향상
3. CIC 활성화 (Gate-on 시 Cgd 전하 주입 보상)
   - TFT_charge_injection_compensation_ppt 기반:
     CIC가 QTFT=~2pC 자동 보상 → 동적범위 92% 활용
4. MCLK 공급은 연속 모드 중 항상 ON
```

### 6.3 대형 패널 멀티 AFE (조합 C6: NT39565D + AD71124×12)

```
특이점:
1. NT39565D STV 펄스 → 3072 gate line 스캔
2. AFE 12개 SYNC 동기화:
   - 첫 번째 AFE에서 SYNC 생성
   - 나머지 AFE는 daisy-chain SDO→SDI 또는 SYNC 브로드캐스트
3. LVDS 데이터 수신: 12× line_data_rx 인스턴스
4. line_buf_ram: 3072픽셀 × 16bit = 6KB/라인
5. MCU 전송: 라인 완료마다 burst transfer
```

---

## 7. SystemVerilog 파라미터 설계 원칙

모든 모듈은 아래 파라미터를 최상위에서 override 가능하도록 설계:

```systemverilog
// detector_core.sv 파라미터 예시
module detector_core #(
    // 부품 조합 선택
    parameter GATE_IC_TYPE  = "NV1047",    // "NV1047" / "NT39565D"
    parameter AFE_TYPE      = "AD71124",   // "AD71124" / "AD71143" / "AFE2256"
    // 패널 해상도
    parameter MAX_ROWS      = 2048,
    parameter MAX_COLS      = 2048,
    parameter N_AFE_CHIPS   = 1,           // AFE 체인 수
    // 클럭
    parameter SYS_CLK_HZ    = 100_000_000,
    parameter AFE_CLK_HZ    = 10_000_000,  // ACLK/MCLK
    // 최소 타이밍 제약 (ns) — 데이터시트 기반
    parameter TLINE_MIN_NS  = 22_000,      // AD71124: 22μs / AD71143: 60μs / AFE2256: 51200ns
    parameter TRESET_MIN_NS = 1_000_000    // 1ms
) (
    ...
);
```

---

## 8. 개발 우선순위 및 구현 단계

| 단계 | 모듈 | 조합 | 검증 방법 |
|------|------|------|-----------|
| Phase 1 | spi_slave_if + reg_bank + panel_ctrl_fsm(FSM만) | 공통 | ModelSim/Questa 단위 TB |
| Phase 2 | gate_nv1047 + row_scan_eng | C1~C5 | NV1047 타이밍 다이어그램 기반 TB |
| Phase 3 | afe_ad711xx + line_data_rx(ADI) + line_buf_ram | C1, C2 | AD71124 스펙 기반 LVDS 모델 TB |
| Phase 4 | 통합 C1 (NV1047+AD71124) end-to-end | C1 | 패널 에뮬레이터 연동 통합 TB |
| Phase 5 | afe_afe2256 + CIC 보상 로직 | C3 | AFE2256 TG 타이밍 프로파일 TB |
| Phase 6 | gate_nt39565d + 멀티 AFE SYNC | C6, C7 | 대형 패널 에뮬레이터 TB |
| Phase 7 | HIL (실제 보드 포팅) | C1 우선 | 실측 데이터 비교 |
