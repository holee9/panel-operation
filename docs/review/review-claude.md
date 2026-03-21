# SPEC-FPD-SIM-001 v1.2.0 구현 코드 리뷰 + 개선 계획

**문서 버전**: v6
**기준 SPEC**: SPEC-FPD-SIM-001 v1.2.0 (52 R-SIM + 47 AC-SIM + 8 EC-SIM)
**리뷰일**: 2026-03-21
**빌드 검증**: TVR-FPD-SIM-001-001 (MSVC 19.40, 13/13 PASS)
**분석 대상**: RTL 27개 모듈, 골든 모델 23개 클래스, C++ 테스트 13개, cocotb 14개

---

## 1. Executive Summary

| 지표 | 현황 | 목표 | 비고 |
|------|------|------|------|
| 빌드 | **PASS** (0 에러, 0 경고) | PASS | MSVC 19.40 /W4 Release |
| 단위 테스트 | **13/13 PASS** (100%) | PASS | 2건 크래시 수정 완료 |
| 요구사항 구현율 | 25/52 IMPL (48%) | 100% | 18 PARTIAL, 9 NOT_IMPL |
| 수용기준 통과율 | 0/47 실PASS (0%) | 100% | 전체 stub — 실 검증 로직 없음 |
| RTL 완성도 | 75% | 100% | 27 모듈 구조 완성, 세부 로직 갭 |
| 골든 모델 완성도 | 71% | 100% | 23/30 클래스, 다수 stub |
| C++ 테스트 | 375 LOC (20%) | 1,800 LOC | 평균 2.1 assert/파일 |
| cocotb 테스트 | 208 LOC (6%) | 3,400 LOC | 전수 reset/idle stub |
| CRITICAL 발견 | **6건** | 0 | |

---

## 2. 빌드 검증 결과 (TVR-001)

| 항목 | 판정 | 상세 |
|------|------|------|
| **MSVC 빌드** | **PASS** | 0 에러, 0 경고, 21 산출물 (lib 2 + exe 19) |
| **CTest** | **PASS** | 13/13 (100%), 0.47초 |
| **결함 수정** | **PASS** | 2건 크래시 → 즉시 수정 (done_ 펄스, settle step 부족) |
| **정적 분석** | **PASS** | raw new/delete 0, C-cast 0, 멤버 초기화 205건 전수 |
| **동적 분석** | **조건부** | ASan VS IDE 실행 권장 |

---

## 3. 발견 사항 요약

### 3.1 CRITICAL (6건)

| ID | 모듈 | 문제 | R-SIM | 개선 계획 |
|----|------|------|-------|-----------|
| CR-001 | panel_ctrl_fsm + PanelFsmModel | ST_SETTLE 상태에서 cfg_tgate_settle 미사용 | R-SIM-043 | Sprint 1 |
| CR-002 | prot_mon + PanelFsmModel | 5s/30s 듀얼 타임아웃 미분리 | R-SIM-038 | Sprint 1 |
| CR-003 | gate_nv1047 + GateNv1047Model | Break-before-make gap 없음 | R-SIM-046 | Sprint 1 |
| CR-004 | gate_nt39565d + GateNt39565dModel | Dual-STV 토글 로직 미구현 (항상 0) | R-SIM-047 | Sprint 1 |
| CR-005 | reg_bank + RegBankModel | TLINE_MIN 쓰기 검증 + 에러 플래그 없음 | R-SIM-041 | Sprint 1 |
| CR-006 | csi2_packet_builder | ECC=0x00 고정 (RTL 미구현) | R-SIM-013 | Sprint 1 |

### 3.2 HIGH (8건)

| ID | 모듈 | 문제 | R-SIM | 개선 계획 |
|----|------|------|-------|-----------|
| HI-001 | PanelFsmModel | S1_POWER_CHECK 즉시 통과 (실 검증 없음) | R-SIM-051 | Sprint 2 |
| HI-002 | RadiogModel | 핸드셰이크 + 다크 프레임 평균화 미완성 | R-SIM-042,050 | Sprint 2 |
| HI-003 | AfeAfe2256Model | 파이프라인 지연 0행, CIC 보상 미구현 | R-SIM-006,052 | Sprint 2 |
| HI-004 | detector_core | C6/C7 12-AFE LVDS 미인스턴스화 | R-SIM-044 | Sprint 3 |
| HI-005 | Csi2LaneDistModel | 2/4레인 인터리빙 step() stub | R-SIM-014,015 | Sprint 2 |
| HI-006 | PowerSeqModel | VGL→VGH ≥5ms 딜레이, ≤5V/ms 슬루 미적용 | R-SIM-049 | Sprint 2 |
| HI-007 | FoundationConstants | combo별 NCOLS/TLINE 기본값 전부 C1 값 | R-SIM-041 | Sprint 1 |
| HI-008 | RegBankModel | regs_ uint32 (RTL 16비트) 폭 불일치 | - | Sprint 2 |

### 3.3 MEDIUM (8건)

| ID | 모듈 | 문제 | 개선 계획 |
|----|------|------|-----------|
| MD-001 | PanelIntegModel | cfg_tsettle 미모델링 | Sprint 2 |
| MD-002 | GateNv1047Model | CLK ≤200kHz 검증 미적용 | Sprint 2 |
| MD-003 | LineBufModel | FIFO overflow 플래그/multi-AFE 스케일링 없음 | Sprint 3 |
| MD-004 | PanelFsmModel | CONTINUOUS 모드 S1 재진입 | Sprint 2 |
| MD-005 | LvdsRxModel | bitslip 정렬 미검증 | Sprint 3 |
| MD-006 | reg_bank | C3/C5 기본 TLINE=2200 (MIN=5120 위반) | Sprint 1 |
| MD-007 | AfeSpiMasterModel | 24칩 데이지체인 미테스트 | Sprint 3 |
| MD-008 | Csi2PacketModel | RTL CRC 미구현 (골든 모델만) | Sprint 2 |

---

## 4. 수용기준 현황 (AC-SIM-001 ~ AC-SIM-047)

| 상태 | 건수 | 비율 |
|------|------|------|
| STUB | 20 | 43% |
| NOT_TESTED | 15 | 32% |
| PARTIAL | 12 | 25% |
| **PASS** | **0** | **0%** |

**엣지케이스 (EC-SIM-001~008)**: 전체 0/8 테스트됨

---

## 5. 모듈별 완성도

```
모듈                      RTL    골든모델   종합    개선 Sprint
──────────────────────────────────────────────────────────
fpd_types_pkg.sv          95%    -         95%     -
fpd_params_pkg.sv         80%    -         80%     -
spi_slave_if.sv           85%    85%       85%     S3
reg_bank.sv               90%    85%       87%     S1 (TLINE_MIN)
clk_rst_mgr.sv            85%    80%       82%     S3
panel_ctrl_fsm.sv         70%    80%       75%     S1 (settle, timeout)
panel_integ_ctrl.sv       60%    60%       60%     S2
panel_reset_ctrl.sv       85%    75%       80%     S3
power_sequencer.sv        75%    70%       72%     S2 (VGL→VGH)
prot_mon.sv               65%    65%       65%     S1 (dual timeout)
emergency_shutdown.sv     90%    90%       90%     -
gate_nv1047.sv            65%    65%       65%     S1 (break-before-make)
gate_nt39565d.sv          60%    55%       57%  ★  S1 (dual-STV)
afe_ad711xx.sv            75%    85%       80%     S2
afe_afe2256.sv            70%    70%       70%     S2 (CIC, pipeline)
afe_spi_master.sv         75%    75%       75%     S3
row_scan_eng.sv           70%    75%       72%     S2
line_data_rx.sv           85%    75%       80%     S3
line_buf_ram.sv           90%    80%       85%     S3
data_out_mux.sv           85%    80%       82%     -
mcu_data_if.sv            80%    75%       77%     -
csi2_packet_builder.sv    65%    75%       70%     S1 (ECC), S2 (CRC)
csi2_lane_dist.sv         70%    70%       70%     S2 (인터리빙)
detector_core.sv          70%    -         70%     S3 (multi-AFE)
fpga_top_c1/c3/c6.sv     85%    -         85%     -
RadiogModel               -      50%       50%  ★  S2 (핸드셰이크)
전체 평균                  77%    73%       75%
```

---

## 6. 코드 개선 계획

### 6.1 개요

| Sprint | 기간 | 목표 | 파일 수 | 예상 LOC |
|--------|------|------|---------|----------|
| **Sprint 1** | 5일 | CRITICAL 6건 해결 + 즉시 AC PASS | 14 | +800 |
| **Sprint 2** | 8일 | HIGH 8건 해결 + 골든 모델 완성 | 18 | +1,500 |
| **Sprint 3** | 8일 | 테스트 실구현 + 통합 검증 | 27 | +3,800 |
| **합계** | **21일** | CRITICAL 0, AC PASS ≥80% | | **+6,100** |

### 6.2 Sprint 1 — CRITICAL 해결 (5일)

**목표**: CRITICAL 6건 전수 해결, AC-SIM 최소 8건 PASS

#### S1-1: NT39565D Dual-STV 실구현 (2일) — CR-004

| 작업 | 파일 | 내용 |
|------|------|------|
| RTL | gate_nt39565d.sv | STV1/STV2 홀짝 행 토글 + OE1/OE2 좌우 독립 + STVD 6칩 캐스케이드 |
| 골든 모델 | GateNt39565dModel.cpp | step()에서 STV/OE 상태 갱신 로직 구현 |
| 테스트 | test_gate_models.cpp | STV 홀짝 검증 + 6칩 전파 타이밍 + OE 독립 assert 추가 |
| AC 대상 | AC-SIM-022, AC-SIM-041 | 6칩 캐스케이드 + dual-STV 검증 |

#### S1-2: CSI-2 ECC RTL 구현 (0.5일) — CR-006

| 작업 | 파일 | 내용 |
|------|------|------|
| RTL | csi2_packet_builder.sv | MIPI Annex A ECC 바이트 생성 (3바이트 헤더 → 1바이트 ECC) |
| 테스트 | test_csi2_model.cpp | ECC 비트 정합 assert + 1비트 오류 정정 검증 |
| AC 대상 | AC-SIM-009, AC-SIM-010 | CSI-2 패킷 CRC + ECC |

#### S1-3: TLINE_MIN 레지스터 검증 (0.5일) — CR-005, HI-007, MD-006

| 작업 | 파일 | 내용 |
|------|------|------|
| RTL | reg_bank.sv | REG_TLINE 쓰기 시 combo별 TLINE_MIN 클램핑 + 에러 플래그 |
| 골든 모델 | FoundationConstants.h | MakeDefaultRegisters()에 combo 파라미터 추가: C4=1664, C6=3072 |
| 골든 모델 | RegBankModel.cpp | TLINE_MIN 검증 로직 추가 |
| 테스트 | test_reg_bank.cpp | C2 TLINE=2200 → 6000 클램핑 assert + C4 NCOLS=1664 assert |
| AC 대상 | AC-SIM-035, AC-SIM-036 | TLINE_MIN 클램핑 + combo NCOLS |

#### S1-4: Settle Time 실구현 (0.5일) — CR-001

| 작업 | 파일 | 내용 |
|------|------|------|
| RTL | panel_ctrl_fsm.sv | ST_SETTLE case에서 cfg_tgate_settle 카운터 적용 |
| 골든 모델 | PanelFsmModel.cpp | state_=8에서 tgate_settle_ 실적용 |
| 테스트 | test_panel_fsm.cpp | settle 5 사이클 대기 후 state 전이 검증 |
| AC 대상 | AC-SIM-038 | settle 딜레이 검증 |

#### S1-5: 듀얼 타임아웃 분리 (0.5일) — CR-002

| 작업 | 파일 | 내용 |
|------|------|------|
| RTL | panel_ctrl_fsm.sv | radiography_mode 분기에서 XRAY_TIMEOUT_30S 적용 |
| RTL | prot_mon.sv | cfg_radiography_mode 입력 추가, 타임아웃 분기 |
| 골든 모델 | ProtMonModel.cpp | cfg_max_exposure_ 모드별 전환 |
| 테스트 | test_panel_fsm.cpp | 일반 5s + 방사선 30s 타임아웃 각각 검증 |
| AC 대상 | AC-SIM-003, AC-SIM-023 | 5s/30s 타임아웃 |

#### S1-6: NV1047 Break-Before-Make (0.5일) — CR-003

| 작업 | 파일 | 내용 |
|------|------|------|
| RTL | gate_nv1047.sv | OE 해제 → cfg_bbm_gap 사이클 대기 → 행 전환 → OE 재활성 |
| 골든 모델 | GateNv1047Model.cpp | break-before-make 상태 머신 추가 |
| 테스트 | test_gate_models.cpp | OE 해제-재활성 간 gap ≥ 2µs assert |
| AC 대상 | AC-SIM-004, AC-SIM-005 | SD1 시퀀스 + OE 타이밍 |

#### Sprint 1 예상 성과

| 지표 | 현재 | Sprint 1 후 |
|------|------|-------------|
| CRITICAL | 6 | **0** |
| AC-SIM PASS | 0 | **~8** |
| 테스트 LOC (C++) | 375 | ~550 |

### 6.3 Sprint 2 — HIGH 해결 + 골든 모델 완성 (8일)

#### S2-1: AFE2256 파이프라인 + CIC (1일) — HI-003

| 작업 | 파일 | 내용 |
|------|------|------|
| 골든 모델 | AfeAfe2256Model.cpp | pipeline_latency_rows_=1 설정, CIC 보상 공식 구현 (256ch 프로파일) |
| RTL | afe_afe2256.sv | cfg_cic_profile 소비 로직 추가 |
| 테스트 | test_afe_models.cpp | 파이프라인 1행 지연 assert + CIC on/off 출력 차이 검증 |
| AC 대상 | AC-SIM-008, AC-SIM-029, AC-SIM-046 |

#### S2-2: RadiogModel 핸드셰이크 + 다크 프레임 (1일) — HI-002

| 작업 | 파일 | 내용 |
|------|------|------|
| 골든 모델 | RadiogModel.cpp | PREP→ENABLE 200ms~2s 딜레이 범위 파라미터화 + dark_accum_ 평균화 완성 |
| 테스트 | test_radiog_model.cpp | 핸드셰이크 타이밍 window + 4프레임 평균 정확도 assert |
| AC 대상 | AC-SIM-037, AC-SIM-044 |

#### S2-3: CSI-2 레인 인터리빙 (1일) — HI-005

| 작업 | 파일 | 내용 |
|------|------|------|
| RTL | csi2_lane_dist.sv | 2/4레인 바이트 분배 로직 실구현 |
| 골든 모델 | Csi2LaneDistModel.cpp | step()에서 lane 분배 + CRC 전달 |
| RTL | csi2_packet_builder.sv | CRC-16 계산 RTL 구현 (MD-008) |
| 테스트 | test_csi2_model.cpp | 2레인 [0,4,8..] + 4레인 [N,N+4..] assert |
| AC 대상 | AC-SIM-011, AC-SIM-012 |

#### S2-4: PowerSeqModel 타이밍 (0.5일) — HI-006

| 작업 | 파일 | 내용 |
|------|------|------|
| 골든 모델 | PowerSeqModel.cpp | VGL→VGH ≥5ms 딜레이 카운터 + ≤5V/ms 슬루율 검증 |
| RTL | power_sequencer.sv | T_VGL_TO_VGH 카운터 추가 |
| 테스트 | test_data_path_models.cpp 또는 신규 | VGL stable 후 5ms 경과 전 VGH assert 불가 검증 |
| AC 대상 | AC-SIM-016, AC-SIM-043 |

#### S2-5: FSM S1 실 검증 + CONTINUOUS 수정 (0.5일) — HI-001, MD-004

| 작업 | 파일 | 내용 |
|------|------|------|
| 골든 모델 | PanelFsmModel.cpp | S1에서 power_good 입력 확인, CONTINUOUS 시 S1 스킵 |
| 테스트 | test_panel_fsm.cpp | power_good=0 → ERROR, CONTINUOUS 3회 반복 검증 |
| AC 대상 | AC-SIM-002, AC-SIM-025, AC-SIM-045 |

#### S2-6: RegBankModel 폭 정렬 + PanelInteg settle (0.5일) — HI-008, MD-001

| 작업 | 파일 | 내용 |
|------|------|------|
| 골든 모델 | RegBankModel.cpp | regs_ uint32→uint16 마스킹 (0xFFFF) |
| 골든 모델 | PanelIntegModel.cpp | cfg_tsettle 파라미터 수용 |
| 테스트 | test_reg_bank.cpp | 32비트 쓰기 → 16비트 truncation 검증 |

#### S2-7: GateNv1047 CLK 검증 + RowScan 타이밍 (0.5일) — MD-002

| 작업 | 파일 | 내용 |
|------|------|------|
| 골든 모델 | GateNv1047Model.cpp | cfg_clk_period ≥ 500 (200kHz) 검증 + warning |
| 골든 모델 | RowScanModel.cpp | cfg_tgate_on/settle 타이밍 실적용 (1사이클 → 카운터) |
| 테스트 | test_gate_models.cpp | CLK period 경계값 assert |

#### S2-8: AFE AD711xx 타이밍 + SPI 데이지체인 기반 (0.5일)

| 작업 | 파일 | 내용 |
|------|------|------|
| 골든 모델 | AfeAd711xxModel.cpp | TLINE_MIN 검증 로직 (RTL과 동일) |
| 골든 모델 | AfeSpiMasterModel.cpp | 24칩 데이지체인 576비트 시뮬레이션 기본 |

#### Sprint 2 예상 성과

| 지표 | Sprint 1 후 | Sprint 2 후 |
|------|-------------|-------------|
| CRITICAL | 0 | 0 |
| HIGH | 8 | **0** |
| AC-SIM PASS | ~8 | **~22** |
| 골든 모델 완성도 | 73% | **~90%** |
| 테스트 LOC (C++) | ~550 | ~900 |

### 6.4 Sprint 3 — 테스트 실구현 + 통합 검증 (8일)

#### S3-1: C++ 단위 테스트 확장 (3일)

| 테스트 파일 | 현재 LOC | 목표 LOC | 추가 assert |
|------------|----------|----------|-------------|
| test_spi_model.cpp | 32 | 120 | 32레지스터 전수 R/W + Mode 0/3 + R/O 보호 |
| test_reg_bank.cpp | 37 | 150 | 전 레지스터 기본값 + combo별 분기 + TLINE_MIN |
| test_panel_fsm.cpp | 30 | 200 | 5모드 전수 + 12상태 전이 + 타임아웃 + ABORT |
| test_gate_models.cpp | 25 | 150 | SR 비트별 + break-before-make + STV 홀짝 + 6칩 |
| test_afe_models.cpp | 48 | 200 | AD71124/43 IFS + AFE2256 CIC + TLINE_MIN + 파이프라인 |
| test_csi2_model.cpp | 18 | 150 | FS/FE + RAW16 + CRC + ECC + 2/4레인 |
| test_radiog_model.cpp | 38 | 150 | 핸드셰이크 + 다크 프레임 4/64 + settle |
| test_data_path_models.cpp | 40 | 120 | FIFO 스트레스 + 핑퐁 스왑 + MCU IRQ |
| 기타 5개 | 107 | 300 | PLL lock + CDC + 긴급차단 + 전원 시퀀싱 + VectorIO |
| **합계** | **375** | **1,540** | **+1,165 LOC** |

AC 대상: AC-SIM-001~034 중 20건 이상 PASS 전환

#### S3-2: cocotb 테스트 실구현 (3일)

| 테스트 파일 | 현재 LOC | 목표 LOC | 내용 |
|------------|----------|----------|------|
| test_spi_slave.py | 20 | 200 | 32레지스터 R/W + 골든 모델 비교 (AC-SIM-017) |
| test_panel_fsm.py | 15 | 250 | STATIC/CONTINUOUS/TRIGGERED 모드 + 타임아웃 |
| test_gate_nv1047.py | 14 | 150 | SD1 시프트 시퀀스 + OE 타이밍 |
| test_afe_ad711xx.py | 15 | 200 | ACLK + SYNC + 256ch 출력 + TLINE 검증 |
| test_csi2_tx.py | 13 | 200 | 패킷 구조 + CRC + ECC + 레인 |
| test_safety.py | 13 | 150 | 긴급차단 + 전원 시퀀싱 + 타임아웃 |
| 기타 8개 | ~100 | 800 | 나머지 모듈 기능 검증 |
| **합계** | **208** | **1,950** | **+1,742 LOC** |

AC 대상: AC-SIM-017 (RTL-골든 비교) + 각 SPEC별 cocotb 검증

#### S3-3: Multi-AFE + 통합 (1일) — HI-004, MD-003, MD-005, MD-007

| 작업 | 파일 | 내용 |
|------|------|------|
| RTL | detector_core.sv | C6/C7 시 line_data_rx 12인스턴스 generate loop |
| 골든 모델 | LineBufModel.cpp | multi-AFE FIFO 스케일링 + overflow 플래그 |
| 테스트 | test_data_path_models.cpp | 3072 픽셀 정렬 검증 + FIFO 스트레스 |
| AC 대상 | AC-SIM-014, AC-SIM-021, AC-SIM-039 |

#### S3-4: 엣지케이스 8건 (1일)

| EC-ID | 테스트 파일 | 내용 |
|-------|------------|------|
| EC-SIM-001 | test_spi_model.cpp | SPI 내부 갱신 경합 |
| EC-SIM-002 | test_data_path_models.cpp | FIFO near-full 배압 |
| EC-SIM-003 | test_panel_fsm.cpp | ABORT 2사이클 IDLE |
| EC-SIM-004 | test_afe_models.cpp | 24칩 576비트 데이지체인 |
| EC-SIM-005 | test_csi2_model.cpp | LP↔HS <1µs |
| EC-SIM-006 | test_afe_models.cpp | 12-AFE SYNC ±31ns 스큐 |
| EC-SIM-007 | test_data_path_models.cpp | VGL 미인가 VGH 래치업 |
| EC-SIM-008 | test_reg_bank.cpp | C2 TLINE=2200 클램핑 |

#### Sprint 3 예상 성과

| 지표 | Sprint 2 후 | Sprint 3 후 |
|------|-------------|-------------|
| CRITICAL | 0 | 0 |
| HIGH | 0 | 0 |
| AC-SIM PASS | ~22 | **≥38 (80%+)** |
| C++ 테스트 LOC | ~900 | **~1,540** |
| cocotb 테스트 LOC | 208 | **~1,950** |
| 엣지케이스 PASS | 0/8 | **≥6/8** |
| 기능 커버리지 | ~68% | **≥80%** |

---

## 7. Sprint 완료 후 예상 상태

| 지표 | 현재 | Sprint 1 | Sprint 2 | Sprint 3 |
|------|------|----------|----------|----------|
| CRITICAL | 6 | **0** | 0 | 0 |
| HIGH | 8 | 8 | **0** | 0 |
| MEDIUM | 8 | 6 | 2 | **0** |
| AC-SIM PASS | 0/47 | ~8/47 | ~22/47 | **≥38/47** |
| RTL 완성도 | 75% | 82% | 88% | **92%** |
| 골든 모델 완성도 | 71% | 78% | 90% | **95%** |
| C++ 테스트 LOC | 375 | 550 | 900 | **1,540** |
| cocotb LOC | 208 | 208 | 208 | **1,950** |
| 기능 커버리지 | 68% | 72% | 78% | **≥80%** |

---

## 8. 리스크 및 의존성

| 리스크 | 영향 | 완화 |
|--------|------|------|
| NT39565D 데이터시트 STV 타이밍 불확실 | Sprint 1 지연 | 데이터시트 교차 검증 우선 |
| Multi-AFE 12인스턴스 BRAM 부족 | Sprint 3 차단 | BRAM 예산 사전 검증 (현재 6-8/50) |
| cocotb + xsim 호환성 | Sprint 3 지연 | Verilator 우선 사용 |
| ASan 동적 분석 미실행 | 런타임 결함 잔존 | VS IDE에서 수동 실행 병행 |

---

## 9. 이전 리뷰 대비 변화

| 항목 | v5 | v6 (현재) |
|------|-----|-----------|
| 빌드 검증 | 없음 | **TVR-001 통합 (PASS)** |
| 결함 수정 | 없음 | **2건 크래시 수정 (13/13 PASS)** |
| 개선 계획 | 우선순위 목록만 | **3 Sprint 상세 계획 (21일, +6,100 LOC)** |
| Sprint별 AC 목표 | 없음 | **S1: 8, S2: 22, S3: 38+** |
| 모듈별 Sprint 배정 | 없음 | **전 모듈 Sprint 매핑** |
| 파일별 작업 명세 | 없음 | **RTL/골든모델/테스트 3중 명세** |

---

## 10. 결론

**빌드와 기본 테스트는 PASS** (MSVC 0 에러, CTest 13/13). **CRITICAL 6건은 Sprint 1 (5일)에 전수 해결 가능**. 가장 큰 갭은 **테스트 실구현** (C++ 375→1,540 LOC, cocotb 208→1,950 LOC)이며, Sprint 3 완료 시 AC-SIM PASS ≥80%, 기능 커버리지 ≥80% 달성.

**3 Sprint (21일) 실행으로 CRITICAL 0, HIGH 0, AC PASS ≥80% 도달.**

---

*Generated by MoAI Code Review Pipeline v6*
*SPEC: SPEC-FPD-SIM-001 v1.2.0 | Build: TVR-001 PASS | Tests: 13/13 PASS*
*Improvement Plan: 3 Sprints, 21 days, +6,100 LOC*
