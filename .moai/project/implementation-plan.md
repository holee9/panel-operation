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

### Data Output Architecture: CSI-2 MIPI TX

v1에서 데이터 출력 경로를 MCU 병렬/SPI 대신 CSI-2 MIPI TX로 외부 SoC에 전송:

```
Line Buffer BRAM (ping-pong)
    ↓
csi2_packet_builder (CSI-2 패킷 조립: header + RAW16 payload + CRC)
    ↓
csi2_lane_dist (2/4-lane data distribution)
    ↓
LVDS TX (OSERDESE2, Artix-7) → 외부 D-PHY serializer IC (DS90UB953 등)
    ↓
CSI-2 MIPI 2~4 lane → 외부 SoC (ISP/AP)
```

Artix-7 35T에서 CSI-2 MIPI TX D-PHY 소프트 구현은 이미 검증 완료되어 운용 중.
OSERDESE2 기반 직렬화 + LVDS TX로 D-PHY HS 모드 구현. 외부 직렬화 IC 불필요.

**대역폭 요구**:

| 패널 | 해상도 | 프레임 크기 (16-bit) | 30fps 데이터율 | 필요 CSI-2 구성 |
|------|--------|---------------------|---------------|----------------|
| C1-C5 (17") | 2048×2048 | 8 MB | 240 MB/s | 2-lane @ 1.5 Gbps |
| C6-C7 (43cm) | 3072×3072 | 18.9 MB | 567 MB/s | 4-lane @ 1.5 Gbps |

### Verification Methodology: SW-First (C++ Golden Model → RTL)

**원칙**: C++ 소프트웨어 시뮬레이션으로 알고리즘 검증 → 검증된 알고리즘을 SystemVerilog RTL로 변환.

```
Stage 1: C++ Golden Model (알고리즘 정확성 검증)
    ↓ 테스트 벡터 생성
Stage 2: cocotb/Verilator Testbench (RTL 검증)
    ↓ 비트 정확도 비교
Stage 3: SystemVerilog RTL (FPGA 합성 가능 코드)
    ↓ 합성 + 타이밍 검증
Stage 4: Vivado xsim / Verilator 통합 시뮬레이션
```

**도구 스택**:

| Tool | Role | License |
|------|------|---------|
| **C++ (gcc/MSVC)** | 골든 모델: AFE 타이밍, Gate 시퀀스, FSM 로직 | Free |
| **Verilator** | RTL → C++ 변환, 사이클 정확 시뮬레이션 (ModelSim 대비 10~50× 빠름) | Open-source |
| **cocotb** | Python 기반 테스트벤치 (UVM 불필요, 10× 적은 코드) | Open-source |
| **Vivado xsim** | 네이티브 Xilinx 시뮬레이터 (Questa 라이선스 불필요) | Free (Vivado) |

**C++ 골든 모델 구조**:

```
sim/
├── golden_models/
│   ├── afe_model.cpp          # AFE 256ch 적분 + LVDS 직렬화 모델
│   ├── afe_model.h
│   ├── gate_nv1047_model.cpp  # NV1047 시프트 레지스터 시뮬레이션
│   ├── gate_nt39565d_model.cpp # NT39565D 듀얼-STV 시뮬레이션
│   ├── panel_fsm_model.cpp    # FSM 상태 전이 + 타이밍 검증
│   ├── csi2_tx_model.cpp      # CSI-2 패킷 빌더 레퍼런스
│   └── test_vectors/          # 생성된 테스트 벡터 (hex/bin)
├── cocotb_tests/
│   ├── test_spi_slave.py      # SPEC-001 테스트
│   ├── test_panel_fsm.py      # SPEC-002 테스트
│   ├── test_gate_nv1047.py    # SPEC-003 테스트
│   ├── test_afe_ad711xx.py    # SPEC-005 테스트
│   └── test_csi2_tx.py        # SPEC-007 테스트
├── verilator/
│   ├── sim_main.cpp           # Verilator top-level driver
│   ├── golden_compare.cpp     # C++ 모델 vs RTL 비교기
│   └── Makefile
└── CMakeLists.txt             # C++ 빌드 설정
```

**TDD 개발 사이클 (Testbench-First)**:

```
1. C++ 골든 모델 작성 (알고리즘 정확성 확인)
2. cocotb 테스트벤치 작성 (expected behavior 정의)
3. RTL 스켈레톤 작성 (인터페이스만)
4. cocotb 실행 → FAIL (Red)
5. RTL 구현
6. cocotb 실행 → PASS (Green)
7. RTL 리팩토링 (Green 유지)
8. Verilator로 C++ 골든 모델 vs RTL 비트 비교
```

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
Gate IC → Panel → AFE (charge) → LVDS RX → FPGA line_buf_ram
                                                     │
                                    ┌────────────────┴────────────────┐
                                    ▼                                 ▼
                           data_out_mux → mcu_data_if        csi2_packet_builder
                           (legacy MCU 병렬)                 → csi2_lane_dist
                                    │                        → LVDS TX (OSERDESE2)
                                    │                        → 외부 D-PHY IC
                                    │                        → CSI-2 MIPI 2~4 lane
                                    │                                 │
                                    ▼                                 ▼
                               MCU (SPI)                     외부 SoC (ISP/AP)
                                                                      │
                                                        소프트웨어 보정 (Offset, Gain, Defect, Lag)
```

### v1 Implementation Order (Critical Path)

```
Phase 1:  SPEC-FPD-001 (Foundation) ←── 최우선, 모든 SPEC의 선행 조건
Phase 2:  SPEC-FPD-002 (FSM) + SPEC-FPD-008 (Safety) — 병렬 가능
Phase 3:  SPEC-FPD-003 (NV1047) + SPEC-FPD-005 (AD711xx) — 병렬 가능
Phase 4:  SPEC-FPD-004 (NT39565D) + SPEC-FPD-006 (AFE2256) — 병렬 가능
Phase 5:  SPEC-FPD-007 (LVDS + Buffer + MCU Output)
Phase 6:  SPEC-FPD-009 (Integration fpga_top)
Phase 7:  SPEC-FPD-010 (Radiography Static Mode)
```

### v1 Dependency Graph

```
SPEC-001 ──┬──▶ SPEC-002 ──────────────────────────────┐
           ├──▶ SPEC-003 (row_scan_eng 공유) ──▶ SPEC-004 ┤
           ├──▶ SPEC-005 (afe_spi_master 공유) ──▶ SPEC-006 ┤
           ├──▶ SPEC-008                                ├──▶ SPEC-009 ──▶ SPEC-010
           └──▶ SPEC-005 or SPEC-006 ──▶ SPEC-007 ─────┘
```

---

## SPEC-FPD-001: Foundation — SPI + Register + Clock

### Module Mapping

| Module | Path | Type |
|--------|------|------|
| `fpd_types_pkg.sv` | `rtl/packages/fpd_types_pkg.sv` | Package |
| `fpd_params_pkg.sv` | `rtl/packages/fpd_params_pkg.sv` | Package |
| `spi_slave_if.sv` | `rtl/common/spi_slave_if.sv` | Common |
| `reg_bank.sv` | `rtl/common/reg_bank.sv` | Common |
| `clk_rst_mgr.sv` | `rtl/common/clk_rst_mgr.sv` | Common |

### Acceptance Criteria

**AC-001-1: SPI Slave Interface**
- SPI Mode 0 (CPOL=0, CPHA=0) 및 Mode 3 (CPOL=1, CPHA=1) 지원
- 마스터 클럭 범위: 1–10 MHz
- 프레임 포맷: 8-bit 주소 + 16-bit 데이터 (MSB first)
- CS 활성화→첫 SCLK: ≥10ns setup time
- Write: addr[7]=1, Read: addr[7]=0
- Full-duplex: MISO에 이전 주소의 데이터 출력

**AC-001-2: Register Bank**
- 32개 레지스터 (0x00–0x1F), 각 16-bit
- Power-on 기본값이 설계 문서와 일치해야 함
- R/W 속성이 레지스터 맵에 정의된 대로 동작
- Read-only 레지스터 (REG_STATUS, REG_LINE_IDX, REG_ERR_CODE, REG_VERSION)에 쓰기 시 무시
- 동시 SPI write와 내부 status update 간 우선순위: 내부 > SPI

**AC-001-3: Clock & Reset Manager**
- MMCM 기반 클럭 생성:
  - ACLK: 10 MHz ±1% (AD711xx용, 파라미터로 10–40 MHz 범위 지원)
  - MCLK: 32 MHz ±1% (AFE2256용)
  - SYS_CLK: 100 MHz (내부 로직용)
- 비동기 리셋 → 동기 해제 (2-stage FF synchronizer)
- MMCM lock 감지, lock 실패 시 리셋 유지
- PLL_LOCKED 상태 출력

### Register Map (관련 레지스터)

| Addr | Name | Width | R/W | Description |
|------|------|-------|-----|-------------|
| 0x00 | REG_CTRL | 8 | R/W | [0]=START, [1]=ABORT, [2]=IRQ_EN |
| 0x01 | REG_STATUS | 8 | R | [0]=BUSY, [1]=DONE, [2]=ERROR, [3]=LINE_RDY |
| 0x02 | REG_MODE | 4 | R/W | 구동 모드 선택 (5종) |
| 0x03 | REG_COMBO | 4 | R/W | 부품 조합 선택 (C1–C7) |
| 0x04 | REG_NROWS | 12 | R/W | 유효 행 수 (최대 3072) |
| 0x05 | REG_NCOLS | 12 | R/W | 유효 열 수 (최대 3072) |
| 0x06 | REG_TLINE | 16 | R/W | 라인 타임 (10ns 단위, 최소 2200) |
| 0x07 | REG_TRESET | 16 | R/W | 리셋 시퀀스 시간 (10ns 단위) |
| 0x08 | REG_TINTEG | 24 | R/W | 적분 시간 (10ns 단위) |
| 0x09 | REG_TGATE_ON | 12 | R/W | Gate ON 펄스 폭 (clk 단위) |
| 0x0A | REG_TGATE_SETTLE | 8 | R/W | Gate 안정화 대기 (clk 단위) |
| 0x0B | REG_AFE_IFS | 6 | R/W | AFE 풀스케일 코드 |
| 0x0C | REG_AFE_LPF | 4 | R/W | LPF 시상수 코드 |
| 0x0D | REG_AFE_PMODE | 2 | R/W | AFE 전력 모드 |
| 0x0E | REG_CIC_EN | 1 | R/W | AFE2256 CIC 활성화 |
| 0x0F | REG_CIC_PROFILE | 4 | R/W | AFE2256 CIC 프로파일 |
| 0x10 | REG_SCAN_DIR | 1 | R/W | 스캔 방향 (0=정, 1=역) |
| 0x11 | REG_GATE_SEL | 2 | R/W | Gate IC 채널 선택 모드 |
| 0x12 | REG_AFE_NCHIP | 4 | R/W | AFE 체인 수 |
| 0x13 | REG_SYNC_DLY | 8 | R/W | AFE SYNC 딜레이 조정 |
| 0x14 | REG_LINE_IDX | 12 | R | 현재 스캔 중인 행 인덱스 |
| 0x15 | REG_ERR_CODE | 8 | R | 에러 코드 |
| 0x1F | REG_VERSION | 8 | R | FPGA 펌웨어 버전 |

### Timing Constraints

| Parameter | Value | Source |
|-----------|-------|--------|
| SPI SCLK | 1–10 MHz | 설계 문서 §5 |
| ACLK 정밀도 | ±1% (10 MHz nom.) | MMCM spec |
| MCLK 정밀도 | ±1% (32 MHz nom.) | MMCM spec |
| Reset sync | 2-FF, ≥2 SYS_CLK cycles | CDC best practice |

### Test Plan

| ID | Scenario | Pass Criteria |
|----|----------|---------------|
| TB-001-1 | SPI write all 32 registers | 읽기 값 = 쓰기 값 (R/W 레지스터) |
| TB-001-2 | SPI read-only register write attempt | 값 변경 없음 |
| TB-001-3 | SPI Mode 0 / Mode 3 switching | 두 모드 모두 정상 동작 |
| TB-001-4 | SPI clock sweep (1, 5, 10 MHz) | 모든 주파수에서 에러 없음 |
| TB-001-5 | ACLK frequency measurement | 10 MHz ±1% |
| TB-001-6 | MCLK frequency measurement | 32 MHz ±1% |
| TB-001-7 | Async reset → sync release | 2-FF 동기화 확인, glitch 없음 |
| TB-001-8 | MMCM lock failure | 리셋 유지, PLL_LOCKED=0 |
| TB-001-9 | Register default values | 모든 레지스터 POR 값 일치 |

### Dependencies

- 선행: 없음 (최초 SPEC)
- 후행: SPEC-002, 003, 004, 005, 006, 007, 008 (전부)

---

## SPEC-FPD-002: Panel Control FSM

### Module Mapping

| Module | Path | Type |
|--------|------|------|
| `panel_ctrl_fsm.sv` | `rtl/panel/panel_ctrl_fsm.sv` | Common |
| `panel_reset_ctrl.sv` | `rtl/panel/panel_reset_ctrl.sv` | Common |
| `panel_integ_ctrl.sv` | `rtl/panel/panel_integ_ctrl.sv` | Common |

### Acceptance Criteria

**AC-002-1: FSM State Machine**
- 7개 상태: IDLE → RESET → INTEGRATE → READOUT_INIT → SCAN_LINE → READOUT_DONE → DONE
- ERROR 상태: 어느 상태에서든 진입 가능, IDLE로 복귀
- ABORT 명령 (REG_CTRL[1]): 어느 상태에서든 → IDLE 즉시 복귀

**AC-002-2: 5 Operating Modes**
- STATIC (000): 단일 프레임 획득 후 DONE
- CONTINUOUS (001): DONE → RESET 자동 반복
- TRIGGERED (010): 외부 트리거 대기 후 획득
- DARK_FRAME (011): Gate OFF 유지, AFE 리드아웃만 (오프셋 캘리브레이션)
- RESET_ONLY (100): 패널 리셋만 실행

**AC-002-3: Panel Reset Controller**
- 3–8회 dummy scan 실행 (REG_NRESET 레지스터로 설정)
- 각 dummy scan: 전체 행 스캔 (ONA=L → 전체 VGG)
- 완료 후 reset_done 신호 발생

**AC-002-4: Integration Controller**
- X-ray 핸드셰이크: PREP_REQUEST → X_RAY_READY 대기 → X_RAY_ENABLE → EXPOSURE_DONE
- 적분 시간: REG_TINTEG (10ns 단위, 24-bit → 최대 ~168ms)
- 타임아웃: X_RAY_READY 미수신 시 5초 타임아웃 → ERROR
- TRIGGERED 모드: 외부 트리거 에지 감지 → INTEGRATE 진입

### Timing Constraints

| Parameter | Value | Source |
|-----------|-------|--------|
| T_RESET | REG_TRESET × 10ns | 레지스터 설정 |
| T_INTEG | REG_TINTEG × 10ns (max ~168ms) | 레지스터 설정 |
| X_RAY_READY 타임아웃 | 5 sec | 설계 문서 §8.1 |
| ABORT 응답 시간 | ≤2 SYS_CLK cycles | 즉시 복귀 |

### Test Plan

| ID | Scenario | Pass Criteria |
|----|----------|---------------|
| TB-002-1 | IDLE→RESET→INTEGRATE→READOUT→DONE 전체 경로 | 정상 전이 |
| TB-002-2 | STATIC 모드 완료 후 IDLE 유지 | DONE 후 자동 반복 없음 |
| TB-002-3 | CONTINUOUS 모드 자동 반복 | DONE→RESET 반복 확인 |
| TB-002-4 | TRIGGERED 모드 외부 트리거 대기 | 트리거 전 IDLE 유지 |
| TB-002-5 | DARK_FRAME 모드 Gate OFF 확인 | gate_en=0 유지 |
| TB-002-6 | RESET_ONLY 모드 | 리셋 완료 후 DONE (리드아웃 건너뜀) |
| TB-002-7 | ERROR 진입 및 복귀 | 에러 조건 → ERROR → ABORT → IDLE |
| TB-002-8 | ABORT 즉시 정지 | 어느 상태에서든 ≤2 clk 내 IDLE 복귀 |
| TB-002-9 | Dummy scan 횟수 3, 5, 8회 | REG_NRESET 값별 리셋 횟수 일치 |
| TB-002-10 | X_RAY_READY 타임아웃 (5초) | 5초 후 ERROR 진입 |

### Dependencies

- 선행: SPEC-FPD-001 (reg_bank, clk_rst_mgr)
- 후행: SPEC-FPD-009 (통합), SPEC-FPD-010 (radiography 확장)

---

## SPEC-FPD-003: Gate NV1047 Driver + Row Scan Engine

### Module Mapping

| Module | Path | Type |
|--------|------|------|
| `gate_nv1047.sv` | `rtl/gate/gate_nv1047.sv` | Combo-specific |
| `row_scan_eng.sv` | `rtl/gate/row_scan_eng.sv` | Common |

### Acceptance Criteria

**AC-003-1: NV1047 Shift Register Control**
- SD1 데이터 시프트: CLK 상승 에지에서 row_index에 해당하는 비트 출력
- CLK 주파수: ≤200 kHz (T_clk_period ≥ 5 µs)
- OE (output enable): Active-low, gate_on_pulse 동안 HIGH → VGH 출력
- ONA (ON-ALL): Low → 전체 행 VGG 강제 (리셋 시 사용)
- L/R (방향): REG_SCAN_DIR에 따라 정방향(0)/역방향(1)
- RST: 파워온 리셋, ≥100ns 펄스

**AC-003-2: Gate Timing**
- T_gate_on: 15–45 µs (5×τ_TFT, REG_TGATE_ON으로 설정)
- T_gate_settle: ≥2 µs (REG_TGATE_SETTLE으로 설정)
- Gate ON → Gate OFF → Settle → 다음 행 Gate ON 순서 보장
- Gate ON/OFF 오버랩 없음 (break-before-make)

**AC-003-3: Row Scan Engine**
- 정방향 스캔: row 0 → N-1
- 역방향 스캔: row N-1 → 0
- 행 카운터: REG_NROWS 기반 (최대 3072)
- gate_on_pulse: REG_TGATE_ON clk cycles
- settle_time: REG_TGATE_SETTLE clk cycles
- scan_done: 모든 행 스캔 완료 시 발생
- row_index 출력: 현재 스캔 중인 행 번호 (REG_LINE_IDX에 반영)

### Timing Constraints

| Parameter | Value | Source |
|-----------|-------|--------|
| CLK max freq | 200 kHz | NV1047 datasheet |
| T_gate_on | 15–45 µs (configurable) | τ_TFT = 3–9 µs, 5× |
| T_gate_settle | ≥2 µs | 설계 문서 §4.2 |
| T_line (min) | 22 µs (AD71124) / 60 µs (AD71143) | AFE datasheet |
| RST pulse | ≥100 ns | NV1047 datasheet |

### Test Plan

| ID | Scenario | Pass Criteria |
|----|----------|---------------|
| TB-003-1 | SD1 shift 256행 정방향 | 행 순서 0→255 |
| TB-003-2 | 역방향 스캔 | 행 순서 255→0 |
| TB-003-3 | CLK 주파수 200 kHz 상한 | CLK ≤ 200 kHz |
| TB-003-4 | T_gate_on = 15/30/45 µs | ±2 µs 이내 |
| TB-003-5 | T_gate_settle ≥ 2 µs | settle 기간 중 다음 행 gate ON 없음 |
| TB-003-6 | ONA=L 리셋 모드 | 전체 행 동시 VGG |
| TB-003-7 | Break-before-make | 인접 행 gate 오버랩 없음 |
| TB-003-8 | 3072행 대형 패널 스캔 | row_index 정상 카운팅 |

### Dependencies

- 선행: SPEC-FPD-001 (reg_bank, clk_rst_mgr)
- 후행: SPEC-FPD-004 (row_scan_eng 재사용), SPEC-FPD-009 (통합)

---

## SPEC-FPD-004: Gate NT39565D Driver

### Module Mapping

| Module | Path | Type |
|--------|------|------|
| `gate_nt39565d.sv` | `rtl/gate/gate_nt39565d.sv` | Combo-specific |

### Acceptance Criteria

**AC-004-1: NT39565D Dual-STV Control**
- STV1/STV2: 스타트 펄스 생성, 2G 모드 시 두 펄스 교번
- STV 펄스 폭: Long=2T 또는 Short=1T (모드 설정)
- STV1L/STV1R + STV2L/STV2R: 좌우 독립 제어
- CPV (Clock Pulse Vertical): 행 선택 진행 클럭, ≤200 kHz
- CPV ~100 kHz 권장 (30 fps 대형 패널 기준)

**AC-004-2: OE Split-Channel Control**
- OE1/OE2: 홀수/짝수 채널 분리 제어
- OE1_L, OE1_R, OE2_L, OE2_R: 좌우 독립
- OE 타이밍은 NV1047의 gate_on/settle과 동일 개념

**AC-004-3: 6-Chip Cascade**
- NT39565D 541ch × 6개 = 3072+ gate lines
- STVD (Start Vertical Data) 전파 모니터링
- 칩 1의 STVD → 칩 2의 STVI 연결 확인
- Cascade 완료 감지: 마지막 칩의 STVD 수신

**AC-004-4: row_scan_eng 재사용**
- SPEC-003에서 구현된 row_scan_eng.sv 인스턴스 사용
- NT39565D 전용 래퍼: row_index → STV/CPV/OE 신호 매핑

### Timing Constraints

| Parameter | Value | Source |
|-----------|-------|--------|
| CPV max freq | 200 kHz | NT39565D datasheet |
| CPV 권장 freq | ~100 kHz (30 fps) | 설계 문서 §6.3 |
| STV pulse width | 1T or 2T (T = CPV period) | NT39565D datasheet |
| Gate lines | 3072 (541ch × 6 chips) | C6/C7 패널 |

### Test Plan

| ID | Scenario | Pass Criteria |
|----|----------|---------------|
| TB-004-1 | Dual-STV 2G 모드 생성 | STV1/STV2 교번 확인 |
| TB-004-2 | CPV 100 kHz 출력 | 주파수 ±5% |
| TB-004-3 | OE1/OE2 분리 제어 | 홀짝 채널 독립 동작 |
| TB-004-4 | 6-chip cascade STVD 전파 | 마지막 칩 STVD 수신 |
| TB-004-5 | 3072행 스캔 완료 | scan_done 정상 발생 |
| TB-004-6 | LR 방향 전환 | 좌우 STV/OE 방향 반전 |

### Dependencies

- 선행: SPEC-FPD-001, SPEC-FPD-003 (row_scan_eng.sv 재사용)
- 후행: SPEC-FPD-009 (통합)

---

## SPEC-FPD-005: AFE AD711xx Controller

### Module Mapping

| Module | Path | Type |
|--------|------|------|
| `afe_ad711xx.sv` | `rtl/roic/afe_ad711xx.sv` | Combo-specific |
| `afe_spi_master.sv` | `rtl/roic/afe_spi_master.sv` | Common |

### Acceptance Criteria

**AC-005-1: AFE SPI Configuration**
- SPI Master: AFE 초기화 레지스터 설정
- IFS (Input Full Scale): 6-bit (AD71124, 0–63) / 5-bit (AD71143, 0–31) — REG_AFE_IFS
- LPF (Low-Pass Filter): 4-bit 시상수 — REG_AFE_LPF
- PMODE (Power Mode): 2-bit — REG_AFE_PMODE
- 데이지체인 SPI: 24 AFE × 24-bit = 576 bits per frame
- SPI clock ≤ ACLK/4

**AC-005-2: ACLK Generation**
- MMCM에서 생성된 ACLK (10–40 MHz, 기본 10 MHz)
- clk_rst_mgr로부터 수신, AFE로 직접 출력
- ACLK jitter: ≤100 ps RMS (MMCM spec)

**AC-005-3: SYNC Pulse**
- SYNC 펄스 폭: ≥1 ACLK period
- 브로드캐스트: 모든 AFE에 동시 전달
- SYNC 딜레이: REG_SYNC_DLY로 미세 조정 (8-bit)
- SYNC → 첫 DCLK 에지: 데이터시트 규격 준수

**AC-005-4: Data Valid Window**
- dout_window_valid: SYNC 후 변환 완료까지의 유효 구간 표시
- AD71124: tLINE min = 22 µs
- AD71143: tLINE min = 60 µs
- 유효 구간 외 LVDS 데이터 무시

### Timing Constraints

| Parameter | Value | Source |
|-----------|-------|--------|
| ACLK | 10–40 MHz (default 10 MHz) | AD711xx datasheet |
| SYNC pulse width | ≥1 ACLK period | AD711xx datasheet |
| tLINE min (AD71124) | 22 µs (= REG_TLINE ≥ 2200) | AD71124 datasheet |
| tLINE min (AD71143) | 60 µs (= REG_TLINE ≥ 6000) | AD71143 datasheet |
| SPI daisy-chain | 24 AFE × 24-bit = 576 bits | 설계 문서 §4.3 |

### Test Plan

| ID | Scenario | Pass Criteria |
|----|----------|---------------|
| TB-005-1 | SPI init: IFS/LPF/PMODE 설정 | AFE 레지스터 값 일치 |
| TB-005-2 | Daisy-chain 24 AFE 전송 | 576-bit 프레임 무결성 |
| TB-005-3 | ACLK 10 MHz 출력 | 주파수 ±1% |
| TB-005-4 | SYNC 펄스 폭 ≥1 ACLK | 파형 확인 |
| TB-005-5 | SYNC → DCLK 타이밍 | 데이터시트 규격 이내 |
| TB-005-6 | dout_window_valid 타이밍 | 유효 구간 ≥ tLINE |
| TB-005-7 | REG_SYNC_DLY 조정 | 딜레이 0–255 범위 동작 |
| TB-005-8 | AD71124 vs AD71143 IFS bit-width | 6-bit / 5-bit 분리 동작 |

### Dependencies

- 선행: SPEC-FPD-001 (clk_rst_mgr ACLK, reg_bank)
- 후행: SPEC-FPD-006 (afe_spi_master 재사용), SPEC-FPD-007 (LVDS 수신), SPEC-FPD-009 (통합)

---

## SPEC-FPD-006: AFE2256 Controller

### Module Mapping

| Module | Path | Type |
|--------|------|------|
| `afe_afe2256.sv` | `rtl/roic/afe_afe2256.sv` | Combo-specific |

### Acceptance Criteria

**AC-006-1: AFE2256 SPI Configuration**
- afe_spi_master.sv (SPEC-005에서 구현) 재사용
- IFS 설정 (AFE2256 전용 레지스터)
- CIC (Charge Injection Compensation) enable/disable — REG_CIC_EN
- CIC profile 선택 — REG_CIC_PROFILE (4-bit)
- CIC profile load: 256ch × 16-bit per channel
- Pipeline mode (Integrate-and-Read): PIPELINE_EN 레지스터
- TP_SEL (Timing Profile Select): integrate-up vs integrate-down

**AC-006-2: MCLK Generation**
- MMCM에서 생성된 MCLK (32 MHz)
- clk_rst_mgr로부터 수신, AFE로 직접 출력

**AC-006-3: SYNC Timing (TI-mode)**
- SYNC → 내부 TG (Timing Generator) 기동
- FCLK (Frame Clock) expected window 감지
- DCLK_P/M 기반 LVDS 데이터 수신 (ADI와 포맷 다름)

**AC-006-4: Pipeline Mode**
- Integrate-and-Read 파이프라인: row[n] Gate ON + Integrate 동안 row[n-1] ADC 읽기
- 파이프라인 지연: 1 row

### Timing Constraints

| Parameter | Value | Source |
|-----------|-------|--------|
| MCLK | 32 MHz (default) | AFE2256 datasheet |
| tLINE min | 51.2 µs | AFE2256 datasheet |
| CIC profile load | 256ch × 16-bit = 4096 bits | AFE2256 datasheet |
| Pipeline latency | 1 row | 설계 문서 §4.3 |

### Test Plan

| ID | Scenario | Pass Criteria |
|----|----------|---------------|
| TB-006-1 | CIC enable/disable 토글 | REG_CIC_EN 반영 |
| TB-006-2 | CIC profile 로드 (256ch) | 4096-bit 전송 완료 |
| TB-006-3 | MCLK 32 MHz 출력 | 주파수 ±1% |
| TB-006-4 | SYNC → FCLK window 감지 | 타이밍 규격 이내 |
| TB-006-5 | Pipeline mode 활성화 | row[n-1] 읽기 + row[n] 적분 동시 |
| TB-006-6 | TP_SEL integrate-up/down | 두 모드 정상 동작 |
| TB-006-7 | afe_spi_master 공유 확인 | AD711xx와 독립 사용 |

### Dependencies

- 선행: SPEC-FPD-001, SPEC-FPD-005 (afe_spi_master.sv 재사용)
- 후행: SPEC-FPD-007 (LVDS 수신), SPEC-FPD-009 (통합)

---

## SPEC-FPD-007: LVDS Receiver + Line Buffer + CSI-2 MIPI TX Output

### Module Mapping

| Module | Path | Type |
|--------|------|------|
| `line_data_rx.sv` | `rtl/roic/line_data_rx.sv` | Common (param) |
| `line_buf_ram.sv` | `rtl/roic/line_buf_ram.sv` | Common |
| `data_out_mux.sv` | `rtl/common/data_out_mux.sv` | Common |
| `mcu_data_if.sv` | `rtl/common/mcu_data_if.sv` | Common (legacy, MCU 병렬) |
| `csi2_packet_builder.sv` | `rtl/common/csi2_packet_builder.sv` | Common (신규) |
| `csi2_lane_dist.sv` | `rtl/common/csi2_lane_dist.sv` | Common (신규) |

### Acceptance Criteria

**AC-007-1: LVDS Receiver (ADI Mode — AD711xx)**
- IBUFDS → ISERDESE2 → 16-bit 역직렬화
- 2 LVDS data pairs (DOUTA, DOUTB) + 1 DCLK pair per AFE
- Self-clocked: DCLK는 AFE 내부 생성 (ACLK 기반)
- DCLKH/DCLKL 차동 수신
- Per-AFE 인스턴스: 최대 24개 동시 수신

**AC-007-2: LVDS Receiver (TI Mode — AFE2256)**
- 4 LVDS data pairs + 1 DCLK pair + 1 FCLK pair per AFE
- DCLK_P/M + FCLK_P/M 차동 수신
- FCLK: 라인 완료 경계 표시

**AC-007-3: CDC (Clock Domain Crossing)**
- DCLK write domain → SYS_CLK read domain
- Dual-clock BRAM 또는 async FIFO 기반 CDC
- FIFO depth: ≥16 words per AFE group (overflow 방지)
- No data loss, no metastability

**AC-007-4: Line Buffer RAM (Ping-Pong)**
- Dual-port BRAM, ping-pong 구조
- Bank A: write (현재 라인 수신 중)
- Bank B: read (이전 라인 MCU 전송 중)
- Bank swap: 라인 완료 시 자동 전환
- 용량: 2048 × 16-bit × 2 banks = 4 BRAM36K

**AC-007-5: Data Output MUX + MCU Interface (Legacy)**
- data_out_mux: 다중 AFE 라인 데이터 → 순차 MCU 버스 정렬
- mcu_data_if: 16-bit 병렬 데이터 출력
- Handshake: DATA_VALID + DATA_ACK
- LINE_RDY 인터럽트: REG_STATUS[3] 세트 → MCU IRQ

**AC-007-6: CSI-2 MIPI TX Output (Primary)**
- csi2_packet_builder: CSI-2 패킷 조립
  - Short Packet: Frame Start (FS), Frame End (FE)
  - Long Packet: Header (DI + WC + ECC) + RAW16 Payload + CRC-16
  - Data Type: 0x2E (RAW16)
  - Virtual Channel: 0 (단일 카메라)
- csi2_lane_dist: 2-lane 또는 4-lane 데이터 분배
  - Byte interleaving: Lane 0 = byte[0,4,8...], Lane 1 = byte[1,5,9...]
  - 2-lane 모드: C1-C5 (17" 패널)
  - 4-lane 모드: C6-C7 (43cm 대형 패널)
- 출력: LVDS TX (OSERDESE2) → 외부 D-PHY serializer IC
  - 인터페이스: 16-bit 병렬 데이터 + BYTE_CLK + LP 신호
  - LP (Low-Power) 모드: 프레임 간 유휴 시 전환
- 데이터 무결성: CRC-16 per packet, ECC per header

### Timing Constraints

| Parameter | Value | Source |
|-----------|-------|--------|
| LVDS bit rate | ACLK × 2 (ADI) / MCLK × 2 (TI) | AFE datasheet |
| ISERDESE2 | SDR/DDR, 1:8 deserialize | Artix-7 UG471 |
| IDELAYE2 | Tap resolution ~78ps | Artix-7 UG471 |
| Line buffer swap | < 1 SYS_CLK cycle | 설계 요구 |
| MCU burst transfer | Per line complete | 설계 문서 §6 |
| CSI-2 lane rate | 1.0–1.5 Gbps/lane | MIPI CSI-2 spec |
| CSI-2 2-lane throughput | 250–375 MB/s | C1-C5 (17" 패널) |
| CSI-2 4-lane throughput | 500–750 MB/s | C6-C7 (43cm 패널) |
| D-PHY LP→HS transition | < 1 µs | MIPI D-PHY spec |

### Test Plan

| ID | Scenario | Pass Criteria |
|----|----------|---------------|
| TB-007-1 | LVDS loopback (ADI mode) | TX→RX 데이터 일치 |
| TB-007-2 | LVDS loopback (TI mode) | TX→RX 데이터 일치 |
| TB-007-3 | BER 측정 (1M+ bits) | BER < 1e-12 |
| TB-007-4 | Ping-pong bank swap | 라인 경계에서 정상 전환 |
| TB-007-5 | CDC FIFO overflow stress | FIFO full 시 backpressure 동작 |
| TB-007-6 | 24-AFE 동시 수신 | 모든 AFE 데이터 무결성 |
| TB-007-7 | LINE_RDY IRQ 발생 | 라인 완료 시 REG_STATUS[3]=1 |
| TB-007-8 | MCU handshake timing | DATA_VALID→ACK latency 측정 |
| TB-007-9 | data_out_mux AFE 순서 | AFE 0→23 순차 출력 |
| TB-007-10 | CSI-2 FS/FE short packet | 프레임 시작/종료 패킷 정상 |
| TB-007-11 | CSI-2 RAW16 long packet CRC | CRC-16 일치 |
| TB-007-12 | CSI-2 ECC header 검증 | ECC 1-bit 정정 가능 |
| TB-007-13 | CSI-2 2-lane byte interleave | Lane 0/1 바이트 분배 정확 |
| TB-007-14 | CSI-2 4-lane byte interleave | Lane 0-3 바이트 분배 정확 |
| TB-007-15 | CSI-2 LP 모드 전환 | 프레임 간 LP 상태 유지 |
| TB-007-16 | CSI-2 full-frame 전송 (C++ 골든 비교) | 비트 정확도 일치 |

### Dependencies

- 선행: SPEC-FPD-005 또는 SPEC-FPD-006 (AFE 제어 + LVDS 출력)
- 후행: SPEC-FPD-009 (통합)

---

## SPEC-FPD-008: Safety — Protection + Emergency + Power Sequencer

### Module Mapping

| Module | Path | Type |
|--------|------|------|
| `prot_mon.sv` | `rtl/common/prot_mon.sv` | Common |
| `emergency_shutdown.sv` | `rtl/common/emergency_shutdown.sv` | Common |
| `power_sequencer.sv` | `rtl/common/power_sequencer.sv` | Common |

### Acceptance Criteria

**AC-008-1: Protection Monitor**
- 과노출 타임아웃: 5초 카운터 (적분 상태 진입 후)
- 에러 플래그: timeout_flag, overexposure_flag
- force_gate_off: 타임아웃 시 모든 Gate IC 강제 OFF
- 에러 코드 → REG_ERR_CODE (0x15) 반영

**AC-008-2: Emergency Shutdown**
- 감지 조건:
  - VGH > 38V (과전압)
  - VGH < 15V (저전압)
  - TEMP > 45°C (과온도)
  - PLL unlock (MMCM lock 상실)
- 응답 시간: < 100 µs (감지 → 셧다운 완료)
- 셧다운 동작: 모든 출력 비활성화, Gate OFF, AFE RESET
- 래치 방식: 셧다운 후 MCU 명시적 클리어까지 유지

**AC-008-3: Power Sequencer**
- 전원 인가 순서: VGL → VGL 안정화(10ms) → VGH → VGH 안정화(10ms) → AFE AVDD → AFE DVDD
- VGL이 VGH보다 반드시 먼저 인가 (하드웨어 보호)
- 전원 해제 순서: 인가의 역순
- Slew rate: ≤5 V/ms (전압 변화율 제한)
- FSM 상태: S_OFF → S_VGL → S_VGL_WAIT → S_VGH → S_VGH_WAIT → S_AFE_AVDD → S_AFE_DVDD → S_READY
- power_good 출력: 모든 전원 안정화 완료 시

### Timing Constraints

| Parameter | Value | Source |
|-----------|-------|--------|
| 과노출 타임아웃 | 5 sec | 설계 문서 §4.5 |
| Emergency 응답 | < 100 µs | 안전 요구사항 |
| VGL 안정화 | 10 ms | 설계 문서 §4.6 |
| VGH 안정화 | 10 ms | 설계 문서 §4.6 |
| Slew rate | ≤5 V/ms | 전원 설계 규격 |
| VGH 범위 | +20V ~ +35V (C1-C5) / +28V (C6-C7) | 패널 사양 |
| VGL 범위 | -10V (C1-C5) / -12V (C6-C7) | 패널 사양 |

### Test Plan

| ID | Scenario | Pass Criteria |
|----|----------|---------------|
| TB-008-1 | 5초 타임아웃 주입 | timeout_flag=1, force_gate_off=1 |
| TB-008-2 | VGH > 38V 과전압 감지 | emergency_shutdown 활성화 < 100 µs |
| TB-008-3 | VGH < 15V 저전압 감지 | emergency_shutdown 활성화 < 100 µs |
| TB-008-4 | TEMP > 45°C 과온도 | emergency_shutdown 활성화 |
| TB-008-5 | PLL unlock 감지 | emergency_shutdown 활성화 |
| TB-008-6 | 셧다운 래치 유지 | MCU 클리어 전 재활성화 불가 |
| TB-008-7 | 전원 순서 VGL→VGH | VGL 인가 10ms 후 VGH 인가 |
| TB-008-8 | 전원 해제 역순 | DVDD→AVDD→VGH→VGL 순서 |
| TB-008-9 | power_good 신호 | 모든 전원 안정화 후 assert |
| TB-008-10 | 전원 시퀀스 중 abort | 안전한 역순 셧다운 |

### Dependencies

- 선행: SPEC-FPD-001 (clk_rst_mgr, reg_bank)
- 후행: SPEC-FPD-009 (통합)
- SPEC-FPD-002와 병렬 구현 가능

---

## SPEC-FPD-009: Integration — fpga_top

### Module Mapping

| Module | Path | Type |
|--------|------|------|
| `fpga_top_c1.sv` | `rtl/top/fpga_top_c1.sv` | Top |
| `fpga_top_c3.sv` | `rtl/top/fpga_top_c3.sv` (신규) | Top |
| `fpga_top_c6.sv` | `rtl/top/fpga_top_c6.sv` (신규) | Top |

### Acceptance Criteria

**AC-009-1: fpga_top_c1 (NV1047 + AD71124)**
- 모든 공통 모듈 인스턴스화 및 연결
- gate_nv1047 + afe_ad711xx 조합
- REG_COMBO=C1 시 활성화
- 핀 매핑: xc7a35tfgg484-1 패키지 기준

**AC-009-2: fpga_top_c3 (NV1047 + AFE2256)**
- gate_nv1047 + afe_afe2256 조합
- CIC 관련 추가 핀 (TP_SEL)
- MCLK 32 MHz 핀 배정

**AC-009-3: fpga_top_c6 (NT39565D × 6 + AD71124 × 12)**
- gate_nt39565d + afe_ad711xx × 12 (다중 인스턴스)
- 12 AFE LVDS: 36 LVDS pairs = 72 pins
- SYNC 브로드캐스트 + SPI 데이지체인
- STV/CPV/OE 좌우 독립 핀

**AC-009-4: XDC Constraints**
- 핀 할당: I/O standard (LVCMOS33, LVDS_25)
- 타이밍 제약: ACLK/MCLK/SYS_CLK periods
- LVDS 입력: IBUFDS differential termination
- False path: SPI ↔ SYS_CLK CDC, X-ray trigger async

**AC-009-5: End-to-End Functional**
- Dummy scan → LVDS 수신 → BRAM → MCU 출력 데이터 경로 완료
- Gate ON → SYNC → DCLK 타이밍 마진 양수
- 24-AFE 동시 수신 시 BRAM 대역폭 충분
- 에러 주입 → emergency_shutdown → 복구 경로

### Test Plan

| ID | Scenario | Pass Criteria |
|----|----------|---------------|
| TB-009-1 | C1 end-to-end: scan→readout→MCU | 전체 데이터 경로 무결성 |
| TB-009-2 | C3 end-to-end + CIC | AFE2256 CIC 데이터 경로 |
| TB-009-3 | C6 multi-AFE 12×LVDS 동시 | 12 AFE 병렬 수신 |
| TB-009-4 | Gate→SYNC→DCLK 타이밍 마진 | 마진 ≥ 0 (모든 조합) |
| TB-009-5 | 에러 주입 + 복구 | emergency → IDLE 복구 |
| TB-009-6 | 전원 시퀀스 → 스캔 → 셧다운 | 전체 lifecycle |
| TB-009-7 | XDC lint (Vivado) | 미할당 핀 없음, 제약 위반 없음 |
| TB-009-8 | Resource utilization report | BRAM ≤ 8, LUT ≤ 80%, FF ≤ 80% |

### Dependencies

- 선행: SPEC-FPD-001 ~ SPEC-FPD-008 (모든 하위 모듈)
- 후행: SPEC-FPD-010 (radiography 확장)

---

## SPEC-FPD-010: Radiography Static Mode

### Module Mapping

| Module | Path | Type |
|--------|------|------|
| `panel_ctrl_fsm.sv` | `rtl/panel/panel_ctrl_fsm.sv` | 확장 |
| `panel_integ_ctrl.sv` | `rtl/panel/panel_integ_ctrl.sv` | 확장 |

### Acceptance Criteria

**AC-010-1: Radiography Sub-FSM**
- 정지영상 전용 FSM 확장 (12 상태):
  S1_IDLE → S2_PANEL_RESET → S3_PREP_WAIT → S4_XRAY_READY_WAIT →
  S5_XRAY_ENABLE → S6_INTEGRATION → S7_LAST_RESET → S8_READOUT_INIT →
  S9_SCAN_LINE → S10_READOUT_DONE → S11_DATA_TRANSFER → S12_POST_STAB
- panel_ctrl_fsm의 STATIC 모드 활성화 시 radiography sub-FSM 진입

**AC-010-2: Pre-Exposure Reset**
- 3–8회 dummy scan (REG_NRESET로 설정)
- 각 dummy scan = 전체 행 Gate ON → Gate OFF 사이클
- TFT 트랩 안정화 목적

**AC-010-3: Generator Handshake**
- PREP_REQUEST: Pre-reset 완료 후 assert
- X_RAY_READY 대기: Generator 고전압 충전 확인 (최대 30초 타임아웃)
- X_RAY_ENABLE: assert → INTEGRATE 상태 진입
- EXPOSURE_DONE: Generator → FPGA, 조사 완료 통지
- 타임아웃: X_RAY_READY 미수신 시 ERR_XRAY_TIMEOUT

**AC-010-4: Dark Frame Acquisition**
- DARK_FRAME 모드 (REG_MODE=011)
- Gate OFF 유지, AFE 리드아웃만 실행
- 64 프레임 연속 획득 (CAL_DARK_FRAMES=64)
- 오프셋 캘리브레이션 데이터용

**AC-010-5: Post-Stabilization**
- 촬영 완료 후 Forward Bias / Dummy scan으로 패널 안정화
- 래그 감소 목적

### Timing Constraints

| Parameter | Value | Source |
|-----------|-------|--------|
| Pre-exposure reset | 3–8 × T_frame | 설계 문서 §8.1.2 |
| X_RAY_READY 타임아웃 | 30 sec | 설계 문서 §8.1.3 |
| Dark frame count | 64 frames | 설계 문서 §8.2 |
| Post-stabilization | Configurable | 설계 문서 §7 |

### Test Plan

| ID | Scenario | Pass Criteria |
|----|----------|---------------|
| TB-010-1 | Pre-exposure reset 3회 | 3 dummy scan 완료 |
| TB-010-2 | Pre-exposure reset 8회 | 8 dummy scan 완료 |
| TB-010-3 | Generator handshake 정상 경로 | PREP→READY→ENABLE→DONE |
| TB-010-4 | X_RAY_READY 30초 타임아웃 | ERR_XRAY_TIMEOUT 발생 |
| TB-010-5 | Dark frame 64 프레임 획득 | frame_cnt = 64, Gate OFF 유지 |
| TB-010-6 | 전체 radiography 사이클 | reset→expose→readout→stabilize |
| TB-010-7 | Radiography 중 ABORT | 안전한 즉시 정지 |

### Dependencies

- 선행: SPEC-FPD-009 (통합 완료 후 FSM 확장)
- 후행: 없음 (v1 최종 SPEC)

---

## v1 SPEC Summary

| SPEC-ID | Title | Modules | Dependencies |
|---------|-------|---------|--------------|
| SPEC-FPD-001 | Foundation: SPI + Register + Clock | spi_slave_if, reg_bank, clk_rst_mgr, fpd_types_pkg, fpd_params_pkg | None |
| SPEC-FPD-002 | Panel Control FSM | panel_ctrl_fsm, panel_reset_ctrl, panel_integ_ctrl | 001 |
| SPEC-FPD-003 | Gate NV1047 Driver + Row Scan Engine | gate_nv1047, row_scan_eng | 001 |
| SPEC-FPD-004 | Gate NT39565D Driver | gate_nt39565d | 001, 003 |
| SPEC-FPD-005 | AFE AD711xx Controller | afe_ad711xx, afe_spi_master | 001 |
| SPEC-FPD-006 | AFE2256 Controller | afe_afe2256 | 001, 005 |
| SPEC-FPD-007 | LVDS Receiver + Line Buffer + CSI-2 TX | line_data_rx, line_buf_ram, data_out_mux, mcu_data_if, csi2_packet_builder, csi2_lane_dist | 005 or 006 |
| SPEC-FPD-008 | Safety: Protection + Emergency + Power | prot_mon, emergency_shutdown, power_sequencer | 001 |
| SPEC-FPD-009 | Integration: fpga_top | fpga_top_c1, fpga_top_c3, fpga_top_c6 | 001–008 |
| SPEC-FPD-010 | Radiography Static Mode | panel_ctrl_fsm (확장), panel_integ_ctrl (확장) | 009 |

### v1 Module-SPEC Cross Reference (22 RTL files)

| RTL File | SPEC | Role |
|----------|------|------|
| `rtl/packages/fpd_types_pkg.sv` | 001 | 전역 타입 정의 |
| `rtl/packages/fpd_params_pkg.sv` | 001 | 조합별 파라미터 |
| `rtl/common/spi_slave_if.sv` | 001 | MCU SPI 슬레이브 |
| `rtl/common/reg_bank.sv` | 001 | 32-레지스터 파일 |
| `rtl/common/clk_rst_mgr.sv` | 001 | MMCM 클럭 + 리셋 |
| `rtl/panel/panel_ctrl_fsm.sv` | 002, 010 | 메인 FSM + radiography 확장 |
| `rtl/panel/panel_reset_ctrl.sv` | 002 | 패널 리셋 (dummy scan) |
| `rtl/panel/panel_integ_ctrl.sv` | 002, 010 | 적분 제어 + X-ray 핸드셰이크 |
| `rtl/gate/gate_nv1047.sv` | 003 | NV1047 시프트 레지스터 |
| `rtl/gate/row_scan_eng.sv` | 003 | 행 스캔 엔진 (공통) |
| `rtl/gate/gate_nt39565d.sv` | 004 | NT39565D 듀얼 STV |
| `rtl/roic/afe_ad711xx.sv` | 005 | AD711xx AFE 제어 |
| `rtl/roic/afe_spi_master.sv` | 005 | AFE SPI 마스터 (공통) |
| `rtl/roic/afe_afe2256.sv` | 006 | AFE2256 제어 |
| `rtl/roic/line_data_rx.sv` | 007 | LVDS 수신 |
| `rtl/roic/line_buf_ram.sv` | 007 | BRAM 라인 버퍼 |
| `rtl/common/data_out_mux.sv` | 007 | 데이터 출력 MUX |
| `rtl/common/mcu_data_if.sv` | 007 | MCU 데이터 인터페이스 (legacy) |
| `rtl/common/csi2_packet_builder.sv` | 007 | CSI-2 패킷 조립 (신규) |
| `rtl/common/csi2_lane_dist.sv` | 007 | CSI-2 2/4-lane 분배 (신규) |
| `rtl/common/prot_mon.sv` | 008 | 과노출 보호 |
| `rtl/common/emergency_shutdown.sv` | 008 | 비상 셧다운 |
| `rtl/common/power_sequencer.sv` | 008 | 전원 시퀀서 |
| `rtl/top/fpga_top_c1.sv` | 009 | C1 통합 Top |

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

### v2 SPEC Decomposition

| SPEC-ID | Title |
|---------|-------|
| SPEC-FPD-011 | External Memory Interface (SRAM/DDR MIG) |
| SPEC-FPD-012 | Offset Subtraction Pipeline (streaming, ext mem) |
| SPEC-FPD-013 | Gain Multiplication Pipeline (fixed-point, ext mem) |
| SPEC-FPD-014 | Defect Pixel Replacement (3×3, 2-line buffer) |
| SPEC-FPD-015 | LTI Lag Correction (4-exponential, ext mem state) |
| SPEC-FPD-016 | Forward Bias Control |
| SPEC-FPD-017 | Frame Buffer + Multi-frame Averaging |
| SPEC-FPD-018 | v2 Integration & Calibration Validation |

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

Version: 3.1.0
Created: 2026-03-19
Updated: 2026-03-19
Based on: docs/fpga-design/ (4 files), docs/research/ (7 files), docs/datasheet/ (10 files)
Changes:
- v3.0: v1 SPEC 10개 세분화 — Acceptance Criteria, Module Mapping, Timing Constraints, Register Map, Test Plan, Dependencies 추가
- v3.1: SW-First 검증 방법론 (C++ Golden Model + cocotb + Verilator), CSI-2 MIPI TX 출력 구조 (csi2_packet_builder, csi2_lane_dist), TDD 개발 사이클 추가
