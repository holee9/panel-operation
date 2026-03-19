# Acceptance Criteria: SPEC-FPD-SIM-001

## SW-First Verification Framework

---

## AC-SIM-001: C++ Golden Model — SPI Slave (SPEC-FPD-001)

**Given** SpiSlaveModel 골든 모델이 SPI Mode 0 (CPOL=0, CPHA=0)으로 초기화된 상태에서
**When** 32개 레지스터 (0x00-0x1F)에 순차적으로 쓰기 후 읽기를 수행하면
**Then** 읽기 값이 쓰기 값과 비트 동일해야 하며, Read-only 레지스터 (0x01, 0x14, 0x15, 0x1F)는 쓰기 시 값이 변경되지 않아야 한다

---

## AC-SIM-002: C++ Golden Model — Panel FSM (SPEC-FPD-002)

**Given** PanelFsmModel이 IDLE 상태에서 REG_MODE=000 (STATIC)으로 설정된 상태에서
**When** REG_CTRL[0]=START가 설정되면
**Then** FSM이 IDLE→RESET→INTEGRATE→READOUT_INIT→SCAN_LINE→READOUT_DONE→DONE 경로를 순차적으로 전이해야 하며, DONE 후 IDLE로 자동 복귀해야 한다 (CONTINUOUS가 아니므로 반복 없음)

---

## AC-SIM-003: C++ Golden Model — Panel FSM Timeout (SPEC-FPD-002)

**Given** PanelFsmModel이 TRIGGERED 모드 (REG_MODE=010)에서 INTEGRATE 상태로 진입한 상태에서
**When** X_RAY_READY 신호가 5초 동안 수신되지 않으면
**Then** FSM이 ERROR 상태로 전이하고 REG_ERR_CODE (0x15)에 ERR_XRAY_TIMEOUT 값이 설정되어야 한다

---

## AC-SIM-004: C++ Golden Model — Gate NV1047 (SPEC-FPD-003)

**Given** GateNv1047Model이 REG_NROWS=2048, REG_SCAN_DIR=0 (정방향)으로 설정된 상태에서
**When** 전체 행 스캔이 실행되면
**Then** SD1 시프트 시퀀스가 row 0→2047 순서로 출력되어야 하며, 인접 행 간 Gate ON/OFF 오버랩이 없어야 하고 (break-before-make), CLK 주기가 5 us 이상이어야 한다

---

## AC-SIM-005: C++ Golden Model — Gate NV1047 Timing (SPEC-FPD-003)

**Given** GateNv1047Model이 REG_TGATE_ON=30 (30 us)으로 설정된 상태에서
**When** Gate ON 펄스가 생성되면
**Then** OE 펄스 폭이 28-32 us 범위 (+-2 us) 내에 있어야 하며, T_gate_settle이 2 us 이상이어야 한다

---

## AC-SIM-006: C++ Golden Model — AFE AD71124 (SPEC-FPD-005)

**Given** AfeAd711xxModel이 AD71124 모드 (IFS 6-bit)로 구성되고 ACLK=10 MHz인 상태에서
**When** SYNC 펄스가 tLINE=2200 (22 us)으로 적용되면
**Then** 256채널 16비트 출력 데이터가 22 us 이내에 완료되어야 하며, 출력이 AD71124 데이터시트 변환 시퀀스와 일치해야 한다

---

## AC-SIM-007: C++ Golden Model — AFE AD71143 (SPEC-FPD-005)

**Given** AfeAd711xxModel이 AD71143 모드 (IFS 5-bit)로 구성되고 ACLK=10 MHz인 상태에서
**When** SYNC 펄스가 tLINE=6000 (60 us)으로 적용되면
**Then** 256채널 16비트 출력이 60 us 이내에 완료되어야 하며, IFS 5비트 인코딩이 올바르게 적용되어야 한다

---

## AC-SIM-008: C++ Golden Model — AFE2256 Pipeline (SPEC-FPD-006)

**Given** AfeAfe2256Model이 CIC 활성화 (REG_CIC_EN=1) + Pipeline 모드로 구성된 상태에서
**When** 연속 2개 행 (row[n-1], row[n])이 처리되면
**Then** row[n-1] ADC 리드아웃과 row[n] 적분이 동시에 수행되어야 하며, 파이프라인 지연이 정확히 1-row이어야 한다

---

## AC-SIM-009: C++ Golden Model — CSI-2 Packet CRC (SPEC-FPD-007)

**Given** Csi2PacketModel이 2048x1 RAW16 라인 데이터 (4096 bytes)를 패킷으로 조립하는 상태에서
**When** Long Packet이 생성되면
**Then** 패킷 구조가 [DI(1B) + WC(2B) + ECC(1B) + Payload(4096B) + CRC(2B)]이어야 하며, CRC-16은 CCITT 다항식 (0x1021)으로 계산되어 페이로드와 비트 일치해야 한다

---

## AC-SIM-010: C++ Golden Model — CSI-2 ECC (SPEC-FPD-007)

**Given** Csi2PacketModel이 생성한 Long Packet 헤더에서
**When** 헤더 3바이트 (DI + WC_lo + WC_hi)에 대해 ECC가 계산되면
**Then** ECC 바이트가 MIPI CSI-2 Annex A 알고리즘과 일치해야 하며, 1-bit 에러 주입 시 ECC로 정정 가능해야 한다

---

## AC-SIM-011: CSI-2 2-Lane Byte Interleaving (SPEC-FPD-007)

**Given** Csi2LaneDistModel이 2-lane 모드 (C1-C5)로 설정된 상태에서
**When** 256 bytes 페이로드가 분배되면
**Then** Lane 0에 bytes [0,4,8,...,252]가, Lane 1에 bytes [1,5,9,...,253]이 할당되어야 하며, 나머지 bytes [2,6,...] 및 [3,7,...]도 올바르게 분배되어야 한다

---

## AC-SIM-012: CSI-2 4-Lane Byte Interleaving (SPEC-FPD-007)

**Given** Csi2LaneDistModel이 4-lane 모드 (C6-C7)로 설정된 상태에서
**When** 256 bytes 페이로드가 분배되면
**Then** Lane N (N=0,1,2,3)에 bytes [N, N+4, N+8, ...]이 할당되어야 한다

---

## AC-SIM-013: Line Buffer Ping-Pong Swap (SPEC-FPD-007)

**Given** LineBufModel이 Bank A에 2048 pixels 쓰기를 완료한 상태에서
**When** 라인 완료 신호가 발생하면
**Then** Bank A↔B 스왑이 1 SYS_CLK 이내에 완료되어야 하며, Bank B (이전 쓰기)의 데이터가 CSI-2 TX와 MCU에 동시 가용해야 한다

---

## AC-SIM-014: CDC FIFO Stress Test (SPEC-FPD-007)

**Given** LineBufModel의 CDC FIFO가 DCLK=20 MHz, SYS_CLK=100 MHz로 동작하는 상태에서
**When** 24개 AFE가 동시에 데이터를 전송하고 FIFO가 75% 용량에 도달하면
**Then** 1000개 연속 라인 전송 동안 데이터 손실 0건, overflow 이벤트 0건이어야 한다

---

## AC-SIM-015: Safety — Emergency Shutdown (SPEC-FPD-008)

**Given** ProtMonModel이 정상 동작 중인 상태에서
**When** VGH > 38V 과전압 조건이 주입되면
**Then** emergency_shutdown이 10,000 SYS_CLK cycles (100 us at 100 MHz) 이내에 활성화되어야 하며, 모든 Gate 출력이 OFF 되어야 한다

---

## AC-SIM-016: Safety — Power Sequence (SPEC-FPD-008)

**Given** PowerSeqModel이 S_OFF 상태에서 전원 인가가 시작된 상태에서
**When** 전원 시퀀스가 진행되면
**Then** VGL이 VGH보다 반드시 먼저 인가되어야 하며, VGL 안정화 (10 ms) 후 VGH가 인가되고, 전압 변화율이 5 V/ms 이하여야 한다

---

## AC-SIM-017: cocotb — RTL vs Golden Bit-Exact Match (SPEC-FPD-001)

**Given** cocotb test_spi_slave.py가 spi_slave_if.sv RTL에 대해 실행되는 상태에서
**When** C++ 골든 모델이 생성한 32개 레지스터 R/W 테스트 벡터가 적용되면
**Then** RTL MISO 출력이 골든 모델 출력과 모든 트랜잭션에서 비트 동일해야 하며, 9개 테스트 케이스 (TB-001-1 ~ TB-001-9) 전체가 PASS해야 한다

---

## AC-SIM-018: Verilator — Full Frame Comparison (SPEC-FPD-007)

**Given** Verilated csi2_packet_builder.sv가 csi2_tx_model.cpp과 연결된 상태에서
**When** 2048x2048 전체 프레임 RAW16 데이터가 전송되면
**Then** 시뮬레이션이 60초 이내에 완료되어야 하며, 모든 패킷 헤더 및 CRC 필드가 골든 모델과 비트 일치해야 한다

---

## AC-SIM-019: Build System — Cross-Platform (NFR-2)

**Given** sim/ 디렉토리에서 clean checkout 상태에서
**When** Windows (MSVC 2022)와 Linux (GCC 11) 양 플랫폼에서 `cmake --build .`가 실행되면
**Then** 골든 모델 라이브러리와 모든 테스트 벡터 생성기가 에러 및 경고 없이 컴파일되어야 한다

---

## AC-SIM-020: CI/CD — Per-SPEC Test Duration (NFR-4)

**Given** CI/CD 파이프라인에서 SPEC-FPD-001의 cocotb 테스트가 실행되는 상태에서
**When** 9개 테스트 케이스 (TB-001-1 ~ TB-001-9)가 Vivado xsim으로 실행되면
**Then** 전체 테스트 스위트가 30분 이내에 완료되어야 하며, 결과가 JUnit XML 형식으로 출력되어야 한다

---

## Edge Case Scenarios

### EC-SIM-001: SPI Contention

**Given** SpiSlaveModel에서 외부 SPI 쓰기와 내부 상태 업데이트가 동시에 발생할 때
**When** 동일 레지스터에 대해 경합이 발생하면
**Then** 내부 업데이트가 SPI 쓰기보다 우선해야 한다 (내부 > SPI 우선순위)

### EC-SIM-002: FIFO Near-Full

**Given** CDC FIFO가 depth-1 상태에서
**When** 추가 데이터 쓰기가 시도되면
**Then** backpressure 신호가 활성화되어야 하며, 데이터 손실 없이 쓰기가 지연되어야 한다

### EC-SIM-003: Panel FSM ABORT During Any State

**Given** PanelFsmModel이 INTEGRATE, READOUT, 또는 RESET 상태에 있을 때
**When** REG_CTRL[1]=ABORT가 설정되면
**Then** 2 SYS_CLK cycles 이내에 IDLE 상태로 복귀해야 한다

### EC-SIM-004: AFE Daisy-Chain 24-Chip

**Given** AfeSpiMasterModel이 24 AFE 데이지체인 (576 bits)으로 구성된 상태에서
**When** 전체 프레임 SPI 설정이 전송되면
**Then** 마지막 AFE (#23)의 설정이 올바르게 적용되어야 하며, 전체 전송이 SPI CLK <= ACLK/4 조건을 충족해야 한다

### EC-SIM-005: CSI-2 LP Mode Transition

**Given** Csi2LaneDistModel이 프레임 전송을 완료한 상태에서
**When** 다음 프레임 시작까지 유휴 기간이 존재하면
**Then** HS→LP 전환이 발생해야 하며, 다음 프레임 시작 시 LP→HS 전환이 1 us 이내에 완료되어야 한다

---

## Coverage Matrix

| SPEC | Golden Model AC | cocotb AC | Verilator AC | Total |
|------|----------------|-----------|--------------|-------|
| SPEC-FPD-001 | AC-SIM-001 | AC-SIM-017 | - | 2 |
| SPEC-FPD-002 | AC-SIM-002, 003 | - | - | 2 |
| SPEC-FPD-003 | AC-SIM-004, 005 | - | - | 2 |
| SPEC-FPD-005 | AC-SIM-006, 007 | - | - | 2 |
| SPEC-FPD-006 | AC-SIM-008 | - | - | 1 |
| SPEC-FPD-007 | AC-SIM-009~014 | AC-SIM-011, 012 | AC-SIM-018 | 9 |
| SPEC-FPD-008 | AC-SIM-015, 016 | - | - | 2 |
| Build/CI | - | - | AC-SIM-019, 020 | 2 |
| **Total** | **14** | **3** | **3** | **20** |

---

Version: 1.0.0
Created: 2026-03-19
