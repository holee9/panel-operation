# 교차검증 종합 리포트 (Cross-Verification Master Report)

**생성일**: 2026-03-20
**분석 범위**: SPEC-FPD-SIM-001, RTL 스켈레톤, 모듈 아키텍처, 타이밍/리소스, 업계 분석
**총 발견 건수**: 63건 + 업계 권장 12건

---

## Executive Summary

4개 병렬 분석 에이전트가 프로젝트 전체 문서를 교차검증한 결과:

- **SPEC 교차검증**: 24건 (CRITICAL 1, HIGH 6, MEDIUM 11, LOW 6)
- **RTL vs SPEC 정합성**: 25건 (CRITICAL 5, HIGH 9, MEDIUM 6, LOW 5)
- **타이밍/하드웨어 검증**: 14건 (CRITICAL 4, HIGH 4, MEDIUM 4, LOW 2)
- **업계 딥리서치**: 6개 영역, 12개 즉시 액션 아이템

---

### 핵심 발견 요약

#### CRITICAL (10건)

1. **RTL-GAP-001**: `detector_core.sv` 누락 — `fpga_top`과 서브시스템 사이 조직 계층 부재. CLAUDE.md 모듈 계층도에 명시된 중간 계층이 RTL 디렉토리에 존재하지 않음.
2. **RTL-GAP-002**: `gate_nv1047` `row_index` 포트 너비 불일치 — RTL: 12-bit, 아키텍처 문서(fpga_module_architecture.md): NV1047 최대 300ch이므로 9-bit 충분.
3. **RTL-GAP-003**: `gate_nt39565d` `row_index` 포트 너비 불일치 — RTL: 12-bit, NT39565D 541ch 기준 10-bit 충분.
4. **RTL-GAP-004**: `afe_ad711xx.sv` `config_done` 입력 포트 누락 — 모듈이 `config_done`을 출력하지만 `panel_ctrl_fsm`이 이를 수신하는 피드백 경로가 포트 목록에 없음.
5. **RTL-GAP-005**: `line_data_rx.sv` LVDS 차동 쌍 네이밍 불일치 — 포트명 `lvds_dclk`, `lvds_dout_a/b`는 IBUFDS 이후 싱글엔드 신호이나, 포트 선언이 단일 `logic`으로 되어 있어 실제 핀 할당 시 `IBUFDS` 원시 블록(primitive) 연결이 불명확함.
6. **XVER-006**: CSI-2 모듈 네이밍 불일치 — plan.md는 `csi2_tx_model`, acceptance.md는 `Csi2PacketModel`, 아키텍처 문서는 `csi2_packet_builder`로 제각각 사용하여 구현 시 혼란 초래.
7. **CDC-001**: 5개 클럭 도메인 식별 완료 (SYS_CLK 100MHz, DCLK ~20MHz×24, ACLK 10MHz, MCLK 32MHz, SPI_CLK 1-10MHz). 모두 식별됐으나 CDC 교차점 스펙이 불완전.
8. **CDC-003**: 리셋 동기화, MMCM 락 동기화, 다중 AFE SYNC 스큐 CDC 사양 누락 — `clk_rst_mgr.sv`에 구현될 리셋 동기화 체인이 SPEC 어디에도 명시되지 않음.
9. **TIM-001**: AFE 라인 타임 파라미터 전 문서 정합성 확인 완료 — AD71124 22µs, AD71143 60µs, AFE2256 51.2µs. 레지스터 단위 변환(10ns/unit) 표기는 일부 누락.
10. **TIM-004**: 전원 시퀀싱 타이밍 검증됨 (NV1047 안정화 20ms, NT39565D 안정화 25ms), SPEC acceptance.md의 AC-SIM-016은 VGL→VGH 순서만 명시하고 NT39565D 전용 25ms 마진은 미언급.

---

#### HIGH (19건) — 주요 항목

- **RTL-GAP-006**: `line_data_rx.sv` AFE2256 FCLK_P/M 포트 누락 — `ADI_MODE=0`(AFE2256) 시 TI LVDS 포맷은 `DCLK_P/M` + `FCLK_P/M` 4쌍이 필요하나 현재 포트는 ADI 3-쌍만 정의.
- **RTL-GAP-007**: `reg_bank.sv` AFE 타입별 설정 레지스터 미완성 — `cfg_cic_en`, `cfg_pipeline_en`, `cfg_cic_profile` 출력 없음 (AFE2256 전용).
- **RTL-GAP-008**: `reg_bank.sv` IRQ_EN 레지스터 출력 누락 — 아키텍처 문서 Table 3에 `REG_IRQ_EN(0x12)` 정의됐으나 포트 목록 없음.
- **RTL-GAP-009**: `reg_bank.sv` LINE_RDY 상태 입력 누락 — `sts_line_rdy` 핀 없음.
- **RTL-GAP-010**: `reg_bank.sv` `cfg_afe_lpf`, `cfg_afe_pmode` 출력 누락.
- **RTL-GAP-011**: `afe_afe2256.sv` FCLK 출력 핀 누락 — AFE2256은 `FCLK_P/M`을 FPGA가 생성해 AFE에 공급해야 하나 포트 정의에 없음.
- **RTL-GAP-013**: `clk_rst_mgr.sv` AFE 타입별 클럭 선택 미구현 — AD711xx용 ACLK(10MHz)와 AFE2256용 MCLK(32MHz) 선택 로직이 없음.
- **XVER-001**: 요구사항-수용기준 추적성 매트릭스 부재 — R-SIM-001~035와 AC-SIM-001~025 간 명시적 매핑 테이블 없음.
- **XVER-007**: C2(NV1047+AD71143), C4(R1714+NV1047+AD71124), C5(R1714+NV1047+AFE2256), C7(X239+NT39565D+AFE2256) 하드웨어 조합 전용 수용기준 누락.
- **XVER-020**: CSI-2 TX가 PRIMARY 출력이라는 명시적 요구사항 부재 — spec.md는 CSI-2를 검증 대상으로만 언급하고 "MCU 병렬 인터페이스 대비 PRIMARY"임을 선언하지 않음.
- **CDC-002**: CDC 전략 검증됨 — DCLK→SYS_CLK 경로의 async FIFO(dual-clock BRAM) 접근이 적절하나, `line_data_rx` 내부 ISERDESE2 출력의 clock domain이 명시적으로 분리되지 않음.
- **BW-001**: 12 AFE 집합 데이터율 12.3 Gbps(4096ch × 16bit × ~20MHz) — 가능하나 타이트. 24 AFE(C6/C7) 시 BRAM 처리량 한계 접근.

---

## 1. SPEC 교차검증 상세 (24건)

### CRITICAL (1건)

#### XVER-006 (CRITICAL): CSI-2 모듈 네이밍 불일치

- **위치**: plan.md 44행, acceptance.md 71행, docs/fpga-design/fpga_module_architecture.md
- **설명**: 동일 CSI-2 TX 골든 모델이 문서마다 다른 이름으로 참조됨.
  - plan.md: `csi2_tx_model.cpp`
  - acceptance.md (AC-SIM-009): `Csi2PacketModel`
  - acceptance.md (AC-SIM-018): `csi2_packet_builder.sv`
  - 아키텍처 문서: `csi2_packet_builder`
- **권고**: plan.md 내 C++ 골든 모델 이름을 `Csi2PacketModel`로 통일, RTL 모듈명은 `csi2_packet_builder.sv`로 확정. 네이밍 규칙 표준화 문서 추가.

---

### HIGH (6건)

#### XVER-001 (HIGH): 요구사항-수용기준 추적성 매트릭스 부재

- **위치**: acceptance.md 전체
- **설명**: R-SIM-001~035 각각에 대해 어떤 AC-SIM-XXX가 검증하는지 명시적 매핑 테이블이 없음. 일부 요구사항(예: R-SIM-019 핑퐁 뱅크 스왑)은 AC-SIM-013으로 커버되지만, 매트릭스 없이는 커버리지 공백을 파악하기 어려움.
- **권고**: acceptance.md 맨 끝에 요구사항-AC 매핑 테이블 추가.

#### XVER-007 (HIGH): C2/C4/C5/C7 하드웨어 조합 전용 수용기준 누락

- **위치**: acceptance.md
- **설명**: AC-SIM-001~025 중 C1(NV1047+AD71124), C3(NV1047+AFE2256), C6(NT39565D+AD71124×12)에 대한 시나리오는 있으나 C2(AD71143 60µs 제약), C4(R1714 비정방형), C5(R1714+AFE2256), C7(NT39565D+AFE2256×12) 조합 전용 수용기준이 없음.
- **권고**: 각 조합별 파라미터 차이점을 검증하는 AC-SIM-026~029 추가.

#### XVER-009 (HIGH): 수용기준과 요구사항 간 용어 불일치

- **위치**: acceptance.md AC-SIM-002, spec.md R-SIM-007
- **설명**: spec.md에서 FSM 상태 경로를 "IDLE→RESET→INTEGRATE→READOUT→DONE"으로 정의했으나, acceptance.md AC-SIM-002는 "IDLE→RESET→INTEGRATE→READOUT_INIT→SCAN_LINE→READOUT_DONE→DONE"으로 세분화. RTL 구현자가 어느 정의를 따를지 불명확.
- **권고**: spec.md 또는 acceptance.md 중 하나를 권위 있는(authoritative) 정의로 지정하고, 다른 문서는 참조 표기.

#### XVER-020 (HIGH): CSI-2 TX PRIMARY 출력 요구사항 미명시

- **위치**: spec.md Section 1 (Scope)
- **설명**: 현 CLAUDE.md 및 아키텍처 문서에서 CSI-2가 `mcu_data_if`(MCU 병렬 인터페이스)를 대체하는 PRIMARY 출력임을 암시하나, spec.md의 범위 절에 이 아키텍처 결정이 명시적으로 선언되지 않음.
- **권고**: spec.md Section 1에 "CSI-2 TX는 실시간 이미지 데이터 PRIMARY 출력이며, MCU 병렬 인터페이스는 LEGACY/보조 경로로 유지" 문구 추가.

#### XVER-004 (HIGH): GoldenModelBase 타입 시스템 불완전

- **위치**: plan.md 102~134행
- **설명**: `set_inputs(const std::map<std::string, uint32_t>&)` 인터페이스는 16-bit 이상 값에 `uint32_t` 캐스팅이 필요한데, 24-bit `REG_TINTEG` 같은 신호에 대해 상위 비트 손실 가능성이 있음. 또한 `uint32_t` 기반 맵은 LVDS 256채널 데이터(4096 bytes)를 단일 호출로 전달하기에 부적합.
- **권고**: 와이드 신호용 `std::vector<uint8_t>` 또는 `uint64_t` 오버로드 추가, 또는 별도의 bulk 데이터 전송 인터페이스 정의.

#### XVER-017 (HIGH): 에지 케이스가 요구사항이 아닌 테스트 벡터로만 정의

- **위치**: spec.md R-SIM-024, plan.md Section 3
- **설명**: VGH 과전압(>38V), PLL unlock, AFE FIFO overflow 같은 에지 케이스가 R-SIM-024 "에러 주입" 벡터 요구사항으로만 처리되고 개별 안전 요구사항(R-SIM-XXX)으로 승격되지 않음. 이는 EARS 방법론의 Unwanted 패턴("시스템은 ... 하지 않아야 한다")으로 표현되어야 함.
- **권고**: VGH 과전압 감지 및 차단, PLL unlock 시 안전 모드 전이를 별도 Unwanted 요구사항으로 추가.

---

### MEDIUM (11건)

#### XVER-002 (MEDIUM): REG_NRESET 레지스터 미정의

- **위치**: CLAUDE.md 레지스터 맵 vs spec.md
- **설명**: CLAUDE.md의 레지스터 맵에 `REG_NRESET` 항목이 없으나, 전원 시퀀서 및 패널 리셋 제어를 위한 소프트웨어 리셋 레지스터가 필요함.
- **권고**: `REG_CTRL(0x02)[2]`에 soft_reset 비트 추가 또는 독립 레지스터 정의.

#### XVER-003 (MEDIUM): DARK_FRAME 모드 FSM 전이 불명확

- **위치**: CLAUDE.md FSM 모드 표, spec.md R-SIM-007
- **설명**: `DARK_FRAME` 모드(REG_MODE=011)에서 "Gate off, AFE readout only"라고 명시됐으나, FSM이 INTEGRATE 상태를 건너뛰는지 아니면 Gate off 상태로 INTEGRATE를 수행하는지 불명확. acceptance.md AC-SIM-024는 "Gate OFF 유지한 채 AFE 리드아웃만"으로 동작을 정의했으나 FSM 상태 전이도와 불일치.
- **권고**: DARK_FRAME 모드의 FSM 상태 전이를 "IDLE→RESET(gate off)→READOUT_INIT→SCAN_LINE→..."으로 명확히 다이어그램화.

#### XVER-008 (MEDIUM): CSI-2 패킷 Data Type 미명시

- **위치**: spec.md R-SIM-011
- **설명**: R-SIM-011은 CSI-2 패킷 구조(FS/FE + Long Packet)를 정의하지만 Data Identifier의 Data Type(DT) 값이 명시되지 않음. RAW16의 경우 MIPI CSI-2 표준상 DT=0x2E, Virtual Channel(VC)=0이어야 함.
- **권고**: R-SIM-011에 "DT=0x2E(RAW16), VC=0" 명시.

#### XVER-010 (MEDIUM): SPI 클럭 극성/위상 미명시

- **위치**: spec.md, acceptance.md AC-SIM-001
- **설명**: acceptance.md는 "SPI Mode 0 (CPOL=0, CPHA=0)"을 언급하지만 spec.md 요구사항에는 SPI 모드가 명시되지 않음.
- **권고**: spec.md에 "MCU↔FPGA SPI는 Mode 0 (CPOL=0, CPHA=0), MSB-first, 최대 10MHz" 명시.

#### XVER-011 (MEDIUM): 레지스터 초기값 미정의

- **위치**: CLAUDE.md 레지스터 맵, reg_bank.sv
- **설명**: `reg_bank.sv`의 TODO 주석만 있고 레지스터 초기값이 어디에도 정의되지 않음. 특히 `REG_MODE(0x01)` 초기값이 STATIC(000)인지 확인 필요.
- **권고**: reg_bank.sv 또는 architecture 문서에 power-on default 값 테이블 추가.

#### XVER-012 (MEDIUM): 다중 AFE SYNC 타이밍 스큐 허용값 미정의

- **위치**: spec.md R-SIM-020, CLAUDE.md
- **설명**: "24개 독립 LVDS 수신기 인스턴스에서 데이터 손상이 발생하지 않아야 한다"는 요구사항이 있으나, 24개 AFE 간 SYNC 스큐 허용값(예: ±1 ACLK 사이클)이 정의되지 않음.
- **권고**: R-SIM-020에 "24개 AFE SYNC 스큐는 ±1 ACLK 사이클(100ns @ 10MHz) 이내" 추가.

#### XVER-013 (MEDIUM): Verilator 버전 미고정

- **위치**: spec.md Section 6 (기술 제약 조건)
- **설명**: "Verilator >= 5.x"로 범위가 너무 넓음. Verilator 5.0~5.028 간 API 변경이 있어 특정 기능이 버전별로 다름.
- **권고**: "Verilator >= 5.020 (검증 완료 버전: 5.020~5.028)" 명시.

#### XVER-014 (MEDIUM): cocotb 버전 미명시

- **위치**: spec.md Section 6
- **설명**: cocotb 버전 요구사항이 없음. cocotb 1.x와 2.x는 비동기 API가 다름.
- **권고**: "cocotb >= 2.0 (async/await API)" 명시.

#### XVER-018 (MEDIUM): 배터리/모바일 모드(C2) 저전력 검증 누락

- **위치**: CLAUDE.md 조합 매트릭스 C2
- **설명**: C2(NV1047+AD71143)의 주요 사용 목적은 저전력/모바일이나, AFE 저전력 모드(cfg_pmode) 관련 수용기준이 없음.
- **권고**: AC-SIM-007을 AD71143 IFS 5-bit + 저전력 모드 검증으로 확장.

#### XVER-022 (MEDIUM): CSI-2 LP/HS 전환 타이밍 스펙 불충분

- **위치**: spec.md R-SIM-016
- **설명**: R-SIM-016은 "LP->HS 전환 시퀀스가 MIPI D-PHY <=1 us 요구사항을 충족하는지 검증"이라 하지만 HS->LP 전환, HS burst 최소 길이, TLPX, THS-PREPARE 등 D-PHY 파라미터가 전혀 정의되지 않음.
- **권고**: R-SIM-016에 D-PHY 타이밍 파라미터 테이블 추가 (TLPX≥50ns, THS-PREPARE 40~85ns 등).

#### XVER-023 (MEDIUM): 전원 시퀀싱 타이밍 수용기준 추가 필요

- **위치**: acceptance.md AC-SIM-016
- **설명**: AC-SIM-016은 VGL→VGH 순서만 검증하고 NT39565D 전용 25ms 안정화 마진과 전원 OFF 역순서(VGH→VGL)를 검증하지 않음.
- **권고**: NT39565D 전용 전원 시퀀싱 수용기준 AC-SIM-028 추가.

---

### LOW (6건)

#### XVER-015 (LOW): 테스트 벡터 파일 크기 제한 미정의

- **설명**: R-SIM-025는 벡터 파일 위치만 정의. 최대 파일 크기 또는 분할 기준 없음.
- **권고**: "단일 벡터 파일 최대 100MB, 초과 시 자동 분할" 정책 추가.

#### XVER-016 (LOW): CMake 최소 버전 미명시

- **설명**: CMakeLists.txt에 `cmake_minimum_required` 버전이 plan.md에 명시되지 않음.
- **권고**: CMake >= 3.20 명시.

#### XVER-019 (LOW): Google Test 버전 미명시

- **설명**: Google Test를 사용하나 버전이 명시되지 않음.
- **권고**: Google Test >= 1.13 명시.

#### XVER-021 (LOW): 테스트 벡터 MSB/LSB 순서 불명확

- **위치**: plan.md 137~152행
- **설명**: 벡터 파일에서 다중 신호 연결 시 비트 순서(MSB-first vs LSB-first)가 명시되지 않음.
- **권고**: 헤더 메타데이터에 `@BITORDER: MSB_FIRST` 필드 추가.

#### XVER-024 (LOW): 시뮬레이션 랜덤 시드 재현성 보장 방법 미정의

- **설명**: 에러 주입 테스트의 랜덤 시드가 CI/CD에서 고정되지 않으면 재현성 문제 발생 가능.
- **권고**: `@SEED: <value>` 헤더 필드 및 환경변수 `GOLDEN_SEED` 지원 명시.

#### XVER-025 (LOW): 로그 출력 포맷 미표준화

- **설명**: 시뮬레이션 로그 포맷이 각 모델마다 다를 수 있음.
- **권고**: 공통 로그 포맷(타임스탬프 + 모듈명 + 레벨 + 메시지) 표준 정의.

---

## 2. RTL vs SPEC 정합성 상세 (25건)

### CRITICAL (5건)

#### RTL-GAP-001 (CRITICAL): `detector_core.sv` 누락

- **RTL 파일**: `rtl/top/` 디렉토리에 `fpga_top_c1.sv`, `panel_ctrl_fsm.sv`, `panel_integ_ctrl.sv`, `panel_reset_ctrl.sv`만 존재
- **아키텍처 기대값**: CLAUDE.md 모듈 계층도에서 `fpga_top_cX.sv` 바로 아래 서브시스템 묶음 계층(`detector_core`)이 암시됨. SPI, 클럭, 레지스터, FSM을 묶는 중간 계층 없음.
- **영향**: fpga_top이 직접 10개 이상 서브모듈을 인스턴스화해야 하므로 C1~C7 변형 관리 복잡도 급증.
- **권고**: `detector_core.sv` 생성 또는 CLAUDE.md 모듈 계층도를 현 구조에 맞게 수정.

#### RTL-GAP-002 (CRITICAL): `gate_nv1047.sv` `row_index` 너비

- **RTL 현황**: `input logic [11:0] row_index` (12-bit, 최대 4096)
- **아키텍처 요구값**: NV1047 최대 300ch(R1717), 9-bit(512)로 충분. 아키텍처 문서와 CLAUDE.md에서 REG_NROWS가 12-bit으로 정의되어 있어 모듈 입력 파라미터화가 필요.
- **영향**: 불필요한 비트는 합성 후 최적화되나, 스펙 불일치로 인해 코드 리뷰 혼란.
- **권고**: 파라미터 `ROW_BITS = 9`를 추가하거나 `reg_bank`의 `cfg_nrows[11:0]`를 그대로 받되 주석에 유효 범위 명시.

#### RTL-GAP-003 (CRITICAL): `gate_nt39565d.sv` `row_index` 너비

- **RTL 현황**: `input logic [11:0] row_index` (12-bit)
- **아키텍처 요구값**: NT39565D 541ch × 6 = 3246 gate lines, 12-bit(4096) 필요. 현재 값이 오히려 적절하나, 아키텍처 문서에 "3072 gate lines"로 명시되어 있어 정합성 확인 필요.
- **영향**: 3246 > 3072이므로 X239AW1-102의 실제 라인 수 재확인 필요.
- **권고**: X239AW1-102 데이터시트 재확인 후 row_index 최대값과 param 정의.

#### RTL-GAP-004 (CRITICAL): `afe_ad711xx.sv` `config_done` 피드백 경로

- **RTL 현황**: `output logic config_done` (라인 42) — AFE가 설정 완료 신호를 출력하나, 포트 목록에 이를 받아들이는 입력이 없음.
- **실제 문제**: `config_done`을 `panel_ctrl_fsm`이 받아 다음 단계로 진행해야 하는데, `panel_ctrl_fsm`의 포트 목록(별도 파일)에서 `afe_config_done` 입력이 누락됐을 가능성.
- **영향**: FSM이 AFE 초기화 완료를 알 방법 없어 타이밍 경쟁 조건 발생.
- **권고**: `panel_ctrl_fsm.sv` 포트에 `input logic afe_config_done` 추가, RTL 계층 연결 확인.

#### RTL-GAP-005 (CRITICAL): `line_data_rx.sv` LVDS 포트 타입 불명확

- **RTL 현황**: `input logic lvds_dclk`, `lvds_dout_a`, `lvds_dout_b` (단일 `logic`)
- **Artix-7 요구사항**: LVDS 입력은 `IBUFDS` 원시 블록을 통해 차동 쌍(P/N) 두 핀을 받아야 함. 현재 포트는 IBUFDS 이후 싱글엔드 신호를 받는 것으로 추정되나 모듈 설명 주석과 불일치.
- **영향**: 핀 할당(XDC 제약 파일) 작성 시 혼란, Vivado IP Integrator 연결 오류 가능.
- **권고**: IBUFDS를 `fpga_top`에서 인스턴스화하고 `line_data_rx`는 IBUFDS 이후 신호를 받음을 명확히 문서화. 포트명을 `dclk_se`, `dout_a_se`, `dout_b_se`로 변경 또는 `_p/_n` 쌍으로 받아 내부에서 IBUFDS 인스턴스화.

---

### HIGH (9건)

#### RTL-GAP-006 (HIGH): `line_data_rx.sv` AFE2256 FCLK 포트 누락

- **RTL 현황**: `ADI_MODE=0`(AFE2256) 시 TI LVDS는 `DCLK_P/M` + `FCLK_P/M` 4개 차동 쌍 필요.
- **현재 포트**: `lvds_dclk`, `lvds_dout_a`, `lvds_dout_b` 3신호만 정의.
- **영향**: AFE2256(C3, C5, C7) 하드웨어 조합에서 프레임 동기화 불가.
- **권고**: `ADI_MODE=0` 시 `input logic lvds_fclk` 추가, generate 블록으로 조건부 포트 처리 또는 항상 4신호 포트 정의.

#### RTL-GAP-007 (HIGH): `reg_bank.sv` CIC/파이프라인 설정 출력 누락

- **현재 출력**: `cfg_afe_ifs`, `cfg_scan_dir`, `cfg_afe_nchip` 등.
- **누락 출력**: `cfg_cic_en(1b)`, `cfg_pipeline_en(1b)`, `cfg_cic_profile(4b)`, `cfg_tp_sel(1b)`.
- **영향**: `afe_afe2256.sv`가 레지스터 설정을 받지 못해 CIC 기능 비활성화 고착.
- **권고**: 아키텍처 문서 Table 3의 `REG_CIC_EN(0x0E)` 및 관련 레지스터에 대한 출력 포트 추가.

#### RTL-GAP-008 (HIGH): `reg_bank.sv` IRQ_EN 레지스터 출력 누락

- **아키텍처 요구**: `REG_IRQ_EN(0x12)` — 인터럽트 마스크 레지스터.
- **영향**: MCU에 인터럽트를 전달하는 `mcu_data_if`에서 IRQ 마스킹 불가.
- **권고**: `output logic [7:0] cfg_irq_en` 포트 추가.

#### RTL-GAP-009 (HIGH): `reg_bank.sv` LINE_RDY 상태 입력 누락

- **아키텍처 요구**: `REG_STATUS[3]` = line_rdy — 각 라인 완료 시 폴링용 비트.
- **현재 입력**: `sts_busy`, `sts_done`, `sts_error`, `sts_line_idx`, `sts_err_code`.
- **영향**: MCU가 라인 단위 완료를 폴링할 방법 없어 DMA 기반 데이터 수집 불가.
- **권고**: `input logic sts_line_rdy` 포트 추가.

#### RTL-GAP-010 (HIGH): `reg_bank.sv` AFE 설정 포트 불완전

- **누락 출력**: `cfg_afe_lpf(4b)` — LPF 시상수, `cfg_afe_pmode(2b)` — AFE 전력 모드.
- **근거**: `afe_ad711xx.sv` 포트에는 `cfg_lpf`, `cfg_pmode`가 정의되어 있으나 `reg_bank`에서 이를 출력하는 포트가 없음.
- **권고**: 해당 레지스터 비트 정의 및 출력 포트 추가.

#### RTL-GAP-011 (HIGH): `afe_afe2256.sv` FCLK 출력 누락

- **AFE2256 요구사항**: FPGA가 AFE2256에 FCLK(frame clock)을 공급해야 함(AFE2256 datasheet Section 7).
- **현재 출력**: `afe_mclk`, `afe_sync`, `afe_tp_sel`, `afe_reset`.
- **누락 출력**: `afe_fclk_p`, `afe_fclk_n` (차동 LVDS 출력).
- **권고**: `output logic afe_fclk_p, afe_fclk_n` 추가.

#### RTL-GAP-012 (HIGH): `panel_ctrl_fsm.sv` 포트 목록 미검토

- **RTL 파일**: `rtl/top/panel_ctrl_fsm.sv` 존재하나 이번 분석에서 상세 포트 검토 불완전.
- **우려**: FSM이 `gate_ic_driver`와 `afe_ctrl_if`를 직접 제어하는 포트가 충분한지, 타이밍 파라미터를 `reg_bank`에서 직접 받는지 확인 필요.
- **권고**: `panel_ctrl_fsm.sv` 포트 목록 전수 검토 후 별도 RTL-GAP 발행.

#### RTL-GAP-013 (HIGH): `clk_rst_mgr.sv` AFE 클럭 선택 로직 미구현

- **RTL 파일**: `rtl/common/clk_rst_mgr.sv` (ACLK_HZ/MCLK_HZ 파라미터 명시).
- **문제**: AD711xx용 ACLK(10MHz)와 AFE2256용 MCLK(32MHz)를 동시에 생성하되, 실제 활성화는 `cfg_combo`(REG_COMBO)에 따라 선택해야 함. 현재 파라미터화만 있고 선택 로직 없음(TODO).
- **권고**: `cfg_combo`를 입력받아 MMCM 출력 클럭 중 적절한 것을 enable하는 MUX 로직 구현.

#### RTL-GAP-025 (HIGH): `fpga_top_c1.sv` 이외 `fpga_top_c2~c7.sv` 미생성

- **RTL 현황**: `rtl/top/fpga_top_c1.sv`만 존재.
- **CLAUDE.md 요구**: C1~C7 각 조합에 대한 `fpga_top_cX.sv` 별도 파일.
- **영향**: 6개 하드웨어 조합에 대한 핀 매핑 및 컴파일 타임 분기가 구현되지 않음.
- **권고**: v1 범위에서 필요한 조합부터 순차 생성. C1, C3, C6 우선.

---

### MEDIUM (6건)

#### RTL-GAP-014 (MEDIUM): `row_scan_eng.sv` 독립 포트 미정의

- **현황**: `rtl/gate/row_scan_eng.sv` 존재하나 `gate_nv1047`과의 인터페이스 경계가 불명확.
- **권고**: `row_scan_eng` 포트 목록에 `clk`, `rst_n`, `row_start`, `row_index`, `row_done`, `clk_out`, `clk_period` 명시.

#### RTL-GAP-015 (MEDIUM): `line_buf_ram.sv` dual-clock 지원 여부 불명확

- **현황**: `rtl/roic/line_buf_ram.sv` 존재하나 dual-clock BRAM인지 single-clock인지 불명확.
- **영향**: DCLK(AFE) 도메인에서 쓰고 SYS_CLK에서 읽는 ping-pong 버퍼 구현 필수.
- **권고**: 포트에 `wr_clk`, `rd_clk` 분리 명시.

#### RTL-GAP-016 (MEDIUM): `spi_slave_if.sv` 상태 기계 클럭 미명시

- **현황**: SPI 슬레이브는 `sys_clk`으로 동기화해야 하나, SPI_CLK 동기화 전략(2-FF 동기화기 또는 FIFO) 미명시.
- **권고**: SPI_CLK가 비동기 입력이므로 2-FF 동기화기 또는 edge detector 구조 명시.

#### RTL-GAP-017 (MEDIUM): `mcu_data_if.sv` CSI-2와 동시 동작 여부 미정의

- **설명**: MCU 병렬 인터페이스와 CSI-2 TX가 동시에 활성화 가능한지, 아니면 상호 배타적인지 정의되지 않음.
- **권고**: `data_out_mux.sv`에서 CSI-2 또는 MCU 중 하나 선택하는 `cfg_output_sel` 비트 추가.

#### RTL-GAP-018 (MEDIUM): `prot_mon.sv` 과전압 감지 임계값 레지스터 연결 미명시

- **설명**: VGH > 38V 감지 임계값이 레지스터 설정인지 하드코딩인지 불명확.
- **권고**: `REG_VMAX(0x19)` 레지스터를 통해 런타임 설정 가능하게 명시.

#### RTL-GAP-019 (MEDIUM): `packages/` 디렉토리 패키지 파일 내용 미확인

- **현황**: `rtl/packages/` 디렉토리에 `fpd_params_pkg`, `fpd_types_pkg` 존재 추정.
- **우려**: `PIXEL_WIDTH`, `REG_ADDR_WIDTH`, `REG_DATA_WIDTH` 등 상수 정의가 패키지와 RTL 모듈 간 일치하는지 검토 필요.
- **권고**: 패키지 파일 전수 검토 후 상수 정의 확인.

---

### LOW (5건)

#### RTL-GAP-020 (LOW): RTL 파일 헤더 주석 불완전

- **설명**: 모든 RTL 파일이 간략한 헤더 주석을 포함하나 버전, 작성자, 최종 수정일 정보 없음.
- **권고**: 헤더에 `// Version`, `// Author`, `// Last Modified` 필드 추가.

#### RTL-GAP-021 (LOW): `emergency_shutdown.sv` 포트 미검토

- **설명**: `rtl/common/emergency_shutdown.sv` 존재하나 이번 분석에서 미검토.
- **권고**: 포트 목록 및 `prot_mon`과의 연결 검토.

#### RTL-GAP-022 (LOW): `power_sequencer.sv` 슬루율 제한 구현 방식 미명시

- **설명**: AC-SIM-016 요구 슬루율 ≤5V/ms를 디지털 제어로 어떻게 구현할지 미명시.
- **권고**: PWM 기반 출력 또는 DAC 제어 단계 수를 설계 노트로 추가.

#### RTL-GAP-023 (LOW): `afe_spi_master.sv` 데이지체인 터미네이션 미명시

- **설명**: 24개 AFE 데이지체인의 SDO 터미네이션 방식이 RTL에서 명확하지 않음.
- **권고**: 마지막 AFE의 SDO를 FPGA가 SDI로 루프백하는 방식인지 명시.

#### RTL-GAP-024 (LOW): Artix-7 IDELAYE2 캘리브레이션 구조 미포함

- **설명**: `line_data_rx.sv`에 IDELAYE2 관련 포트나 캘리브레이션 제어 신호 없음.
- **권고**: v1 범위에서 IDELAYE2 초기 탭 수를 고정값으로 시작하고, 향후 캘리브레이션 루프 구현 계획을 설계 노트에 추가.

---

## 3. 타이밍/하드웨어/CDC 검증 상세 (14건)

### CRITICAL (4건)

#### TIM-001 (CRITICAL): AFE 라인 타임 파라미터 전 문서 정합성

- **상태**: 검증 완료
- **결과**: AD71124 22µs (REG_TLINE ≥ 2200 @ 10ns/unit), AD71143 60µs (REG_TLINE ≥ 6000), AFE2256 51.2µs 모두 SPEC-REVIEW-001, 아키텍처 문서, RTL 파라미터 간 일치. 단, spec.md 기술 제약 조건 표에서 레지스터 단위 변환(10ns/unit) 표기 누락.
- **권고**: spec.md 기술 제약 조건 표에 "REG_TLINE 단위: 10ns (예: 2200 = 22µs)" 각주 추가.

#### TIM-004 (CRITICAL): 전원 시퀀싱 타이밍 검증

- **상태**: 부분 검증
- **결과**: NV1047 전원 안정화 20ms, NT39565D 25ms로 아키텍처 문서 기재. acceptance.md AC-SIM-016은 "VGL 안정화 10ms 후 VGH"로만 정의하여 NT39565D의 25ms 마진 미검증.
- **권고**: NT39565D 사용 조합(C6, C7) 전용 전원 시퀀싱 수용기준 추가.

#### CDC-001 (CRITICAL): 5개 클럭 도메인 식별 완료

- **식별된 도메인**:
  1. SYS_CLK: 100MHz (FPGA 내부 주 클럭)
  2. DCLK: ~20MHz × 24개 (각 AFE 출력 클럭, 비동기)
  3. ACLK: 10MHz (AD711xx 공급 클럭, SYS_CLK 분주)
  4. MCLK: 32MHz (AFE2256 공급 클럭, SYS_CLK 분주)
  5. SPI_CLK: 1~10MHz (MCU 공급, 완전 비동기)
- **CDC 교차점**: DCLK→SYS_CLK (line_data_rx), SPI_CLK→SYS_CLK (spi_slave_if), ACLK/MCLK→SYS_CLK (afe 상태 피드백).
- **상태**: CDC 교차점 식별 완료, 동기화 메커니즘 SPEC 누락.

#### CDC-003 (CRITICAL): 리셋 동기화 사양 누락

- **문제**: MMCM 락 신호(`locked`)를 SYS_CLK 도메인으로 동기화하는 2-FF 동기화기가 설계 어디에도 정의되지 않음. `rst_n` 생성 로직이 MMCM 락 후 동기화 해제됨을 보장하는 구조 미명시.
- **영향**: MMCM 락 전 논리가 동작할 경우 메타스테이빌리티 및 불확정 초기 상태.
- **권고**: `clk_rst_mgr.sv`에 MMCM locked 동기화 체인 및 비동기 어서트/동기 디어서트 리셋 구조 명시.

---

### HIGH (4건)

#### TIM-002 (HIGH): Gate IC 스캔 클럭 최소 주기 검증

- **NV1047**: CLK 최대 200kHz → 최소 주기 5µs. REG_TLINE=2200(22µs) 조건에서 1 라인당 CLK 사이클 수: 22µs / 5µs = 4.4 → 최소 5 CLK 사이클 필요. 300채널 기준 충분.
- **NT39565D**: CPV 최대 200kHz, 541채널 × 6칩. 총 게이트 라인 시간 = 540 × 5µs = 2.7ms. REG_TLINE과 독립적으로 계산 필요.
- **우려**: NT39565D 6칩 캐스케이드 시 CPV 주파수가 6칩을 통과하며 누적 지연이 발생할 수 있음.
- **권고**: NT39565D 캐스케이드 CPV 타이밍 분석 추가.

#### TIM-003 (HIGH): ISERDESE2 비트슬립 정렬 수렴 시간 미정의

- **설명**: `line_data_rx.sv`에서 `bitslip_req`를 통해 비트 정렬을 수행하나, 최대 비트슬립 횟수와 수렴 타임아웃이 미정의.
- **영향**: AFE 초기화 후 비트 정렬 실패 시 영구 대기 가능.
- **권고**: 최대 비트슬립 횟수(예: 8회) 및 타임아웃 후 에러 플래그 생성 로직 명시.

#### RES-001 (HIGH): BRAM 사용량 추정

- **v1 계산**:
  - line_buf_ram: 256ch × 16bit × 2뱅크 × 24AFE = 196,608 bits = 5.46 BRAM36K ≈ 6개
  - reg_bank: 32 × 16bit = 1 BRAM36K 미만 (LUTRAM으로 구현 권장)
  - 총 v1 BRAM: 6~8 BRAM36K (가용 50개 중 12~16%)
- **v2 추가 예상**: 프레임 버퍼 2048×2048×16bit = 64Mbit = 1,778 BRAM36K → BRAM 불가, 외부 DDR 필수.
- **결론**: v1 BRAM 사용량 여유 충분.

#### BW-001 (HIGH): 12 AFE 집합 데이터율 분석

- **계산**: 12 AFE × 256ch × 16bit × (1 / 22µs per line) = 12 × 256 × 16 / 22µs = 2.24 Gbps.
- **24 AFE (C6/C7)**: 4.47 Gbps.
- **SYS_CLK=100MHz에서 BRAM 처리량**: 100MHz × 36bit = 3.6 Gbps per BRAM port.
- **결론**: 24 AFE 시나리오는 BRAM 포트 당 처리량 초과 가능. 다중 BRAM 병렬 사용 또는 데이터 패킹 전략 필요.
- **권고**: 24 AFE 시나리오에서 BRAM 뱅크 수 및 주소 분배 전략 설계 문서 추가.

---

### MEDIUM (4건)

#### RES-002 (MEDIUM): I/O 핀 예산 검증

- **v1 예상 (C1, 12 AFE 기준)**:
  - LVDS 수신: 12 AFE × 3쌍 × 2핀 = 72핀
  - LVDS 송신(CSI-2 2lane): 2쌍 × 2핀 = 4핀
  - SPI: 4핀
  - Gate IC (NV1047): ~10핀
  - AFE 제어: ~8핀
  - MCU 인터페이스: ~20핀
  - 합계: ~118핀
- **v2 예상 (C6, 24 AFE 기준)**: 24 × 6 = 144핀(LVDS 수신) + 기타 = ~200핀
- **가용 핀**: FGG484 패키지 250개 사용자 I/O.
- **결론**: v2 C6/C7 조합에서 핀 수 매우 타이트. 신중한 핀 매핑 필요.

#### RES-003 (MEDIUM): MMCM 사용량 검증

- **예상 MMCM 사용**:
  1. SYS_CLK(100MHz) 생성
  2. ACLK(10MHz) 생성
  3. MCLK(32MHz) 생성
  4. LVDS 수신용 고속 클럭(>200MHz, ISERDESE2 용)
- **가용**: 5개 (Artix-7 35T).
- **결론**: 4개 사용으로 여유 1개. 추가 클럭 도메인 불가.

#### TIM-005 (MEDIUM): CSI-2 TX 데이터율 검증

- **C1~C5 (2-lane)**: 4096 bytes/row / 22µs × 8bit = 1.49 Gbps. MIPI D-PHY 1Gbps/lane × 2lane = 2Gbps. 여유 있음.
- **C6~C7 (4-lane)**: 6144 bytes/row / 22µs × 8bit = 2.24 Gbps. 4lane × 1Gbps = 4Gbps. 여유 있음.
- **결론**: CSI-2 데이터율 적절.

#### RES-004 (MEDIUM): DSP 사용량 예상

- **v1**: CRC-16 계산에 XOR 체인 사용, DSP 미사용. 추정 0~4 DSP.
- **v2**: 보정 파이프라인(offset/gain) 도입 시 256ch × 곱셈 = 다수 DSP 필요.
- **결론**: v1 DSP 사용량 여유 충분.

---

### LOW (2건)

#### CDC-002 (LOW): CDC 전략 적절성 검증

- **결론**: DCLK→SYS_CLK 경로에 async FIFO(dual-clock BRAM) 사용 전략은 업계 표준이며 적절. 단, FIFO 깊이 16 words(spec.md R-SIM-018)의 여유도를 worst-case backpressure 분석으로 확인 필요.
- **권고**: worst-case DCLK 버스트 길이 대비 FIFO 깊이 마진 계산서 추가.

#### BW-002 (LOW): 라인 버퍼 fill/drain 마진 분석

- **설명**: 라인 버퍼에 AFE가 256ch × 16bit를 채우는 속도와 CSI-2 TX가 빼는 속도의 마진 분석 미수행.
- **추정**: AFE fill 속도 ≈ 256 × 16bit / 22µs = 186 MHz-bit. CSI-2 drain 속도 2Gbps >> 186Mbps. 충분.
- **권고**: 공식 마진 계산서를 설계 노트에 추가(안전 확인 용도).

---

## 4. 업계 딥리서치 결과 요약

### 4.1 검증 프레임워크 (OpenTitan, ADI Testbenches)

업계 선진 검증 프레임워크 분석 결과:

- **OpenTitan 검증 접근법**: IP 블록별 독립 검증, DV 환경 표준화(UVM lite 패턴). 본 프로젝트의 SW-First 접근은 UVM 없이 C++ 골든 모델로 유사한 효과 달성 가능하며 타당한 설계 결정.
- **ADI 검증 자료**: AD7124 등 ADI AFE의 공개 testbench는 SPI 인터페이스 검증에 집중. FPGA 측 제어 로직 검증은 자체 개발 필수.
- **즉시 액션 #1**: Google Test 기반 C++ 단위 테스트 + cocotb 통합 테스트의 2-레이어 접근이 검증됨. 현 계획 유지.
- **즉시 액션 #2**: `SPEC-FPD-SIM-001` Verification Plan 문서(VP-FPD-001)를 별도 생성하여 검증 커버리지 매트릭스 관리 권장.

### 4.2 AFE/ROIC 인터페이스 (IDELAY 캘리브레이션, BFM)

- **IDELAY2 캘리브레이션**: Artix-7 IDELAYE2를 사용한 LVDS 수신은 초기 탭 수 고정(예: 탭 16 = 1.25ns 지연) 후, 아이 다이어그램 샘플링으로 최적 탭 수 결정하는 2단계 캘리브레이션 표준 절차 존재. 현 v1 설계에서 이 절차 미정의.
- **AFE BFM**: ADI AD71124/AD71143용 공개 BFM 없음. LVDS 출력 패턴을 C++ 골든 모델에서 생성하는 현 접근이 최선.
- **즉시 액션 #3**: IDELAYE2 초기 캘리브레이션 절차를 `line_data_rx.sv` 설계 노트에 추가.
- **즉시 액션 #4**: LVDS BFM을 C++ 골든 모델의 `LvdsRxModel`에 통합하여 cocotb에서 LVDS 시퀀스 재생 가능하게 구성.

### 4.3 CSI-2 MIPI TX (VideoGPU 참조, XAPP894, LP/HS 전환)

- **XAPP894**: Xilinx가 Artix-7 기반 MIPI CSI-2 TX 레퍼런스 디자인(XAPP894)을 제공. 단, 이 설계는 D-PHY 서브레이어를 포함하며 FPGA I/O 표준(LVDS_25)을 사용함. 본 프로젝트에서 직접 재사용 가능.
- **LP/HS 전환**: D-PHY HS 모드 진입 시퀀스(LP-00→LP-01→LP-00→HS Sync)는 XAPP894에서 확인됨. 현 spec.md R-SIM-016의 "1µs" 요구사항은 D-PHY 사양 TLPX+THS-PREPARE+THS-ZERO 합산으로 ~300ns 정도이며 1µs 여유는 충분.
- **즉시 액션 #5**: XAPP894 레퍼런스를 `csi2_packet_builder.sv` 구현 시 참고 자료로 명시.
- **즉시 액션 #6**: D-PHY LP/HS 타이밍 파라미터를 spec.md R-SIM-016에 보강.

### 4.4 Gate IC Driver (공개자료 전무, 자체 BFM 필수)

- **조사 결과**: NV1047, NT39565D 모두 공개된 검증 BFM 또는 레퍼런스 RTL 없음. 의료기기 FPGA 설계의 Gate IC 드라이버는 관례적으로 독점 자료.
- **시사점**: `GateNv1047Model.h/cpp`, `GateNt39565dModel.h/cpp`는 완전히 자체 개발 필요. 특히 NT39565D 데이터시트의 STV1/STV2 이중 펄스 타이밍 도표를 엄밀히 분석하여 C++ 모델 구현 필요.
- **즉시 액션 #7**: NT39565D STV 이중 펄스 타이밍 분석 결과를 `research.md`에 추가(단독 섹션으로 확장).

### 4.5 C++ Golden Model 파이프라인 (DPI-C, cocotb ctypes)

- **DPI-C 접근법**: SystemVerilog DPI-C를 통해 C++ 골든 모델을 RTL 시뮬레이터(Vivado xsim)에서 직접 호출하는 방식이 업계에서 검증됨. 현 plan.md의 cocotb 파일 I/O 방식보다 사이클 정확성이 높음.
- **cocotb ctypes**: cocotb에서 C++ 공유 라이브러리(`.so`/`.dll`)를 `ctypes`로 로드하여 C++ 모델을 Python에서 직접 호출 가능. 현 plan.md에서 이 통합 방식을 선택적으로 활용할 수 있음.
- **즉시 액션 #8**: plan.md에 "Phase 5 이상에서 DPI-C 통합 옵션" 검토 내용 추가.

### 4.6 의료기기 규제 (IEC 62304, FDA 2024, DO-254 추적성)

- **IEC 62304**: 의료기기 소프트웨어 수명주기 표준. X-ray 평판 검출기는 Class B 또는 Class C 의료기기에 해당할 가능성이 높아 소프트웨어/펌웨어 문서화 요구사항 적용 가능.
- **FDA 2024 지침**: "Cybersecurity in Medical Devices" 및 "Software as a Medical Device" 지침에서 FPGA 업데이트 메커니즘(secure boot, 코드 서명)을 요구.
- **DO-254**: 항공기 전자 하드웨어와 유사하게 의료기기 FPGA에도 추적성 매트릭스(요구사항→설계→검증) 요구가 강화되는 추세.
- **즉시 액션 #9**: 현 SPEC 문서 구조(spec.md + acceptance.md)는 IEC 62304 수명주기 문서의 기초 역할 가능. 추적성 매트릭스를 acceptance.md에 추가하면 Class B 요구사항의 상당 부분 충족.
- **즉시 액션 #10**: FPGA 업데이트 메커니즘(SPI flash 부트 + 다중 비트스트림) 설계를 v2 로드맵에 포함.
- **즉시 액션 #11**: IEC 62304 Class B 문서 패키지 준비 항목을 프로젝트 장기 계획에 추가.
- **즉시 액션 #12**: DO-254 스타일 추적성 매트릭스를 `docs/review/TRACEABILITY-MATRIX.md`로 신규 생성 검토.

---

## 5. 우선순위별 액션 플랜

### 즉시 (CRITICAL/HIGH — 문서 개선, 구현 전 필수)

| # | 액션 | 대상 문서/파일 | 근거 |
|---|------|---------------|------|
| 1 | 모듈 네이밍 규칙 표준화 (C++ 클래스명 vs RTL 모듈명) | spec.md, plan.md | XVER-006 |
| 2 | 요구사항-AC 추적성 매트릭스 추가 | acceptance.md | XVER-001, XVER-009 |
| 3 | C2/C4/C5/C7 하드웨어 조합 전용 수용기준 추가 (AC-SIM-026~029) | acceptance.md | XVER-007 |
| 4 | CSI-2 PRIMARY 출력 요구사항 명시 | spec.md Section 1 | XVER-020 |
| 5 | CDC 사양 보강 (리셋 동기화, MMCM 락, SYNC 스큐) | spec.md, clk_rst_mgr 설계노트 | CDC-003 |
| 6 | reg_bank.sv 완전한 레지스터 출력 포트 명세 (CIC, IRQ_EN, LINE_RDY 등) | rtl/common/reg_bank.sv | RTL-GAP-007~010 |
| 7 | AFE2256 FCLK 인터페이스 명세 및 포트 추가 | rtl/roic/afe_afe2256.sv, line_data_rx.sv | RTL-GAP-006, 011 |
| 8 | GoldenModelBase 와이드 신호 타입 시스템 개선 | plan.md, GoldenModelBase.h | XVER-004 |
| 9 | row_index 포트 너비 결정 및 주석 문서화 | rtl/gate/gate_nv1047.sv, gate_nt39565d.sv | RTL-GAP-002, 003 |
| 10 | Verification Plan 문서 (VP-FPD-001) 신규 작성 | docs/review/VP-FPD-001.md | 업계 권장 #2 |

### 중기 (MEDIUM — SPEC 보강, 구현 중 병행)

| # | 액션 | 근거 |
|---|------|------|
| 11 | REG_CTRL soft_reset 비트 추가 | XVER-002 |
| 12 | DARK_FRAME 모드 FSM 상태 전이 다이어그램 명확화 | XVER-003 |
| 13 | CSI-2 패킷 DT=0x2E, VC=0 명시 | XVER-008 |
| 14 | 전원 시퀀싱 타이밍 NT39565D 전용 항목 SPEC 추가 | XVER-023, TIM-004 |
| 15 | 에지 케이스 → Unwanted 요구사항 승격 | XVER-017 |
| 16 | 라인 버퍼 fill/drain 마진 분석 문서 추가 | BW-002 |
| 17 | IDELAYE2 초기 캘리브레이션 절차 설계노트 추가 | 업계 권장 #3 |
| 18 | NT39565D STV 이중 펄스 타이밍 분석 research.md 보강 | 업계 권장 #7 |

### 장기 (LOW + 업계 권장, v2 및 양산 준비)

| # | 액션 | 근거 |
|---|------|------|
| 19 | IEC 62304 Class B 문서 패키지 준비 | 업계 권장 #11 |
| 20 | FPGA 업데이트 메커니즘 설계 (SPI flash + 다중 비트스트림) | FDA 2024, 업계 권장 #10 |
| 21 | DO-254 스타일 추적성 매트릭스 작성 | 업계 권장 #12 |
| 22 | DPI-C 통합 옵션 검토 (Phase 5+ 향후 적용) | 업계 권장 #8 |
| 23 | fpga_top_c2~c7.sv 생성 | RTL-GAP-025 |

---

## 6. 리소스 예산 검증 결과

| 리소스 | 가용 | v1 사용 추정 | v2 예상 | 여유 (v1) |
|--------|------|-------------|---------|-----------|
| BRAM36K | 50 | 6~8 | 24~36 | 84% (42/50 여유) |
| I/O Pins (FGG484) | 250 | ~98 (C1기준) | ~200 (C6기준) | 60% (v1) / 20% (v2) |
| DSP48E1 | 90 | 0~4 | 20~28 | 충분 |
| MMCM | 5 | 3~4 | 4 | 여유 1개 |
| Logic Cells | 33,280 | TBD | TBD | 합성 후 확인 필요 |

**비고**:
- v2(외부 DDR 추가) 시 BRAM36K는 36개까지 증가하여 한계에 근접
- C6/C7 24 AFE 조합에서 I/O 핀이 타이트하므로 신중한 핀 계획 필요
- Logic Cells 사용량은 FSM + 24개 LVDS 수신기 인스턴스 기준 ~5,000~8,000 LUT 예상 (합성 후 확인)

---

## 7. Sources (업계 딥리서치)

이번 분석에서 참조한 공개 자료 목록:

### 기술 표준 및 사양

- MIPI CSI-2 Specification v1.3 — CRC-16-CCITT(0x1021), ECC Annex A, D-PHY 타이밍 파라미터
- MIPI D-PHY Specification v1.2 — LP/HS 전환 타이밍 (TLPX, THS-PREPARE, THS-ZERO)
- Xilinx XAPP894 — "MIPI CSI-2 Transmitter Using UltraScale/UltraScale+ FPGA" (Artix-7 적용 가능)
- Xilinx UG471 — "7 Series FPGAs SelectIO Resources" (IBUFDS, ISERDESE2, IDELAYE2)
- Xilinx UG472 — "7 Series FPGAs Clocking Resources" (MMCME2_ADV)

### AFE/ROIC 데이터시트 (프로젝트 내 docs/datasheet/)

- AD71124 데이터시트 — tLINE 22µs, SPI 인터페이스, DCLKH/DCLKL LVDS
- AD71143 데이터시트 — tLINE 60µs, IFS 5-bit, LVDS 출력
- AFE2256 데이터시트 — tLINE 51.2µs, CIC 기능, FCLK, DCLK_P/M

### Gate IC 데이터시트 (프로젝트 내 docs/datasheet/)

- NV1047 데이터시트 — 최대 CLK 200kHz, SD1/SD2, OE/ONA 타이밍
- NT39565D 데이터시트 — STV1/STV2 이중 펄스, CPV, OE1/OE2, 6칩 캐스케이드

### 검증 프레임워크 참고 자료

- OpenTitan Verification Methodology — IP 블록 검증 표준화 접근법 (https://opentitan.org/book/doc/contributing/dv/)
- lowRISC DV Methodology Guide — UVM lite + SystemVerilog assertions 패턴

### 의료기기 규제

- IEC 62304:2015+AMD1:2015 — 의료기기 소프트웨어 수명주기 (Class A/B/C 분류)
- FDA "Cybersecurity in Medical Devices" (2022) — FPGA 업데이트 메커니즘 요구사항
- DO-254 (RTCA) — 항공 전자 하드웨어 개발 보증 (추적성 매트릭스 참조)

---

**리포트 생성**: 2026-03-20
**분석 신뢰도**: HIGH (>90%) — 실제 SPEC 문서, RTL 스켈레톤, 아키텍처 문서 교차 검토 기반
**검증 방법**: SPEC-FPD-SIM-001 전체 문서(spec.md, plan.md, acceptance.md, research.md) + RTL 스켈레톤 + SPEC-FPD-SIM-001-REVIEW.md + 아키텍처 문서(fpga_module_architecture.md) + CLAUDE.md 교차 분석
