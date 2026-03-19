# Implementation Plan: X-ray FPD FPGA Control System

## Overview

2단계 구현 전략:
- **v1**: FPGA 내부 리소스만 사용 (BRAM only, 외부 메모리 없음) — 핵심 구동 + 데이터 수집
- **v2**: 외부 메모리(SRAM/DDR) 추가 — 보정 파이프라인 + 래그 보정 + 프레임 버퍼

### Target Device & Toolchain

- **FPGA**: xc7a35tfgg484-1 (Artix-7 35T, FGG484, -1)
- **Toolchain**: Vivado 2025.2
- **Resources**: 33,280 LC, 90 DSP, 50 BRAM36K, 250 I/O, 5 MMCM

### Device Architecture: 24-AFE Direct LVDS

- 모든 AFE LVDS 출력이 FPGA에 직접 연결 (외부 MUX 없음)
- Per AFE: 3 LVDS pairs (6 pins), 24 AFE = 72 pairs = 144 pins
- 브로드캐스트 SYNC/ACLK/MCLK + SPI 데이지체인

---

# v1: BRAM Only (외부 메모리 없음)

핵심 목표: **패널 구동 → AFE 제어 → LVDS 수신 → MCU 전송** 데이터 경로 확립.
보정(Offset/Gain/Defect)과 래그 보정은 MCU/PC에서 소프트웨어로 처리.

### v1 BRAM Budget (50 BRAM36K available)

| Use | BRAM36K | Notes |
|-----|---------|-------|
| line_buf_ram (1 line, ping-pong) | 4 | 2048×16bit × 2 banks |
| LVDS async FIFO (per AFE CDC) | 2-4 | 소형 FIFO per AFE group |
| SPI/Register | 0 | CLB distributed RAM |
| **v1 Total** | **~6-8** | **of 50 available** |
| **Remaining** | **42-44** | 여유 (향후 v2 확장용) |

### v1 Data Path

```
Gate IC → Panel → AFE (charge) → LVDS → FPGA line_buf_ram → data_out_mux → MCU
                                                                              │
                                                                MCU/PC에서 소프트웨어 보정
                                                              (Offset, Gain, Defect, Lag)
```

---

## v1 Phase 1: Foundation — SPI & Register & Clock (2주)

**목표**: MCU 통신 확립, 레지스터 액세스, 클럭 분배

| Module | Type | Description | Priority |
|--------|------|-------------|----------|
| `spi_slave_if.sv` | Common | MCU SPI 슬레이브, Mode 0/3, 1-10MHz | P0 |
| `reg_bank.sv` | Common | 32개 레지스터 파일 (0x00-0x1F) | P0 |
| `clk_rst_mgr.sv` | Common | ACLK/MCLK 생성 (MMCM), 리셋 동기화 | P0 |
| `fpd_types_pkg.sv` | Package | 전역 타입 정의 | P0 |
| `fpd_params_pkg.sv` | Package | 조합별 파라미터 패키지 | P0 |

**검증**: SPI R/W, 레지스터 Default, ACLK/MCLK 주파수 ±1%
**의존성**: 없음
**Blocking**: 모든 후속 모듈

---

## v1 Phase 2: FSM Core — State Machine (1주)

**목표**: 메인 구동 FSM (하위 모듈 스텁)

| Module | Type | Description | Priority |
|--------|------|-------------|----------|
| `panel_ctrl_fsm.sv` | Common | 6-state FSM + 5 동작 모드 | P0 |

**FSM States**: INIT → IDLE → CALIBRATE → ACQUIRE → DONE → ERROR
**검증**: 상태 전이, 타이밍 카운터, 모드 전환, ERROR 복귀
**의존성**: v1 Phase 1

---

## v1 Phase 3: Gate IC Drivers (2-3주)

**목표**: Gate IC 제어, 양방향 행 스캔

| Module | Type | Description | Priority |
|--------|------|-------------|----------|
| `gate_nv1047.sv` | Combo | SD1/SD2 시프트, OE/ONA, L/R | P1 |
| `gate_nt39565d.sv` | Combo | 듀얼 STV, CPV, OE1/OE2, 캐스케이드 | P2 |
| `row_scan_eng.sv` | Common | 행 인덱스 카운터, Gate ON/OFF 타이밍 | P1 |

**검증**: 데이터시트 타이밍 대조, 양방향 스캔
**의존성**: v1 Phase 1

---

## v1 Phase 4: AFE Control (2-3주)

**목표**: AFE SPI 초기화, SYNC/ACLK/MCLK 생성

| Module | Type | Description | Priority |
|--------|------|-------------|----------|
| `afe_ad711xx.sv` | Combo | ACLK, SPI 설정, SYNC (AD71124/AD71143) | P1 |
| `afe_afe2256.sv` | Combo | MCLK, SYNC, CIC 프로파일, TP_SEL | P1 |

**검증**: SPI 레지스터, ACLK/MCLK 주파수, SYNC 정렬
**의존성**: v1 Phase 1 (클럭), v1 Phase 3 (Gate 타이밍)

---

## v1 Phase 5: LVDS Data Reception + Line Buffer (1-2주)

**목표**: LVDS 역직렬화, BRAM 라인 버퍼, MCU 전송

| Module | Type | Description | Priority |
|--------|------|-------------|----------|
| `line_data_rx.sv` | Common | LVDS 수신 (IBUFDS→ISERDESE2), per-AFE 인스턴스 | P1 |
| `line_buf_ram.sv` | Common | BRAM ping-pong 라인 버퍼 | P1 |
| `data_out_mux.sv` | Common | 라인 데이터 → MCU 버스 정렬 | P1 |
| `mcu_data_if.sv` | Common | MCU 데이터 전송 (병렬/SPI) | P1 |

**BRAM 사용**: ping-pong 1-line buffer (~4 BRAM36K)
**검증**: LVDS 루프백, BER, 라인 완료 감지
**의존성**: v1 Phase 4

---

## v1 Phase 6: Safety Systems (1-2주)

**목표**: 전원 시퀀싱, 비상 정지, 과노출 보호

| Module | Type | Description | Priority |
|--------|------|-------------|----------|
| `prot_mon.sv` | Common | 과노출 타임아웃 (5초), 에러 플래그 | P0 |
| `emergency_shutdown.sv` | Common | 과전압/과온도/PLL 실패 감지 | P0 |
| `power_sequencer.sv` | Common | M0-M5 모드 전이 | P1 |

**의존성**: v1 Phase 1, Phase 3-4와 병렬 가능

---

## v1 Phase 7: Integration & System Test (2-3주)

**목표**: 전체 모듈 통합, fpga_top 생성, end-to-end 검증

| Module | Type | Description | Priority |
|--------|------|-------------|----------|
| `fpga_top_c1.sv` | Top | C1: NV1047 + AD71124 (reference) | P1 |
| `fpga_top_c3.sv` | Top | C3: NV1047 + AFE2256 | P2 |
| `fpga_top_c6.sv` | Top | C6: NT39565D + AD71124 ×12 | P2 |

**통합 테스트**:
1. End-to-end: 더미 스캔 → LVDS 수신 → BRAM → MCU 출력
2. Gate ON → SYNC → DCLK 타이밍 마진
3. 24-AFE 동시 수신 검증
4. 에러 주입 및 복구

**의존성**: v1 Phase 1-6

---

## v1 Phase 8: Radiography Static Mode (1주)

**목표**: 정지영상 전용 FSM 확장

- Pre-exposure 리셋 FSM (3-8 더미 스캔)
- X-ray 제너레이터 핸드셰이크 (PREP_REQUEST → X_RAY_ENABLE)
- 적분 윈도우 관리
- Dark frame 획득 모드

**의존성**: v1 Phase 7

---

## v1 Parallel Execution Strategy

```
Week:  1    2    3    4    5    6    7    8    9    10

Track A (Control):
       [Phase 1: Foundation  ][Phase 2][Phase 3: Gate IC Drivers ]
                                       [Phase 4: AFE Controllers ]

Track B (Data):
                                                     [Phase 5: LVDS + Buffer + MCU Output]

Track C (Safety):
                                       [Phase 6: Safety (parallel with 3-4)]

Integration:
                                                                       [Phase 7: Integration][P8]
```

---

## v1 SPEC Decomposition

| SPEC-ID | Phase | Title | Effort |
|---------|-------|-------|--------|
| SPEC-FPD-001 | 1 | SPI Slave + Register Bank + Clock Manager | 2주 |
| SPEC-FPD-002 | 2 | Panel Control FSM (6-state, 5-mode) | 1주 |
| SPEC-FPD-003 | 3 | Gate NV1047 Driver + Row Scan Engine | 2주 |
| SPEC-FPD-004 | 3 | Gate NT39565D Driver (large panel) | 2주 |
| SPEC-FPD-005 | 4 | AFE AD711xx Controller (ACLK/SYNC) | 2주 |
| SPEC-FPD-006 | 4 | AFE2256 Controller (MCLK/CIC/SYNC) | 2주 |
| SPEC-FPD-007 | 5 | LVDS Data Receiver + Line Buffer + MCU Output | 2주 |
| SPEC-FPD-008 | 6 | Power Sequencer + Emergency Shutdown + Protection | 1-2주 |
| SPEC-FPD-009 | 7 | Integration: fpga_top C1/C3/C6 | 2-3주 |
| SPEC-FPD-010 | 8 | Radiography Static Mode Extension | 1주 |

**v1 총 예상**: 12-16인주 (3-4개월)
**v1 권장 시작점**: SPEC-FPD-001 → `/moai plan "SPEC-FPD-001"`

---

# v2: 외부 메모리 확장 (v1 완료 후)

v1 완료 후 외부 메모리(SRAM/DDR) 인터페이스를 추가하여 실시간 보정 파이프라인과
래그 보정을 FPGA 내에서 처리.

### v2 전제 조건

- v1 전체 완료 및 검증
- 외부 메모리 하드웨어 (SRAM 또는 DDR3) 연결
- 보정 데이터 (Offset/Gain/Defect map) 준비

### v2에서 추가되는 모듈

| Module | Type | Description | Memory 요구 |
|--------|------|-------------|-------------|
| `ext_mem_if.sv` | Common | 외부 SRAM/DDR 인터페이스 (MIG 또는 Custom) | - |
| `offset_subtractor.sv` | Pipeline | pixel_raw - offset_map[row][col] | Offset map: 8MB+ |
| `gain_multiplier.sv` | Pipeline | pixel_dc × gain_map (고정소수점) | Gain map: 8MB+ |
| `defect_replacer.sv` | Pipeline | 3×3 이웃 보간 (2-line 버퍼) | Defect map: ~1.4KB (sparse) |
| `lag_corrector_lti.sv` | Pipeline | 4-지수 성분 재귀 필터 | Lag state: 150MB |
| `forward_bias_ctrl.sv` | Common | +4V 바이어스, 8-pixel 사이클링 | - |
| `frame_buffer_ctrl.sv` | Common | 프레임 버퍼 관리 (multi-frame) | Frame: 8-19MB/frame |

### v2 Memory Requirements

| Data | Size | Storage |
|------|------|---------|
| Offset map (per temperature) | 8-19 MB | External SRAM/DDR |
| Gain map | 8-19 MB | External SRAM/DDR |
| Defect pixel map (sparse) | ~1.4 KB | BRAM (internal) |
| Lag state (4-component) | ~150 MB | External DDR |
| Frame buffer (1 frame) | 8-19 MB | External DDR |
| Temperature-indexed offset (5 maps) | 40-95 MB | External DDR |

### v2 Data Path (보정 파이프라인 포함)

```
LVDS → line_buf_ram → offset_subtractor → gain_multiplier → defect_replacer → lag_corrector
   (BRAM)            (ext mem read)      (ext mem read)    (BRAM 2-line)    (ext mem R/W)
                                                                                  │
                                                                           data_out_mux → MCU
```

### v2 SPEC Decomposition

| SPEC-ID | Title | Effort |
|---------|-------|--------|
| SPEC-FPD-011 | External Memory Interface (SRAM/DDR MIG) | 2-3주 |
| SPEC-FPD-012 | Offset Subtraction Pipeline (streaming, ext mem) | 2주 |
| SPEC-FPD-013 | Gain Multiplication Pipeline (fixed-point, ext mem) | 1-2주 |
| SPEC-FPD-014 | Defect Pixel Replacement (3×3, 2-line buffer) | 1주 |
| SPEC-FPD-015 | LTI Lag Correction (4-exponential, ext mem state) | 2-3주 |
| SPEC-FPD-016 | Forward Bias Control | 1주 |
| SPEC-FPD-017 | Frame Buffer + Multi-frame Averaging | 2주 |
| SPEC-FPD-018 | v2 Integration & Calibration Validation | 2-3주 |

**v2 총 예상**: 12-18인주 (3-5개월)

---

## Risk Assessment

### v1 Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| RC 시정수 45µs vs 라인타임 충돌 (30fps) | 높음 | 15fps 타겟 또는 VGH 최적화 |
| Multi-AFE SYNC 정렬 | 높음 | IDELAY2 자동 캘리브레이션 |
| Questa 라이선스 | 낮음 | Vivado xsim 대체 |

### v2 Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| 외부 메모리 대역폭 부족 | 높음 | DDR3 MIG 대역폭 분석 필수 |
| BRAM 부족 (보정 파이프라인 + 라인버퍼) | 중간 | 라인 단위 스트리밍, 외부 메모리 캐시 |
| 보정 맵 로딩 시간 | 중간 | DMA 기반 초기화, 온도 변화 시 부분 업데이트 |
| US5452338A 특허 (재귀 오프셋) | 중간 | 고정 오프셋 맵 + 온도 보간 방식 |

---

Version: 2.0.0
Created: 2026-03-19
Updated: 2026-03-19
Based on: docs/fpga-design/ (4 files), docs/research/ (7 files), docs/datasheet/ (10 files)
