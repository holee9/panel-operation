# SPEC-FPD-SIM-001 v1.2.0 구현 코드 리뷰

**문서 버전**: v5
**기준 SPEC**: SPEC-FPD-SIM-001 v1.2.0 (52 R-SIM + 47 AC-SIM + 8 EC-SIM)
**리뷰일**: 2026-03-21
**분석 방법**: 2개 병렬 에이전트 (RTL+골든모델 전수 리뷰, 테스트+AC 검증)
**분석 대상**: RTL 27개 모듈, 골든 모델 23개 클래스, C++ 테스트 13개, cocotb 14개

---

## 1. Executive Summary

| 지표 | 현황 | 목표 | 비고 |
|------|------|------|------|
| 요구사항 구현율 | 25/52 IMPL (48%) | 100% | 18 PARTIAL, 9 NOT_IMPL |
| 수용기준 통과율 | 0/47 PASS (0%) | 100% | 20 STUB, 15 NOT_TESTED, 12 PARTIAL |
| 기능 커버리지 | ~68% | 80% | RTL 구조 양호, 테스트 부재 |
| RTL 완성도 | 75% | 100% | 27 모듈 구조 완성, 세부 로직 갭 |
| 골든 모델 완성도 | 71% | 100% | 23/30 클래스, 다수 stub |
| C++ 테스트 LOC | 365 | ~1,800 | 13개 파일, 단일 assert 수준 |
| cocotb 테스트 LOC | 208 | ~3,400 | 14개 파일, reset/idle 확인만 |
| CRITICAL 발견 | **6건** | 0 | 즉시 조치 필요 |

**핵심 판정**: RTL 모듈 구조와 골든 모델 프레임워크는 양호하나, **테스트가 사실상 미구현**(C++ 365 LOC, cocotb 208 LOC — 계획의 6~18%). AC-SIM 47건 중 **실제 PASS 0건** (모두 stub 또는 미테스트).

---

## 2. CRITICAL 발견 (6건)

### CR-001: Settle Time 미구현 (R-SIM-043)
- **RTL**: ST_SETTLE 상태가 fpd_types_pkg.sv에 정의되었으나, panel_ctrl_fsm.sv에서 cfg_tgate_settle을 사용하지 않음
- **골든 모델**: PanelFsmModel state_=8 매핑 존재, tgate_settle_ 미적용
- **영향**: X_RAY_OFF 후 TFT 전하 안정화 없이 리드아웃 진행 → 노이즈

### CR-002: 방사선 30s 타임아웃 미분리 (R-SIM-038)
- **RTL**: XRAY_TIMEOUT_5S, XRAY_TIMEOUT_30S 상수 정의 (panel_ctrl_fsm.sv:57-58), 모드별 분기 불완전
- **골든 모델**: ProtMonModel에 cfg_max_exposure_ 단일 값, 모드 인식 없음
- **영향**: 방사선 모드에서 5s 조기 타임아웃 → false ERROR

### CR-003: NV1047 Break-Before-Make 미구현 (R-SIM-046)
- **RTL**: gate_nv1047.sv 시프트 레지스터 동작하나, OE 해제 → 행 전환 → OE 재활성 사이 gap 없음
- **골든 모델**: GateNv1047Model에 break-before-make 로직 없음
- **영향**: 행 전환 시 crosstalk — Gate IC 손상 가능

### CR-004: NT39565D Dual-STV 미구현 (R-SIM-047)
- **RTL**: gate_nt39565d.sv stv1l/stv2l 포트 선언됨, 실제 STV 토글 로직 없음 (항상 0)
- **골든 모델**: GateNt39565dModel 출력 선언됨, step()에서 미수정
- **영향**: C6/C7 (3072×3072 대형 패널) 행 주소 지정 불가 — **조합 완전 차단**

### CR-005: TLINE_MIN 레지스터 검증 부재 (R-SIM-041)
- **RTL**: detector_core.sv에 combo_min_tline() 내부 클램핑 존재, reg_bank에 쓰기 검증 없음
- **골든 모델**: 클램핑 로직 없음
- **영향**: MCU가 C2에 TLINE=2200 설정 시 무경고 → CDS 윈도우 부족 → 데이터 손상

### CR-006: CSI-2 ECC 미구현 (R-SIM-013)
- **RTL**: csi2_packet_builder.sv 헤더에 ECC=0x00 고정 (placeholder)
- **골든 모델**: Csi2PacketModel에 ComputeCsi2Ecc() 구현됨 (골든 모델만)
- **영향**: CSI-2 수신기가 패킷 거부 — MIPI 비호환

---

## 3. HIGH 발견 (8건)

| ID | 모듈 | 문제 | R-SIM |
|----|------|------|-------|
| HI-001 | PanelFsmModel | FSM 확장 상태(S1/S3/S5/S7) 의미 구현 불완전 — S1_POWER_CHECK 즉시 통과 | R-SIM-051 |
| HI-002 | RadiogModel | X_RAY_READY 핸드셰이크 + 다크 프레임 평균화 미완성 | R-SIM-042, 050 |
| HI-003 | AfeAfe2256Model | 파이프라인 지연 0행 (사양 1행), CIC 보상 미구현 | R-SIM-006, 052 |
| HI-004 | detector_core | C6/C7 12-AFE LVDS 인스턴스화 없음, 3072열 무결성 미검증 | R-SIM-044 |
| HI-005 | Csi2LaneDistModel | 2/4레인 바이트 인터리빙 step() 로직 없음 (stub) | R-SIM-014, 015 |
| HI-006 | PowerSeqModel | VGL→VGH ≥5ms 딜레이 + ≤5V/ms 슬루율 미적용 | R-SIM-049 |
| HI-007 | FoundationConstants | combo별 NCOLS/TLINE 기본값 미분기 (전부 C1 기본값) | R-SIM-041 |
| HI-008 | RegBankModel | regs_ uint32 선언, RTL은 16비트 — 32비트 값 수용 시 비교 실패 | - |

---

## 4. MEDIUM 발견 (8건)

| ID | 모듈 | 문제 |
|----|------|------|
| MD-001 | PanelIntegModel | cfg_tsettle 미모델링 (settle time) |
| MD-002 | GateNv1047Model | CLK 주파수 ≤200kHz 검증 미적용 |
| MD-003 | LineBufModel | FIFO overflow 플래그 미설정, multi-AFE 스케일링 없음 |
| MD-004 | PanelFsmModel | CONTINUOUS 모드 반복 시 S1_POWER_CHECK 재진입 |
| MD-005 | LvdsRxModel | bitslip 정렬 성공/실패 미검증 |
| MD-006 | reg_bank | C3/C5 기본 TLINE=2200 (TLINE_MIN=5120 위반) |
| MD-007 | AfeSpiMasterModel | 24칩 데이지체인 576비트 테스트 없음 |
| MD-008 | Csi2PacketModel | RTL에 CRC 없음 (골든 모델에만 구현) |

---

## 5. 수용기준 검증 매트릭스 (AC-SIM-001 ~ AC-SIM-047)

### 5.1 요약

| 상태 | 건수 | 비율 | 설명 |
|------|------|------|------|
| STUB | 20 | 43% | 테스트 존재하나 단일 assert / reset 확인만 |
| NOT_TESTED | 15 | 32% | 테스트 자체 없음 |
| PARTIAL | 12 | 25% | 일부 측면만 확인 |
| **PASS** | **0** | **0%** | **완전 통과 없음** |

### 5.2 SPEC별 AC 커버리지

| SPEC | AC 수 | 커버리지 | 비고 |
|------|-------|----------|------|
| SPEC-FPD-001 (SPI) | 2 | 0% | RTL-골든 비교 없음 |
| SPEC-FPD-002 (FSM) | 11 | 9% (1/11 stub) | 타임아웃, 모드, 확장 상태 미테스트 |
| SPEC-FPD-003 (NV1047) | 3 | 33% (1/3 stub) | OE 기본만, SR/타이밍 미검증 |
| SPEC-FPD-004 (NT39565D) | 2 | 50% (1/2 stub) | cascade 신호만, STV 미테스트 |
| SPEC-FPD-005 (AD711xx) | 4 | 25% (1/4 stub) | IFS 오버플로만, 256ch 미검증 |
| SPEC-FPD-006 (AFE2256) | 4 | 25% (1/4 stub) | ready 신호만, CIC/파이프라인 미테스트 |
| SPEC-FPD-007 (CSI-2/Data) | 9 | 22% (2/9 stub) | CRC+ECC 기본만, 레인/스트레스 없음 |
| SPEC-FPD-008 (Safety) | 3 | 0% | 전원/보호 테스트 전무 |
| SPEC-FPD-010 (Radiography) | 5 | 40% (2/5 stub) | 기본 done만, 타임아웃/핸드셰이크 없음 |
| Infrastructure | 6 | 17% (1/6 stub) | 벡터 I/O만, CI/CD/Mismatch 없음 |
| **v1.2.0 신규 (035~047)** | **13** | **8% (1/13 stub)** | **combo NCOLS만 부분 확인** |

### 5.3 엣지케이스 (EC-SIM-001 ~ EC-SIM-008)

**전체 0/8 테스트됨** — SPI 경합, FIFO 배압, FSM ABORT, 24-AFE 데이지체인, CSI-2 LP↔HS, SYNC 스큐, VGL 래치업, TLINE 불일치 모두 미검증.

---

## 6. 테스트 현황 상세

### 6.1 C++ 단위 테스트 (13개 파일, 365 LOC)

| 파일 | LOC | assert 수 | 판정 |
|------|-----|-----------|------|
| test_spi_model.cpp | 32 | 2 | STUB — Mode 3 idle + R/O 확인만 |
| test_reg_bank.cpp | 37 | 3 | STUB — 버전 + W/R + 상태 |
| test_clk_rst.cpp | 28 | 2 | STUB — PLL lock + reset sync |
| test_panel_fsm.cpp | 30 | 2 | STUB — 12 step, idle/done만 |
| test_crc16.cpp | 15 | 1 | STUB — '123456789' 단일 벡터 |
| test_ecc.cpp | 15 | 1 | STUB — bit flip 변화 확인 |
| test_csi2_model.cpp | 18 | 1 | STUB — 패킷 크기/헤더 |
| test_gate_models.cpp | 25 | 2 | STUB — OE 펄스 + cascade |
| test_afe_models.cpp | 48 | 4 | STUB — config + IFS + LVDS |
| test_data_path_models.cpp | 40 | 3 | STUB — FIFO + mux + IRQ |
| test_radiog_model.cpp | 38 | 2 | STUB — done + 2프레임 평균 |
| test_panel_aux_models.cpp | 22 | 2 | STUB — reset/integ 즉시 |
| test_vector_io.cpp | 27 | 1 | STUB — hex 라운드트립 |
| **합계** | **365** | **~26** | **평균 1.9 assert/파일** |

### 6.2 cocotb 통합 테스트 (14개 파일, 208 LOC)

| 파일 | LOC | 내용 | 판정 |
|------|-----|------|------|
| test_spi_slave.py | 20 | SPI idle defaults | STUB |
| test_clk_rst.py | 27 | PLL lock after reset | STUB |
| test_reg_bank.py | 27 | Register defaults | STUB |
| test_panel_fsm.py | 15 | FSM reset→idle | STUB |
| test_gate_nv1047.py | 14 | NV1047 idle levels | STUB |
| test_afe_ad711xx.py | 15 | AFE config request | STUB |
| test_line_buf.py | 13 | Line buffer idle | STUB |
| test_csi2_tx.py | 13 | CSI-2 reset state | STUB |
| test_safety.py | 13 | ProtMon reset | STUB |
| test_integration.py | 13 | Detector smoke test | STUB |
| test_radiography.py | 13 | Radiography reset | STUB |
| 기타 3개 | ~13 each | idle 확인 | STUB |
| **합계** | **208** | **평균 14.9 LOC/파일** | **전수 STUB** |

### 6.3 테스트 벡터 생성기 (6개)

gen_spi_vectors, gen_fsm_vectors, gen_gate_vectors, gen_afe_vectors, gen_csi2_vectors, gen_safety_vectors — 파일 생성은 동작하나 **소비자(cocotb/Verilator) 검증 연결 없음**.

---

## 7. 모듈별 완성도

```
모듈                      RTL    골든모델   종합
──────────────────────────────────────────────
fpd_types_pkg.sv          95%    -         95%
fpd_params_pkg.sv         80%    -         80%
spi_slave_if.sv           85%    85%       85%
reg_bank.sv               90%    85%       87%
clk_rst_mgr.sv            85%    80%       82%
panel_ctrl_fsm.sv         70%    80%       75%
panel_integ_ctrl.sv       60%    60%       60%
panel_reset_ctrl.sv       85%    75%       80%
power_sequencer.sv        75%    70%       72%
prot_mon.sv               65%    65%       65%
emergency_shutdown.sv     90%    90%       90%
gate_nv1047.sv            65%    65%       65%
gate_nt39565d.sv          60%    55%       57%  ★
afe_ad711xx.sv            75%    85%       80%
afe_afe2256.sv            70%    70%       70%
afe_spi_master.sv         75%    75%       75%
row_scan_eng.sv           70%    75%       72%
line_data_rx.sv           85%    75%       80%
line_buf_ram.sv           90%    80%       85%
data_out_mux.sv           85%    80%       82%
mcu_data_if.sv            80%    75%       77%
csi2_packet_builder.sv    65%    75%       70%
csi2_lane_dist.sv         70%    70%       70%
detector_core.sv          70%    -         70%
fpga_top_c1/c3/c6.sv     85%    -         85%
RadiogModel               -      50%       50%  ★
전체 평균                  77%    73%       75%
```

★ = CRITICAL 갭 포함 모듈

---

## 8. 우선순위별 조치 항목

### Priority 1 — CRITICAL (RTL 검증 차단)

| # | 항목 | 모듈 | 예상 |
|---|------|------|------|
| 1 | NT39565D dual-STV + STVD 캐스케이드 실구현 | gate_nt39565d + 골든 모델 | 2일 |
| 2 | CSI-2 ECC 바이트 생성 RTL 구현 | csi2_packet_builder | 0.5일 |
| 3 | TLINE_MIN 레지스터 쓰기 검증 + 에러 플래그 | reg_bank | 0.5일 |
| 4 | Settle time ST_SETTLE 상태 실구현 | panel_ctrl_fsm + 골든 모델 | 0.5일 |
| 5 | 방사선 5s/30s 듀얼 타임아웃 분리 | prot_mon + panel_ctrl_fsm | 0.5일 |
| 6 | NV1047 break-before-make gap 구현 | gate_nv1047 + 골든 모델 | 0.5일 |

### Priority 2 — HIGH (시뮬레이션 정확도)

| # | 항목 | 예상 |
|---|------|------|
| 7 | AFE2256 파이프라인 지연 0→1행 + CIC 보상 | 1일 |
| 8 | RadiogModel 핸드셰이크 + 다크 프레임 평균화 | 1일 |
| 9 | CSI-2 2/4레인 인터리빙 step() 구현 | 1일 |
| 10 | Multi-AFE 12인스턴스 + 3072열 검증 | 3일 |
| 11 | PowerSeqModel VGL→VGH 5ms 딜레이 + 슬루 | 0.5일 |
| 12 | FoundationConstants combo별 기본값 분기 | 0.5일 |
| 13 | FSM S1_POWER_CHECK 실제 전원 검증 로직 | 0.5일 |
| 14 | RegBankModel uint32→uint16 정렬 | 0.5일 |

### Priority 3 — 테스트 실구현 (커버리지 0%→80%)

| # | 항목 | 예상 |
|---|------|------|
| 15 | C++ 단위 테스트 365→1,800 LOC 확장 | 5일 |
| 16 | cocotb 테스트 208→3,400 LOC 확장 | 7일 |
| 17 | RTL-골든 모델 bit-exact 비교 cocotb 구현 | 3일 |
| 18 | Verilator 풀프레임 통합 테스트 | 3일 |
| 19 | 엣지케이스 8건 테스트 구현 | 2일 |
| 20 | CI/CD 파이프라인 (JUnit XML, <30분) | 1일 |

---

## 9. 이전 리뷰 대비 변화

| 항목 | v4 | v5 (현재) |
|------|-----|-----------|
| AC-SIM PASS | 22 (47%) | **0 (0%)** — stub을 PASS로 카운트하지 않음 |
| 테스트 판정 기준 | 파일 존재 = PASS | **실제 검증 로직 유무** 기준 |
| CRITICAL 건수 | 6 | 6 (내용 변경) |
| 핵심 신규 발견 | - | CSI-2 ECC RTL 부재, break-before-make, 듀얼 타임아웃 |
| 테스트 LOC 분석 | 없음 | **C++ 365, cocotb 208 (계획의 6~18%)** |

**v4→v5 핵심 차이**: v4는 파일 존재를 PASS로 판정했으나, v5는 **실제 검증 로직**이 있는지 확인. 결과적으로 AC-SIM PASS가 22→0으로 하향 조정됨. 이는 테스트 품질이 낮은 것이지 RTL/골든 모델 구조가 나쁜 것은 아님.

---

## 10. 결론

**RTL 모듈 구조(75%)와 골든 모델 프레임워크(71%)는 양호**. 22개 골든 모델 클래스가 GoldenModelBase 인터페이스를 충실히 구현하고, RTL 27개 모듈이 전체 계층을 구성.

**테스트가 사실상 미구현**. C++ 365 LOC (계획 1,800의 20%), cocotb 208 LOC (계획 3,400의 6%). 모든 테스트가 stub 수준(단일 assert, reset/idle 확인만)이어서 **AC-SIM 47건 중 실제 PASS 0건**.

**즉시 조치 필요한 CRITICAL 6건** (Priority 1: ~4.5일) 해결 후, **테스트 실구현** (Priority 3: ~21일)이 커버리지 80% 달성의 핵심.

---

*Generated by MoAI Code Review Pipeline v5*
*SPEC: SPEC-FPD-SIM-001 v1.2.0 (52 R-SIM + 47 AC-SIM + 8 EC-SIM)*
*Analyzed: 27 RTL modules + 23 golden models + 13 C++ tests + 14 cocotb tests*
