---
id: SPEC-FPD-SIM-001
version: "1.1.0"
status: draft
created: "2026-03-19"
updated: "2026-03-20"
author: drake
priority: P1
issue_number: 0
---

## HISTORY

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-19 | drake | Initial SPEC creation via team-based deep research |
| 1.1.0 | 2026-03-20 | drake | Cross-verification improvements: R-SIM-037~040, naming convention, timing constraints |

---

# SPEC-FPD-SIM-001: SW-First Verification Framework

## 1. Overview

SW-First 검증 프레임워크: C++ 골든 모델 시뮬레이터를 먼저 구현하고, cocotb/Verilator로 RTL을 검증한 후, Vivado xsim으로 최종 통합하는 4단계 검증 방법론.

### Scope

- SPEC-FPD-001 ~ SPEC-FPD-010의 모든 RTL 모듈에 대한 C++ 골든 모델
- Test vector 생성 파이프라인
- cocotb 테스트벤치 프레임워크
- Verilator 사이클 정확 비교 프레임워크
- CMake 기반 크로스 플랫폼 빌드 시스템

### Out of Scope

- Analog 신호 모델링 (LVDS 신호 무결성, MMCM 지터)
- Post-implementation 타이밍 시뮬레이션 (Vivado SDF back-annotation)
- v2 보정 파이프라인 (offset/gain/defect/lag) 골든 모델

---

## 2. Requirements

### Module 1: C++ Golden Model Framework

**R-SIM-001 (Ubiquitous):** 시스템은 SPEC-FPD-001 ~ SPEC-FPD-010에 정의된 각 RTL 모듈에 대응하는 C++ 골든 모델을 제공해야 한다 (SHALL).

**R-SIM-002 (Ubiquitous):** 각 골든 모델은 동일한 입력에 대해 RTL과 비트 동일한(bit-identical) 출력을 생성해야 한다 (SHALL).

**R-SIM-003 (Ubiquitous):** 골든 모델은 GoldenModelBase 추상 클래스를 상속하며, reset(), step(clock_edge), compare(rtl_output) 인터페이스를 구현해야 한다 (SHALL).

**R-SIM-004 (Event-Driven):** AFE AD71124 골든 모델이 tLINE=2200 (22 us)으로 설정되었을 때, 256채널 16비트 출력 데이터를 AD71124 데이터시트 변환 시퀀스와 일치하게 생성해야 한다 (SHALL).

**R-SIM-005 (Event-Driven):** AFE AD71143 골든 모델이 tLINE=6000 (60 us)으로 설정되었을 때, 5비트 IFS 인코딩과 일치하는 256채널 16비트 출력을 생성해야 한다 (SHALL).

**R-SIM-006 (Event-Driven):** AFE2256 골든 모델이 CIC 활성화 상태에서 Pipeline 모드로 동작할 때, row[n-1] ADC 리드아웃과 row[n] 적분을 동시에 수행하며 정확히 1-row 파이프라인 지연을 가져야 한다 (SHALL).

**R-SIM-007 (State-Driven):** Panel FSM 골든 모델이 CONTINUOUS 모드에 있는 동안, 외부 개입 없이 DONE→RESET 전이를 자동 반복해야 한다 (SHALL).

**R-SIM-008 (Ubiquitous):** gate_nv1047 골든 모델은 SD1 시프트 레지스터 동작을 CLK <= 200 kHz로 시뮬레이션하고, OE/ONA 신호를 NV1047 데이터시트 타이밍과 일치하게 생성해야 한다 (SHALL).

**R-SIM-009 (Ubiquitous):** gate_nt39565d 골든 모델은 듀얼-STV 펄스 생성, OE1/OE2 분리 채널 제어, 6-chip 캐스케이드 STVD 전파를 시뮬레이션해야 한다 (SHALL).

**R-SIM-010 (Ubiquitous):** Power Sequencer 골든 모델은 VGL->VGH 전원 시퀀스를 10 ms 안정화 지연과 <=5 V/ms 슬루율 제약 조건으로 모델링해야 한다 (SHALL).

### Module 2: CSI-2 TX Verification

**R-SIM-011 (Ubiquitous):** csi2_tx_model.cpp는 CSI-2 v1.3 준수 패킷 조립을 구현해야 한다: FS/FE 쇼트 패킷 (4 bytes) + RAW16 롱 패킷 — Header [DI(1B, Data Type 0x2E) + WC(2B) + ECC(1B)] + Payload + CRC-16(2B), Virtual Channel = 0 (단일 카메라) (SHALL).

**R-SIM-012 (Ubiquitous):** CRC-16은 MIPI CSI-2 사양에 따라 CCITT 다항식 (0x1021)을 사용하여 롱 패킷 페이로드에 대해 계산되어야 한다 (SHALL).

**R-SIM-013 (Ubiquitous):** ECC는 MIPI CSI-2 사양 Annex A에 따라 3바이트 패킷 헤더 (DI + WC[7:0] + WC[15:8])에 대해 계산되어야 한다 (SHALL).

**R-SIM-014 (Ubiquitous):** 2-lane 바이트 인터리빙: Lane 0 = bytes [0,4,8,...], Lane 1 = bytes [1,5,9,...] 패턴을 구현해야 한다 (SHALL).

**R-SIM-015 (Ubiquitous):** 4-lane 바이트 인터리빙: Lane N = bytes [N, N+4, N+8,...] (N=0,1,2,3) 패턴을 구현해야 한다 (SHALL).

**R-SIM-016 (Event-Driven):** 프레임 간 LP 모드 전환이 트리거될 때, LP->HS 전환 시퀀스가 MIPI D-PHY <=1 us 요구사항을 충족하는지 검증해야 한다 (SHALL).

### Module 3: Line Buffer CDC Verification

**R-SIM-017 (Ubiquitous):** 라인 버퍼 CDC 모델은 DCLK (AFE 생성) -> SYS_CLK (100 MHz) 클럭 도메인 교차를 dual-clock BRAM 또는 async FIFO 모델로 시뮬레이션해야 한다 (SHALL).

**R-SIM-018 (Ubiquitous):** Async FIFO 모델은 AFE 그룹당 >=16 words 깊이를 가져 worst-case backpressure 시나리오에서 overflow를 방지해야 한다 (SHALL).

**R-SIM-019 (Event-Driven):** Ping-pong 뱅크 스왑이 라인 완료 시 발생할 때, CSI-2 TX 모듈에 대한 데이터 가용성에 0-cycle 갭이 없어야 한다 (SHALL).

**R-SIM-020 (State-Driven):** 24개 AFE가 동시에 데이터를 수신하는 동안, 24개 독립 LVDS 수신기 인스턴스에서 데이터 손상이 발생하지 않아야 한다 (SHALL).

**R-SIM-021 (Ubiquitous):** 다중 AFE 데이터 정렬 모델은 AFE #0 ~ #23의 데이터가 라인 버퍼에 올바르게 시퀀싱되는지 (AFE 0 우선, AFE 23 마지막) 검증해야 한다 (SHALL).

### Module 4: Test Vector Pipeline

**R-SIM-022 (Ubiquitous):** 시스템은 다음 필드를 포함하는 표준 형식의 테스트 벡터를 생성해야 한다 (SHALL): 사이클 번호 (uint64, 0-indexed), 입력 신호 맵 (signal_name:hex_value 쌍), 예상 출력 신호 맵 (signal_name:hex_value 쌍), 레지스터 설정 스냅샷 (0x00-0x1F 값). 파일 헤더에 @MODULE, @SPEC, @SIGNALS_IN, @SIGNALS_OUT, @CLOCK 메타데이터를 포함해야 한다 (SHALL).

**R-SIM-023 (Ubiquitous):** 테스트 벡터는 경계 조건을 포함해야 한다: 최소 tLINE (AD71124=2200, AD71143=6000), 최대 행 수 (3072), 최소/최대 SPI 클럭 (1/10 MHz) (SHALL).

**R-SIM-024 (Ubiquitous):** 테스트 벡터는 에러 주입 케이스를 포함해야 한다: X_RAY_READY 타임아웃 (SPEC-002 기본 5초; SPEC-010 radiography 모드 30초 — X-ray 제너레이터 HV 충전 대기), VGH 과전압 (>38V), PLL unlock, AFE FIFO overflow (SHALL).

**R-SIM-025 (Ubiquitous):** 테스트 벡터 파일은 sim/golden_models/test_vectors/에 hex 형식 (cocotb용)과 binary 형식 (Verilator용) 모두로 생성되어야 한다 (SHALL).

### Module 5: cocotb Integration

**R-SIM-026 (Ubiquitous):** 각 SPEC 모듈은 sim/cocotb_tests/에 test_{module_name}.py 명명 규칙을 따르는 cocotb 테스트벤치 파일을 가져야 한다 (SHALL).

**R-SIM-027 (Ubiquitous):** cocotb 테스트벤치는 C++ 골든 모델이 생성한 테스트 벡터를 파일 I/O로 자동 로드해야 한다 (하드코딩 금지) (SHALL).

**R-SIM-028 (Event-Driven):** cocotb 테스트벤치가 골든 모델 테스트 벡터를 RTL DUT에 적용할 때, DUT 출력을 예상 출력과 비트 단위로 비교해야 한다 (SHALL).

**R-SIM-029 (Ubiquitous):** cocotb 프레임워크는 Questa 라이선스 없이 Vivado xsim을 시뮬레이션 백엔드로 지원해야 한다 (SHALL).

### Module 6: Verilator Integration

**R-SIM-030 (Ubiquitous):** Verilator 시뮬레이션 프레임워크는 각 RTL 모듈을 C++로 컴파일하고 대응하는 C++ 골든 모델과 링크하여 비교해야 한다 (SHALL).

**R-SIM-031 (Ubiquitous):** golden_compare.cpp는 골든 모델 출력과 Verilated RTL 출력 사이의 사이클 정확 비교를 수행하고, 첫 번째 불일치 사이클과 신호명을 보고해야 한다 (SHALL).

**R-SIM-032 (State-Driven):** C1-C5 패널 (2048x2048, 16-bit) 전체 프레임 시뮬레이션 중, Verilator는 60초 이내에 시뮬레이션을 완료해야 한다 (SHALL).

### Module 7: Build System

**R-SIM-033 (Ubiquitous):** 빌드 시스템은 CMake (>= 3.20)를 모든 C++ 골든 모델 컴파일에 사용해야 한다 (SHALL).

**R-SIM-034 (Ubiquitous):** 빌드 시스템은 Windows (MSVC 2022) 및 Linux (GCC >= 11, Clang >= 14)에서 플랫폼 특화 코드 변경 없이 컴파일을 지원해야 한다 (SHALL).

**R-SIM-035 (Event-Driven):** Verilator 시뮬레이션이 골든 모델과 RTL 간 불일치를 감지할 때, 빌드는 non-zero 반환 코드로 종료하여 CI/CD 파이프라인을 실패시켜야 한다 (SHALL).

**R-SIM-036 (Ubiquitous):** C++ 골든 모델 소스 코드의 라인 커버리지는 90%를 초과해야 한다 (SHALL). 측정 도구: gcov (GCC) 또는 llvm-cov (Clang).

### Module 8: System-Level Requirements

**R-SIM-037 (System-Level):** CSI-2 MIPI TX는 v1의 PRIMARY 데이터 출력이어야 하며 (SHALL), MCU 데이터 인터페이스는 legacy로 제어/설정용으로만 사용될 수 있다 (MAY).

**R-SIM-038 (Event-Driven):** Radiography 모드에서 X_RAY_READY 타임아웃은 30초여야 한다 (SHALL). 이는 SPEC-002의 기본 5초 타임아웃과 별개이다.

**R-SIM-039 (Ubiquitous):** CDC 모델은 리셋 동기화 (async reset assertion across clock domains), MMCM 락 동기화 (FF chain), 다중 AFE SYNC 도착 스큐 (max ±1 MCLK period = ±31ns at 32MHz)를 명시적으로 모델링해야 한다 (SHALL).

**R-SIM-040 (Ubiquitous):** Safety 골든 모델 (ProtMonModel, PowerSeqModel, EmergencyShutdownModel)은 SPEC-FPD-008의 모든 보호 기능을 C++로 모델링해야 하며, 5초 타임아웃, 과전압/과온도 감지, VGL→VGH 전원 시퀀스(10ms 안정화, ≤5V/ms 슬루율)를 포함해야 한다 (SHALL).

### Non-Functional Requirements

**NFR-SIM-001 (Regulatory):** 검증 방법론은 IEC 62220-1 준수 문서화를 지원해야 한다 (SHALL). 테스트 커버리지 보고서는 수용기준 ID (AC-001-x ~ AC-010-x)로 추적 가능해야 한다.

**NFR-SIM-002 (Performance):** 전체 프레임 Verilator 시뮬레이션은 2048x2048에서 60초, 3072x3072에서 120초 이내에 완료되어야 한다 (SHALL).

**NFR-SIM-003 (Performance):** cocotb 테스트 스위트는 단일 SPEC 모듈당 30분 이내에 완료되어야 한다 (SHALL).

**NFR-SIM-004 (Portability):** 골든 모델은 Windows (MSVC 2022, /std:c++17)와 Linux (GCC >= 11, -std=c++17) 양 플랫폼에서 플랫폼 특화 코드 없이 빌드되어야 한다 (SHALL).

---

## 3. Architecture Decision: Pure C++ (NOT SystemC)

### 결정: SystemC 미사용, 순수 C++17 채택

**근거:**
- SystemC는 20% 이상의 학습/통합 오버헤드 추가 — 이 프로젝트 규모에서는 과잉
- 순수 C++로 behavioral 모델링 충분 (HDL 시뮬레이션 레이어 불필요)
- Verilator DPI-C로 RTL과 직접 연결 가능
- 빌드 복잡도 최소화 (SystemC 라이브러리 의존성 제거)
- 업계 참조: OpenTitan 프로젝트도 순수 C++ golden model 채택

### 대안 분석

| 접근법 | 장점 | 단점 | 결정 |
|--------|------|------|------|
| Pure C++17 | 빌드 단순, 빠른 반복, DPI-C 직접 연결 | TLM 추상화 없음 | **채택** |
| SystemC | TLM 2.0, 시간 모델링 내장 | 오버헤드, 라이선스, 학습 곡선 | 기각 |
| Python only | cocotb와 통합 최적 | 성능 부족 (10-100x 느림) | 기각 |

---

## 4. Architecture Decision: Test Vector Interface

### 결정: 파일 기반 테스트 벡터 + DPI-C 하이브리드

**Phase 1 (RTL 전):** 파일 기반 — C++ 모델이 hex/bin 파일 생성, cocotb가 로드
**Phase 2 (RTL 후):** DPI-C — Verilator가 C++ 모델과 RTL을 동일 프로세스에서 실행, 사이클별 비교

**근거:**
- Phase 1에서 파일 기반은 C++와 Python 간 디커플링 보장
- Phase 2에서 DPI-C는 사이클별 비교로 디버깅 효율 극대화
- 두 방식 모두 CI/CD 파이프라인에서 자동화 가능

---

## 5. Technical Constraints

### 5.1 Naming Convention

C++ 골든 모델과 RTL 모듈 간 네이밍 규칙:
- C++ 클래스: PascalCase (예: Csi2PacketModel, AfeAd711xxModel)
- RTL 모듈: snake_case (예: csi2_packet_builder, afe_ad711xx)
- C++ 파일명: PascalCase.h/.cpp (예: Csi2PacketModel.h)
- RTL 파일명: snake_case.sv (예: csi2_packet_builder.sv)
- 1:1 매핑: Csi2PacketModel ↔ csi2_packet_builder, AfeAd711xxModel ↔ afe_ad711xx

### 타이밍 파라미터 (데이터시트 기준)

| Component | Parameter | Value | Source |
|-----------|-----------|-------|--------|
| AD71124 | tLINE min | 22 us (REG_TLINE >= 2200, 1 unit = 10 ns) | AD71124 datasheet |
| AD71143 | tLINE min | 60 us (REG_TLINE >= 6000, 1 unit = 10 ns) | AD71143 datasheet |
| AFE2256 | tLINE min | 51.2 us (REG_TLINE >= 5120, 1 unit = 10 ns) | AFE2256 datasheet |
| AFE2256 | MCLK | 32 MHz | AFE2256 datasheet |
| NV1047 | CLK max | 200 kHz | NV1047 datasheet |
| NT39565D | CPV max | 200 kHz, ~100 kHz recommended | NT39565D datasheet |
| CSI-2 | Lane rate | 1.0-1.5 Gbps/lane | MIPI CSI-2 spec |
| SYS_CLK | Frequency | 100 MHz | FPGA design |
| ACLK | Frequency | 10-40 MHz (default 10) | MMCM |
| Gate Settle Time | T_gate_settle | ≥2 us | Row 전환 시 크로스토크 방지 |
| SYNC Skew (Multi-AFE) | Max skew | ±31 ns (±1 MCLK @ 32MHz) | 다중 AFE 동기화 |
| tLINE per AFE type | AD71124≥2200, AD71143≥6000, AFE2256≥5120 | 10ns 단위 | REG_TLINE 설정 |
| VGL Stabilization | Delay | 10 ms | Gate IC 전원 시퀀싱 |
| VGH Stabilization | Delay | 10 ms | Gate IC 전원 시퀀싱 |
| Max Slew Rate | Voltage | ≤5 V/ms | 전원 시퀀싱 |

### BRAM Budget (골든 모델 검증 대상)

| Use | BRAM36K | Golden Model Abstraction |
|-----|---------|--------------------------|
| Line buffer (ping-pong) | 4 | std::array<uint16_t, 2048> x 2 |
| LVDS async FIFO | 2-4 | std::queue<uint16_t> per AFE |
| **Total** | 6-8 | C++ containers |

### 클럭 도메인 (CDC 검증 대상)

| Domain | Frequency | Source | Golden Model |
|--------|-----------|--------|--------------|
| SYS_CLK | 100 MHz | MMCM | Primary step() clock |
| ACLK | 10-40 MHz | MMCM → AFE | Derived from SYS_CLK ratio |
| MCLK | 32 MHz | MMCM → AFE2256 | Derived from SYS_CLK ratio |
| DCLK | AFE internal | AFE output | Independent step() clock |

---

## 6. Dependencies

### 선행 의존성
- SPEC-FPD-001 ~ 010의 모듈 사양 (포트 정의, 레지스터 맵, 타이밍 파라미터) — 이미 implementation-plan.md에 정의 완료

### 도구 의존성
- CMake >= 3.20
- C++17 컴파일러 (MSVC 2022 / GCC >= 11)
- Verilator >= 5.x (open-source)
- cocotb >= 1.8 (BSD license)
- Vivado 2025.2 (xsim backend)
- GoogleTest (C++ unit test)

### 후행 의존성
- 각 SPEC-FPD-00x의 RTL 구현 시 대응 골든 모델이 먼저 완료되어야 함
- SPEC-FPD-009 통합 테스트 시 전체 데이터 경로 골든 모델 필요

---

## 7. Risk Assessment

| ID | Risk | Impact | Probability | Mitigation |
|----|------|--------|-------------|------------|
| RISK-1 | C++ 모델이 실제 RTL 동작을 완전히 캡처하지 못함 (ISERDESE2, MMCM 타이밍) | HIGH | MEDIUM | 골든 모델을 behavioral 정확도로 제한; 타이밍 검증은 Vivado xsim SDF |
| RISK-2 | Verilator의 SystemVerilog 호환성 제한 (interface array, 24-AFE 인스턴스) | MEDIUM | MEDIUM | 조기 호환성 테스트; Verilator workaround 문서화 |
| RISK-3 | MSVC/GCC 간 비트필드/정수 오버플로 동작 차이 | LOW | LOW | 명시적 고정폭 타입 (uint16_t) 사용; 양 플랫폼 CI |
| RISK-4 | 전체 프레임 테스트 벡터 파일 크기 (8 MB/frame) | LOW | LOW | 대표 벡터만 커밋; 전체 프레임은 런타임 생성 |
| RISK-5 | cocotb + xsim 통합 불안정 | MEDIUM | LOW | Verilator를 primary, xsim을 secondary로 사용 |
| RISK-6 | 24-AFE 동시 시뮬레이션 성능 병목 | MEDIUM | MEDIUM | AFE 인스턴스 루프 최적화; 벡터화 활용 |

---

Version: 1.1.0
Created: 2026-03-19
Updated: 2026-03-20
Based on: Team research (researcher + analyst + architect) + cross-verification review
