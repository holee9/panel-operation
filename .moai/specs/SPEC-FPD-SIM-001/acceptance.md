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

## AC-SIM-021: 24-AFE Full Data Path Integration (SPEC-FPD-007 + 009)

**Given** C6 하드웨어 조합 (NT39565D x6 + AD71124 x12)의 전체 데이터 경로 골든 모델이 구성된 상태에서
**When** 3072 행 스캔이 실행되고 12개 AFE가 동시에 LVDS 데이터를 출력하면
**Then** 12개 AFE의 256ch x 16-bit 데이터가 올바르게 line buffer에 정렬되어야 하며 (AFE 0 first → AFE 11 last = 3072 pixels/line), CSI-2 4-lane TX로 전체 프레임 (3072x3072 RAW16)이 무결하게 전송되어야 한다

---

## AC-SIM-022: NT39565D 6-Chip Cascade Propagation (SPEC-FPD-004)

**Given** GateNt39565dModel이 6-chip 캐스케이드 모드로 초기화된 상태에서
**When** STV1 스타트 펄스가 chip[0]에 인가되면
**Then** STVD 신호가 chip[0]→chip[1]→...→chip[5]로 순차 전파되어야 하며, 마지막 chip[5]의 STVD 수신 시 cascade_done이 assert되어야 한다. 전체 3072 gate lines (541ch x 6)이 1회 스캔으로 완료되어야 한다

---

## AC-SIM-023: Radiography 30s X_RAY_READY Timeout (SPEC-FPD-010)

**Given** RadiogModel이 radiography sub-FSM의 S4_XRAY_READY_WAIT 상태에 있는 상태에서
**When** X_RAY_READY 신호가 30초 동안 수신되지 않으면
**Then** FSM이 ERROR 상태로 전이하고 ERR_XRAY_TIMEOUT이 설정되어야 하며, 이 타임아웃은 SPEC-002의 일반 5초 타임아웃과 독립적으로 radiography 모드 전용 30초로 동작해야 한다

---

## AC-SIM-024: Dark Frame 64-Frame Acquisition (SPEC-FPD-010)

**Given** RadiogModel이 DARK_FRAME 모드 (REG_MODE=011)로 설정된 상태에서
**When** 다크 프레임 연속 획득이 실행되면
**Then** Gate OFF를 유지한 채 AFE 리드아웃만 64회 연속 실행되어야 하며, 64 프레임 완료 후 정상 종료되어야 한다

---

## AC-SIM-025: CONTINUOUS Mode Auto-Repeat (SPEC-FPD-002)

**Given** PanelFsmModel이 REG_MODE=001 (CONTINUOUS)로 설정되고 START가 발생한 상태에서
**When** 첫 번째 프레임이 DONE 상태에 도달하면
**Then** ABORT 없이 자동으로 RESET 상태로 전이하여 다음 프레임을 시작해야 하며, 3회 이상 연속 반복이 검증되어야 한다

---

## AC-SIM-026: AFE2256 tLINE Boundary (SPEC-FPD-006)

**Given** AfeAfe2256Model이 MCLK=32 MHz로 구성된 상태에서
**When** tLINE=5120 (51.2 us, 최소값)으로 SYNC가 적용되면
**Then** 256채널 16비트 출력이 51.2 us 이내에 완료되어야 하며, tLINE < 5120 설정 시 에러가 감지되어야 한다

---

## AC-SIM-027: GoldenModelBase Interface Contract (Infrastructure)

**Given** GoldenModelBase를 상속한 임의의 골든 모델이 인스턴스화된 상태에서
**When** reset() → set_inputs() → step() → get_outputs() → compare() 시퀀스가 호출되면
**Then** 각 메서드가 정의된 계약에 따라 동작해야 하며, compare()는 불일치 시 Mismatch 리스트를 반환하고, cycle()은 step() 호출마다 1씩 증가해야 한다

---

## AC-SIM-028: Test Vector Dual-Format Output (Infrastructure)

**Given** 임의의 테스트 벡터 생성기가 실행된 상태에서
**When** generate_vectors()가 출력 디렉토리를 지정받으면
**Then** hex 형식 (.hex, cocotb용)과 binary 형식 (.bin, Verilator용) 파일이 모두 생성되어야 하며, 각 파일에 @MODULE, @SPEC, @SIGNALS_IN, @SIGNALS_OUT 메타데이터가 포함되어야 한다

---

## AC-SIM-029: AFE2256 CIC Variant Validation (C3, C5, C7)

**Given** AfeAfe2256Model이 CIC 활성화 상태(C3/C5/C7)로 구성되었을 때
**When** CIC compensation이 적용된 readout이 수행되면
**Then** CIC 보상된 출력이 비-CIC 출력과 구분되며, CIC 프로파일 레지스터 설정이 올바르게 반영되어야 한다

---

## AC-SIM-030: AD71143 Low-Power Variant Validation (C2)

**Given** AfeAd711xxModel이 AD71143 모드(IFS_WIDTH=5, tLINE>=6000)로 구성되었을 때
**When** 저전력 모드 readout이 수행되면
**Then** tLINE이 60us 이상이고, IFS가 5-bit 범위(0~31) 내에서 동작해야 한다

---

## AC-SIM-031: Non-Square Panel Validation (C4, C5)

**Given** PanelFsmModel이 R1714 패널(2048x1680, 비정방형)로 구성되었을 때
**When** 전체 프레임 readout이 수행되면
**Then** row count가 1680이고, 올바른 프레임 크기(2048x1680x16bit)가 생성되어야 한다

---

## AC-SIM-032: REG_LINE_IDX Update Verification

**Given** panel_ctrl_fsm이 SCAN_LINE 상태에서 row scan이 진행 중일 때
**When** 각 행이 완료되면
**Then** REG_LINE_IDX가 현재 행 번호(0~N-1)로 업데이트되어야 한다

---

## AC-SIM-033: CDC Reset Synchronization Verification

**Given** 다중 클럭 도메인(SYS_CLK, ACLK, MCLK, DCLK)이 활성화된 상태에서
**When** async reset이 assert되면
**Then** 모든 클럭 도메인에서 리셋이 2-FF 동기화를 거쳐 안전하게 전파되어야 한다

---

## AC-SIM-034: Mismatch Struct Verification

**Given** GoldenModelBase.compare()가 RTL 출력과 비교를 수행할 때
**When** 불일치가 발견되면
**Then** Mismatch 구조체에 cycle(uint64), signal_name(string), expected(uint32), actual(uint32) 필드가 포함되어야 한다

---

## AC-SIM-035: Combo-Specific TLINE_MIN Enforcement (R-SIM-041)

**Given** REG_COMBO = C2 (AD71143) AND REG_TLINE = 2200 (22 us)으로 설정된 상태에서
**When** Panel FSM이 SCAN_LINE 상태에 진입하면
**Then** 유효 tLINE이 TLINE_MIN = 6000 (60 us)으로 클램핑되어야 하며, 골든 모델 출력이 RTL의 TLINE_MIN 적용 동작과 일치해야 한다

---

## AC-SIM-036: Combo-Specific NCOLS Validation (R-SIM-041)

**Given** REG_COMBO = C4 (R1714)으로 설정된 상태에서
**When** Register bank이 기본값을 초기화하면
**Then** REG_NCOLS가 1664 (2048이 아닌)로 기본 설정되어야 하며, C6/C7의 경우 3072로 기본 설정되어야 한다

---

## AC-SIM-037: Generator Handshake Timing (R-SIM-042)

**Given** REG_MODE = TRIGGERED (radiography)로 설정된 상태에서
**When** FSM이 적분 시퀀스에 진입하면
**Then** PREP_REQUEST → X_RAY_ENABLE 지연이 200ms~2s 범위여야 하고, X_RAY_READY 타임아웃이 30s여야 하며, 골든 모델이 RTL과 동일하게 핸드셰이크 상태 전이를 추적해야 한다

---

## AC-SIM-038: Settle Time Verification (R-SIM-043)

**Given** X_RAY_OFF가 적분 중 감지된 상태에서
**When** FSM이 INTEGRATE에서 readout으로 전이하면
**Then** Gate scan 시작 전에 설정 가능한 settle 지연 (cfg_tsettle, 기본값 100000 cycles = 100 MHz에서 1 ms)이 경과해야 하며, 골든 모델도 이 지연을 동일하게 적용해야 한다

---

## AC-SIM-039: Multi-AFE Data Path (R-SIM-044)

**Given** cfg_combo = C6, cfg_afe_nchip = 12로 설정된 상태에서
**When** Full-frame readout (3072 rows x 3072 cols)이 수행되면
**Then** 12개 LVDS 수신기 인스턴스가 3072 pixels/line을 생성해야 하며 AFE 간 데이터 오염이 없어야 하고, line buffer가 3072 pixels 전체를 저장해야 한다

---

## AC-SIM-040: Gate NV1047 Shift Register (R-SIM-046)

**Given** NV1047 골든 모델이 row_index = 150으로 설정된 상태에서
**When** gate_on_pulse가 assert되면
**Then** SD1/SD2가 CLK 펄스 (<=200 kHz)를 통해 row address를 비트 단위로 직렬화해야 하고, OE가 cfg_tgate_on cycles 동안 assert되어야 하며, break-before-make 간격이 >=2 us여야 한다

---

## AC-SIM-041: Gate NT39565D Dual-STV (R-SIM-047)

**Given** NT39565D 모델이 6-chip 캐스케이드로 구성된 상태에서
**When** Gate scan이 row 0→540 (chip #0)에서 실행되면
**Then** STV1이 홀수 행, STV2가 짝수 행을 선택해야 하고, OE1_L/OE1_R이 좌/우를 독립 제어해야 하며, STVD 전파가 chip[0]에서 chip[5]까지 6 CPV cycles 이내에 완료되어야 한다

---

## AC-SIM-042: LVDS Format Discrimination (R-SIM-048)

**Given** afe_type_sel = ADI (AD71124)로 설정된 상태에서
**When** LVDS 수신기 모델이 데이터를 처리하면
**Then** 모델이 3-pair 포맷 (DOUT_A/B + DCLK)을 사용해야 하며, afe_type_sel = TI (AFE2256)인 경우 4-pair 포맷 (DOUT + DCLK + FCLK)을 사용해야 한다

---

## AC-SIM-043: Power Mode VGL→VGH Delay (R-SIM-049)

**Given** Power sequencer가 PWR_ACTIVE 모드를 목표로 하는 상태에서
**When** VGL enable이 assert되면
**Then** VGH enable은 VGL_STABLE AND >=5 ms 지연이 경과할 때까지 assert되지 않아야 하며, slew rate가 <=5 V/ms여야 한다. 골든 모델도 동일한 시퀀싱을 적용해야 한다

---

## AC-SIM-044: Dark Frame Averaging (R-SIM-050)

**Given** REG_MODE = DARK_FRAME, REG_DARK_CNT = 4로 설정된 상태에서
**When** 4개 dark frame이 완료되면
**Then** 픽셀별 평균이 계산되어야 하고 (sum/4), dark offset map이 감산용으로 가용해야 한다

---

## AC-SIM-045: Panel FSM Extended States (R-SIM-051)

**Given** v1-extended FSM 모드가 활성화된 상태에서
**When** STATIC 모드 acquisition 시퀀스가 실행되면
**Then** FSM이 core 8 states에 추가로 S1_POWER_CHECK → S3_PREP_WAIT → S5_XRAY_ENABLE → S7_SETTLE 상태를 순차 전이해야 하며, 골든 모델이 RTL과 state-by-state로 일치해야 한다

---

## AC-SIM-046: CIC Compensation (R-SIM-052)

**Given** cfg_combo = C3 (AFE2256), cfg_cic_en = 1, cfg_cic_profile = 0으로 설정된 상태에서
**When** AFE readout이 256 channels를 완료하면
**Then** CIC 보상된 출력이 비-CIC 출력과 차이가 있어야 하며, dynamic range 개선이 측정 가능해야 한다 (>=40% 개선 메트릭)

---

## AC-SIM-047: Golden Model Timing Parameterization (R-SIM-051)

**Given** PanelFsmModel이 cfg_treset=100, cfg_tinteg=1000으로 초기화된 상태에서
**When** FSM이 RESET→INTEGRATE 시퀀스를 실행하면
**Then** RESET 상태가 정확히 cfg_treset cycles (하드코딩된 4가 아닌) 동안 유지되어야 하고, INTEGRATE가 cfg_tinteg cycles (하드코딩된 2-30이 아닌) 동안 유지되어야 한다. 골든 모델에 하드코딩된 타이밍 상수가 존재하지 않아야 한다

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

### EC-SIM-006: Multi-AFE SYNC Skew

**Given** 12개 AFE가 동시 동작하고 MCLK skew가 +-31 ns 범위에서 발생하는 상태에서
**When** SYNC 신호가 모든 AFE에 브로드캐스트되면
**Then** skew에도 불구하고 데이터 정렬이 유지되어야 하며, 최악 조건에서도 데이터 손실이 없어야 한다

### EC-SIM-007: VGL-without-VGH Latch-Up Detection

**Given** Power sequencer에서 VGL이 활성화된 상태에서 VGH가 비활성 상태일 때
**When** VGH 없이 VGL만 일정 시간 이상 유지되면
**Then** seq_error 플래그가 assert되어야 하며, latch-up 방지를 위해 emergency shutdown이 트리거되어야 한다

### EC-SIM-008: TLINE Below Minimum with Combo Mismatch

**Given** REG_COMBO = C2 (AD71143, TLINE_MIN=6000)이지만 REG_TLINE이 기본값 2200으로 설정된 상태에서
**When** Panel FSM이 readout을 시작하면
**Then** TLINE_MIN 클램핑이 적용되어 실제 tLINE=6000으로 동작해야 하며, 에러 플래그 없이 정상 동작해야 한다

### EC-SIM-005: CSI-2 LP Mode Transition

**Given** Csi2LaneDistModel이 프레임 전송을 완료한 상태에서
**When** 다음 프레임 시작까지 유휴 기간이 존재하면
**Then** HS→LP 전환이 발생해야 하며, 다음 프레임 시작 시 LP→HS 전환이 1 us 이내에 완료되어야 한다

---

## Coverage Matrix

| SPEC | Golden Model AC | cocotb AC | Verilator AC | Total |
|------|----------------|-----------|--------------|-------|
| SPEC-FPD-001 | AC-SIM-001 | AC-SIM-017 | - | 2 |
| SPEC-FPD-002 | AC-SIM-002, 003, **025**, **031**, **032**, **035**, **036**, **045**, **047** | - | - | 9 |
| SPEC-FPD-003 | AC-SIM-004, 005, **040** | - | - | 3 |
| SPEC-FPD-004 | AC-SIM-022, **041** | - | - | 2 |
| SPEC-FPD-005 | AC-SIM-006, 007, **030**, **042** | - | - | 4 |
| SPEC-FPD-006 | AC-SIM-008, **026**, **029**, **046** | - | - | 4 |
| SPEC-FPD-007 | AC-SIM-009~014 | AC-SIM-011, 012 | AC-SIM-018 | 9 |
| SPEC-FPD-007+009 | AC-SIM-021, **039** | - | - | 2 |
| SPEC-FPD-008 | AC-SIM-015, 016, **043** | - | - | 3 |
| SPEC-FPD-010 | **AC-SIM-023, 024**, **037**, **038**, **044** | - | - | 5 |
| CDC | **AC-SIM-033** | - | - | 1 |
| Build/CI | - | - | AC-SIM-019, 020 | 2 |
| Infrastructure | **AC-SIM-027, 028**, **034** | - | - | 3 |
| **Total** | **41** | **3** | **3** | **47** |

---

## Traceability Matrix

| AC-ID | R-ID | Module | Description |
|-------|------|--------|-------------|
| AC-SIM-001 | R-SIM-001, R-SIM-002 | SPI Slave | SPI Mode 0/3, 32-register R/W |
| AC-SIM-002 | R-SIM-003 | Panel FSM | 7-state FSM, 5 modes |
| AC-SIM-003 | R-SIM-004, R-SIM-007 | Panel FSM | CONTINUOUS auto-repeat, timeout |
| AC-SIM-004 | R-SIM-008 | Gate NV1047 | SD1 shift register, OE/ONA |
| AC-SIM-005 | R-SIM-008 | Gate NV1047 | Gate settle timing |
| AC-SIM-006 | R-SIM-005 | AFE AD711xx | ACLK/SYNC timing |
| AC-SIM-007 | R-SIM-005 | AFE AD711xx | ADC conversion cycle |
| AC-SIM-008 | R-SIM-006 | AFE AFE2256 | Pipeline 1-row delay |
| AC-SIM-009 | R-SIM-011, R-SIM-012, R-SIM-013 | CSI-2 TX | Packet structure + CRC + ECC |
| AC-SIM-010 | R-SIM-014, R-SIM-015 | CSI-2 Lane | 2/4-lane interleaving |
| AC-SIM-011 | R-SIM-017, R-SIM-018 | Line Buffer | CDC async FIFO |
| AC-SIM-012 | R-SIM-019 | Line Buffer | Ping-pong bank swap |
| AC-SIM-013 | R-SIM-020, R-SIM-021 | Multi-AFE | 24-AFE data alignment |
| AC-SIM-014 | R-SIM-022 | Test Vector | Standard format |
| AC-SIM-015 | R-SIM-023 | Test Vector | Dual format (hex/binary) |
| AC-SIM-016 | R-SIM-010 | Power Seq | VGL→VGH sequence |
| AC-SIM-017 | R-SIM-024 | Verilator | Behavioral wrapper |
| AC-SIM-018 | R-SIM-025 | Verilator | Cycle-accurate comparison |
| AC-SIM-019 | R-SIM-026 | cocotb | Python testbench |
| AC-SIM-020 | R-SIM-027 | cocotb | Regression suite |
| AC-SIM-021 | R-SIM-009 | Gate NT39565D | 6-chip cascade sync |
| AC-SIM-022 | R-SIM-009 | Gate NT39565D | STVD propagation |
| AC-SIM-023 | R-SIM-004, R-SIM-038 | Panel FSM | Radiography 30s timeout |
| AC-SIM-024 | R-SIM-007 | Panel FSM | DARK_FRAME mode |
| AC-SIM-025 | R-SIM-028 | Build System | CMake cross-platform |
| AC-SIM-026 | R-SIM-006 | AFE AFE2256 | SYNC timing |
| AC-SIM-027 | R-SIM-029 | Infrastructure | GoldenModelBase interface |
| AC-SIM-028 | R-SIM-023 | Test Vector | Dual format validation |
| AC-SIM-029 | R-SIM-006 | AFE AFE2256 | CIC variant (C3/C5/C7) |
| AC-SIM-030 | R-SIM-005 | AFE AD71143 | Low-power variant (C2) |
| AC-SIM-031 | R-SIM-003 | Panel FSM | Non-square panel (C4/C5) |
| AC-SIM-032 | R-SIM-003 | Panel FSM | REG_LINE_IDX update |
| AC-SIM-033 | R-SIM-039 | CDC | Reset synchronization |
| AC-SIM-034 | R-SIM-029 | Infrastructure | Mismatch struct |
| AC-SIM-035 | R-SIM-041 | Panel FSM | Combo-specific TLINE_MIN enforcement |
| AC-SIM-036 | R-SIM-041 | Register Bank | Combo-specific NCOLS default |
| AC-SIM-037 | R-SIM-042 | Panel FSM | Generator handshake timing |
| AC-SIM-038 | R-SIM-043 | Panel FSM | Settle time verification |
| AC-SIM-039 | R-SIM-044 | Multi-AFE | Multi-AFE data path (12 LVDS) |
| AC-SIM-040 | R-SIM-046 | Gate NV1047 | Shift register serialization |
| AC-SIM-041 | R-SIM-047 | Gate NT39565D | Dual-STV odd/even row control |
| AC-SIM-042 | R-SIM-048 | LVDS Receiver | ADI vs TI format discrimination |
| AC-SIM-043 | R-SIM-049 | Power Seq | VGL→VGH delay enforcement |
| AC-SIM-044 | R-SIM-050 | Panel FSM | Dark frame averaging |
| AC-SIM-045 | R-SIM-051 | Panel FSM | Extended FSM states |
| AC-SIM-046 | R-SIM-052 | AFE AFE2256 | CIC compensation validation |
| AC-SIM-047 | R-SIM-051 | Panel FSM | Timing parameterization (no hardcode) |

---

Version: 1.2.0
Created: 2026-03-19
Updated: 2026-03-21
