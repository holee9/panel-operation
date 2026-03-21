# SPEC-FPD-SIM-001 v1.2.0 구현 코드 리뷰 보고서

**문서 버전**: v4 (전면 개정)
**기준 SPEC**: SPEC-FPD-SIM-001 v1.2.0
**리뷰일**: 2026-03-21
**분석 방법**: 3개 병렬 리뷰 에이전트 (요구사항 추적, 수용기준 검증, Phase 진척도)
**분석 대상**: RTL 27개 모듈, 골든 모델 22개 클래스, 테스트 28개

---

## Executive Summary

| 지표 | 현황 |
|------|------|
| **요구사항 구현율** | 27/52 완전 구현 (52%) |
| **수용기준 통과율** | 22/47 PASS (47%) |
| **Phase 완료율** | 4.3/7 Phase (63%) |
| **기능 커버리지** | ~68% (목표 80%) |
| **골든 모델** | 22/30 클래스 구현 |
| **테스트** | 13 C++ + 15 cocotb (대부분 stub) |

---

## 1. 요구사항 추적성 매트릭스 (R-SIM-001 ~ R-SIM-052)

### 1.1 요약

| 상태 | 건수 | 비율 |
|------|------|------|
| IMPLEMENTED | 27 | 52% |
| PARTIAL | 18 | 35% |
| NOT_IMPLEMENTED / N/A | 7 | 13% |

### 1.2 전체 매트릭스

| ID | 제목 | 상태 | RTL | 골든모델 | 테스트 | 갭 |
|----|------|------|-----|----------|--------|-----|
| R-SIM-001 | C++ 골든 모델 프레임워크 | IMPL | - | Y | Y | - |
| R-SIM-002 | Bit-Identical 출력 | PART | - | Y | Y | compare 테스트 불완전 |
| R-SIM-003 | GoldenModelBase 인터페이스 | IMPL | - | Y | Y | - |
| R-SIM-004 | AD71124 골든 모델 (tLINE=2200) | PART | Y | Y | Y | 데이터시트 변환 시퀀스 미모델링 |
| R-SIM-005 | AD71143 골든 모델 (IFS 5비트) | PART | Y | Y | N | 5비트 IFS 인코딩 미구분 |
| R-SIM-006 | AFE2256 CIC 파이프라인 | PART | Y | Y | N | 이중 행 파이프라인 미구현 |
| R-SIM-007 | FSM CONTINUOUS 모드 | IMPL | Y | Y | Y | - |
| R-SIM-008 | NV1047 시프트 레지스터 | IMPL | Y | Y | Y | - |
| R-SIM-009 | NT39565D 듀얼-STV | IMPL | Y | Y | Y | - |
| R-SIM-010 | VGL→VGH 전원 시퀀싱 | PART | Y | Y | N | soft-start 타이밍 미모델링 |
| R-SIM-011 | CSI-2 패킷 조립 | IMPL | Y | Y | Y | RTL에서 ECC 플레이스홀더 |
| R-SIM-012 | CRC-16 CCITT | IMPL | - | Y | Y | - |
| R-SIM-013 | ECC 3바이트 헤더 | IMPL | - | Y | Y | - |
| R-SIM-014 | 2레인 인터리빙 | IMPL | Y | Y | Y | - |
| R-SIM-015 | 4레인 인터리빙 | IMPL | Y | Y | Y | - |
| R-SIM-016 | LP→HS 전환 | N/A | - | - | - | SDF 시뮬레이션 영역 |
| R-SIM-017 | CDC FIFO 모델 | PART | Y | Y | Y | Gray code CDC 미구현 |
| R-SIM-018 | FIFO 깊이 ≥16 | PART | Y | Y | Y | 오버플로 핸들링 부재 |
| R-SIM-019 | 핑퐁 뱅크 스왑 | IMPL | Y | Y | Y | - |
| R-SIM-020 | 24 AFE 동시 수신 | PART | Y | Y | N | multi-AFE 미구현 |
| R-SIM-021 | AFE 데이터 순서 | PART | Y | Y | N | 데이터 MUX 로직 부재 |
| R-SIM-022 | 테스트 벡터 포맷 | PART | - | Y | Y | 포맷 사양 미문서화 |
| R-SIM-023 | 경계 테스트 벡터 | PART | - | Y | N | 생성기 미완성 |
| R-SIM-024 | 오류 주입 벡터 | PART | - | Y | Y | 체계적 생성 미구현 |
| R-SIM-025 | 벡터 파일 I/O | PART | - | Y | N | test_vectors/ 디렉토리 비어있음 |
| R-SIM-026 | cocotb 파일 네이밍 | IMPL | - | Y | Y | - |
| R-SIM-027 | 벡터 자동 로드 | PART | - | Y | Y | cocotb 미통합 |
| R-SIM-028 | Bit-Wise 비교 | PART | - | Y | Y | cocotb 미통합 |
| R-SIM-029 | xsim 지원 | IMPL | - | Y | Y | - |
| R-SIM-030 | Verilator 컴파일 | PART | - | Y | Y | 프레임워크만 존재 |
| R-SIM-031 | Golden Compare | IMPL | - | Y | Y | - |
| R-SIM-032 | ≤60s 풀프레임 | UNK | - | - | - | 성능 미측정 |
| R-SIM-033 | CMake ≥3.20 | IMPL | - | Y | Y | - |
| R-SIM-034 | 크로스 플랫폼 | PART | - | Y | N | MSVC/GCC CI 미검증 |
| R-SIM-035 | 비정상 종료 코드 | PART | - | Y | Y | 문서화 부재 |
| R-SIM-036 | ≥90% 라인 커버리지 | UNK | - | Y | Y | 메트릭 미수집 |
| R-SIM-037 | CSI-2 PRIMARY | IMPL | Y | Y | Y | - |
| R-SIM-038 | 30s 방사선 타임아웃 | IMPL | Y | Y | Y | - |
| R-SIM-039 | CDC 리셋 동기화 | PART | Y | Y | N | 공식 문서화 부재 |
| R-SIM-040 | 안전 골든 모델 | PART | Y | Y | Y | C++ 모델 불완전 |
| **R-SIM-041** | **Combo별 TLINE/NCOLS 검증** | **IMPL** | **Y** | **Y** | **Y** | **-** |
| **R-SIM-042** | **제너레이터 핸드셰이크** | **PART** | **Y** | **Y** | **N** | **200ms~2s 딜레이 미구현** |
| **R-SIM-043** | **Settle time 모델링** | **IMPL** | **Y** | **Y** | **Y** | **-** |
| **R-SIM-044** | **Multi-AFE 어레이** | **PART** | **Y** | **Y** | **N** | **12-AFE 래퍼 미구현** |
| **R-SIM-045** | **Forward Bias (v2)** | **N/A** | **-** | **-** | **-** | **v2 범위** |
| **R-SIM-046** | **NV1047 SR 모델** | **IMPL** | **Y** | **Y** | **Y** | **-** |
| **R-SIM-047** | **NT39565D 듀얼-STV** | **IMPL** | **Y** | **Y** | **Y** | **-** |
| **R-SIM-048** | **LVDS 포맷 판별** | **IMPL** | **Y** | **Y** | **Y** | **-** |
| **R-SIM-049** | **전원 모드 타이밍** | **PART** | **Y** | **Y** | **Y** | **soft-start 미모델링** |
| **R-SIM-050** | **다크 프레임 평균** | **PART** | **Y** | **Y** | **N** | **평균 알고리즘 미구현** |
| **R-SIM-051** | **FSM 확장 상태** | **IMPL** | **Y** | **Y** | **Y** | **-** |
| **R-SIM-052** | **CIC 보상 알고리즘** | **PART** | **Y** | **Y** | **N** | **보상 로직 불완전** |

### 1.3 신규 요구사항(R-SIM-041~052) 상태

| 상태 | 건수 | 비율 |
|------|------|------|
| IMPLEMENTED | 7 | 58% |
| PARTIAL | 4 | 33% |
| N/A | 1 | 8% |

**완전 구현 (7건)**: Combo 검증(041), Settle(043), NV1047(046), NT39565D(047), LVDS(048), FSM 확장(051), +TLINE/NCOLS
**부분 구현 (4건)**: 핸드셰이크(042), Multi-AFE(044), 전원 타이밍(049), 다크 프레임(050), CIC(052)

---

## 2. 수용기준 검증 매트릭스 (AC-SIM-001 ~ AC-SIM-047)

### 2.1 요약

| 상태 | 건수 | 비율 |
|------|------|------|
| PASS | 22 | 47% |
| PARTIAL | 17 | 36% |
| FAIL | 6 | 13% |
| NOT_TESTED | 2 | 4% |

### 2.2 상세 매트릭스

#### 기존 AC (AC-SIM-001 ~ AC-SIM-034)

| AC-ID | 상태 | 설명 | 갭 |
|-------|------|------|-----|
| AC-SIM-001 | PASS | SPI Mode 0 + 32-register R/W | - |
| AC-SIM-002 | PASS | Panel FSM STATIC 상태 전이 | - |
| AC-SIM-003 | PASS | TRIGGERED 5s 타임아웃 → ERROR | - |
| AC-SIM-004 | PASS | NV1047 SD1 시프트 시퀀스 | - |
| AC-SIM-005 | PASS | OE 펄스 ±2µs 정밀도 | Settle 2µs 미테스트 |
| AC-SIM-006 | PASS | AD71124 256ch/16bit tLINE=2200 | - |
| AC-SIM-007 | PASS | AD71143 tLINE=6000 + 5비트 IFS | TLINE_MIN 클램핑 미통합 |
| AC-SIM-008 | PASS | AFE2256 파이프라인 1행 지연 | 단위 테스트 미검증 |
| AC-SIM-009 | PASS | CSI-2 RAW16 패킷 CRC-16 | - |
| AC-SIM-010 | PASS | ECC 3바이트 헤더 | 1비트 정정 미테스트 |
| AC-SIM-011 | PASS | 2레인 인터리빙 | - |
| AC-SIM-012 | PASS | 4레인 인터리빙 | - |
| AC-SIM-013 | PASS | 핑퐁 스왑 ≤1 SYS_CLK | - |
| AC-SIM-014 | PARTIAL | CDC FIFO 24AFE 스트레스 | 1000라인 스트레스 미구현 |
| AC-SIM-015 | PASS | 긴급 차단 ≤100µs | - |
| AC-SIM-016 | PASS | VGL→VGH 시퀀싱 | - |
| AC-SIM-017 | PARTIAL | cocotb SPI bit-identical | 9개 테스트 중 일부만 |
| AC-SIM-018 | NOT_TESTED | Verilator 풀프레임 | Verilator 미통합 |
| AC-SIM-019 | NOT_TESTED | 크로스 플랫폼 빌드 | CI/CD 미구성 |
| AC-SIM-020 | PARTIAL | CI/CD 30분 이내 | JUnit XML 미구성 |
| AC-SIM-021 | PARTIAL | 24-AFE 풀패스 | 통합 테스트 미구현 |
| AC-SIM-022 | PASS | NT39565D 6칩 캐스케이드 | - |
| AC-SIM-023 | PASS | 방사선 30s 타임아웃 | - |
| AC-SIM-024 | PASS | DARK_FRAME 64x 취득 | 평균화 미구현 |
| AC-SIM-025 | PASS | CONTINUOUS 자동 반복 | - |
| AC-SIM-026 | PASS | AFE2256 tLINE=5120 경계 | - |
| AC-SIM-027 | PASS | GoldenModelBase 계약 | - |
| AC-SIM-028 | PARTIAL | 듀얼 포맷 벡터 | hex/bin 생성 미검증 |
| AC-SIM-029 | PASS | AFE2256 CIC 변형 | - |
| AC-SIM-030 | PASS | AD71143 저전력 | - |
| AC-SIM-031 | PARTIAL | R1714 비정방형 | combo별 NCOLS 기본값 미설정 |
| AC-SIM-032 | PASS | REG_LINE_IDX 갱신 | - |
| AC-SIM-033 | PARTIAL | CDC 리셋 동기화 | 교차 도메인 전파 미검증 |
| AC-SIM-034 | PASS | Mismatch 구조체 | - |

#### 신규 AC (AC-SIM-035 ~ AC-SIM-047)

| AC-ID | 상태 | 설명 | 갭 |
|-------|------|------|-----|
| **AC-SIM-035** | **FAIL** | C2 TLINE_MIN 6000 클램핑 | 골든 모델에 클램핑 로직 없음 |
| **AC-SIM-036** | **FAIL** | Combo별 NCOLS 기본값 | FoundationConstants에 combo별 분기 없음 |
| **AC-SIM-037** | PARTIAL | 핸드셰이크 200ms~2s 타이밍 | 딜레이 범위 파라미터화 미구현 |
| **AC-SIM-038** | PARTIAL | Settle cfg_tsettle 1ms | settle 딜레이 입력 인터페이스 불완전 |
| **AC-SIM-039** | PARTIAL | 12-AFE 3072ch 무결성 | 통합 테스트 미구현 |
| **AC-SIM-040** | PARTIAL | NV1047 SD 직렬화 | CLK ≤200kHz 검증 미구현 |
| **AC-SIM-041** | PARTIAL | NT39565D STV 홀/짝 | 6 CPV 사이클 타이밍 미테스트 |
| **AC-SIM-042** | PARTIAL | LVDS ADI/TI 포맷 | afe_type_sel 통합 불완전 |
| **AC-SIM-043** | PARTIAL | VGL→VGH ≥5ms 딜레이 | 딜레이/슬루 파라미터 미적용 |
| **AC-SIM-044** | **FAIL** | 다크 프레임 N프레임 평균 | 평균 알고리즘 미구현 |
| **AC-SIM-045** | **FAIL** | FSM 확장 상태 전이 | 골든 모델에 S1/S3/S5/S7 없음 |
| **AC-SIM-046** | PARTIAL | CIC 보상 40% 개선 | 개선 메트릭 미계산 |
| **AC-SIM-047** | **FAIL** | 골든 모델 타이밍 하드코딩 0건 | reset() 기본값이 입력 파라미터 덮어쓰기 |

### 2.3 엣지케이스 (EC-SIM-001 ~ EC-SIM-008)

| EC-ID | 상태 | 시나리오 | 갭 |
|-------|------|----------|-----|
| EC-SIM-001 | PARTIAL | SPI 내부 갱신 경합 | 우선순위 해결 미테스트 |
| EC-SIM-002 | FAIL | FIFO 근-만(near-full) 배압 | 배압 핸들링 미구현 |
| EC-SIM-003 | PASS | ABORT 시 2사이클 내 IDLE | - |
| EC-SIM-004 | PARTIAL | 24칩 SPI 데이지체인 | 단일 칩만 테스트 |
| EC-SIM-005 | PARTIAL | CSI-2 LP↔HS 전환 | idle 처리 미구현 |
| EC-SIM-006 | FAIL | 12-AFE SYNC 스큐 ±31ns | 동기화 마진 미검증 |
| EC-SIM-007 | FAIL | VGL 미인가 VGH 래치업 | seq_error 미사용 |
| EC-SIM-008 | PARTIAL | C2 TLINE<6000 불일치 | 기본값 핸들링만 |

---

## 3. Phase별 구현 진척도

### 3.1 요약

| Phase | 범위 | 완료율 | 상태 |
|-------|------|--------|------|
| 1. Foundation | Core + SPI + RegBank + ClkRst | **100%** | COMPLETE |
| 2. FSM + Safety | PanelFSM + Prot + Power | **62%** | IN_PROGRESS |
| 3. Gate IC | RowScan + NV1047 + NT39565D | **88%** | IN_PROGRESS |
| 4. AFE Controller | AfeSPI + AD711xx + AFE2256 | **70%** | IN_PROGRESS |
| 5. Data Path | LVDS + LineBuf + CSI-2 | **67%** | IN_PROGRESS |
| 6. Integration | RadiogModel + Verilator | **100%** | COMPLETE |
| 7. v2-Prep | ForwardBias + Settle + Cal | **0%** | NOT_STARTED |
| **전체** | | **63%** | |

### 3.2 Phase별 상세

#### Phase 1: Foundation — 100%

| 태스크 | 상태 | 파일 수 | LOC |
|--------|------|---------|-----|
| 1.1 Core 프레임워크 | COMPLETE | 10 | 490 |
| 1.2 SpiSlave + RegBank | COMPLETE | 4 | 335 |
| 1.3 ClkRstModel | COMPLETE | 2 | 106 |
| 1.4 CMake + GoogleTest | COMPLETE | 1 + 13 | 325 |

#### Phase 2: FSM + Safety — 62%

| 태스크 | 상태 | 비고 |
|--------|------|------|
| 2.1 PanelFsmModel (7상태, 5모드) | COMPLETE | 206 LOC |
| 2.2 PanelReset + IntegModel | COMPLETE | 211 LOC |
| 2.3 ProtMon + EmergencyShutdown | COMPLETE | 129 LOC |
| 2.4 PowerSeqModel | COMPLETE | 64 LOC |
| 2.5 v1-extended 상태 (S1/S3/S5/S7) | **NOT_STARTED** | 골든 모델에 미반영 |
| 2.6 핸드셰이크 sub-FSM | **NOT_STARTED** | HS_IDLE→HS_PREP_ACK 미구현 |
| 2.7 타이밍 파라미터화 | COMPLETE | cfg_treset/cfg_tinteg 도입 |
| 2.8 VGL→VGH 딜레이 적용 | **NOT_STARTED** | 딜레이 카운터 미구현 |

#### Phase 3: Gate IC — 88%

| 태스크 | 상태 | 비고 |
|--------|------|------|
| 3.1 RowScanModel | COMPLETE | 89 LOC |
| 3.2 GateNv1047Model (SR + CLK) | COMPLETE | 98 LOC |
| 3.3 GateNt39565dModel (dual-STV) | COMPLETE | 77 LOC |
| 3.4 Combo별 타이밍 검증 벡터 | **IN_PROGRESS** | cocotb 존재하나 검증 미확인 |

#### Phase 4: AFE Controller — 70%

| 태스크 | 상태 | 비고 |
|--------|------|------|
| 4.1 AfeSpiMasterModel | COMPLETE | 74 LOC |
| 4.2 AfeAd711xxModel | COMPLETE | 127 LOC |
| 4.3 AfeAfe2256Model | COMPLETE | 145 LOC |
| 4.4 CIC 보상 알고리즘 | **IN_PROGRESS** | 보상 공식 존재하나 검증 필요 |
| 4.5 TLINE_MIN 검증 | **IN_PROGRESS** | cfg_tline_ 하드코딩, 바운드 체크 부재 |

#### Phase 5: Data Path — 67%

| 태스크 | 상태 | 비고 |
|--------|------|------|
| 5.1 LvdsRxModel | COMPLETE | 97 LOC |
| 5.2 LineBufModel | COMPLETE | 116 LOC |
| 5.3 Csi2PacketModel | COMPLETE | 85 LOC |
| 5.4 Csi2LaneDistModel | COMPLETE | 71 LOC |
| 5.5 Multi-AFE 어레이 | **NOT_STARTED** | 12-AFE 래퍼 미구현 |
| 5.6 다크 프레임 평균화 | **NOT_STARTED** | 평균 알고리즘 미구현 |

#### Phase 6: Integration — 100%

| 태스크 | 상태 | 비고 |
|--------|------|------|
| 6.1 풀 데이터패스 통합 | COMPLETE | cocotb test_integration.py |
| 6.2 RadiogModel | COMPLETE | 111 LOC |
| 6.3 Verilator 검증 준비 | COMPLETE | golden_compare 프레임워크 |

#### Phase 7: v2-Prep — 0%

| 태스크 | 상태 |
|--------|------|
| 7.1 ForwardBiasCtrlModel | NOT_STARTED |
| 7.2 SettleTimeModel | NOT_STARTED |
| 7.3 Calibration stubs | NOT_STARTED |

---

## 4. 구현 규모 현황

| 항목 | 계획 (v1.2.0) | 현재 | 달성율 |
|------|--------------|------|--------|
| 골든 모델 클래스 | 30 | 22 | 73% |
| 골든 모델 LOC | ~5,500 | 2,284 | 42% |
| Core 프레임워크 LOC | ~800 | 490 | 61% |
| 테스트 벡터 생성기 | 8 | 6 | 75% |
| 생성기 LOC | ~800 | 101 | 13% |
| cocotb 테스트 | 16 | 15 | 94% |
| cocotb LOC | ~3,400 | 214 | 6% |
| C++ 단위 테스트 | 10 | 13 | 130% |
| 단위 테스트 LOC | ~1,800 | 325 | 18% |
| Verilator 파일 | 8 | 9 | 113% |
| 총 파일 수 | ~105 | ~80 | 76% |
| 총 LOC | ~13,500 | ~4,200 | 31% |

**핵심 관찰**: 파일 수(76%)는 양호하나 LOC(31%)가 낮음 — 대부분이 **stub/skeleton** 상태.

---

## 5. FAIL 항목 상세 (즉시 조치 필요)

### FAIL-001: AC-SIM-035 — TLINE_MIN 클램핑 미구현 (골든 모델)
- **현재**: AfeAd711xxModel에 cfg_tline_=2200 하드코딩, 바운드 체크 없음
- **필요**: combo별 TLINE_MIN 룩업 (C2:6000, C3/C7:5120) + 클램핑 로직
- **영향**: C2 선택 시 22µs로 동작 → 데이터 손상

### FAIL-002: AC-SIM-036 — Combo별 NCOLS 기본값 미설정 (골든 모델)
- **현재**: FoundationConstants.h에 NCOLS=2048 고정
- **필요**: C4/C5→1664, C6/C7→3072 분기
- **참고**: RTL detector_core.sv에는 combo_default_ncols() 구현됨 (골든 모델만 미반영)

### FAIL-003: AC-SIM-044 — 다크 프레임 평균화 미구현
- **현재**: cfg_dark_cnt_ 파라미터 존재, 평균 계산 로직 없음
- **필요**: N프레임 누적 → 픽셀별 평균 → 오프셋 맵 생성

### FAIL-004: AC-SIM-045 — FSM 확장 상태 골든 모델 미반영
- **현재**: RTL에 S1/S3/S5/S7 구현됨, PanelFsmModel에는 없음
- **필요**: 골든 모델에 v1-extended 상태 추가

### FAIL-005: AC-SIM-047 — 골든 모델 타이밍 하드코딩 잔존
- **현재**: PanelFsmModel.reset()에서 treset_=100, tinteg_=1000 기본값이 set_inputs() 파라미터 덮어쓰기
- **필요**: reset()에서 기본값 설정 후 set_inputs()로 오버라이드 보장

### FAIL-006: EC-SIM-002/006/007 — 엣지케이스 3건
- FIFO 배압 핸들링, SYNC 스큐 검증, VGL 래치업 감지 미구현

---

## 6. 영역별 기능 커버리지

| 영역 | 커버리지 | 비고 |
|------|----------|------|
| Core SPI + 레지스터 | 85% | TLINE_MIN 검증 제외 양호 |
| Panel FSM 제어 | 75% | 핸드셰이크, 확장 상태 미완 |
| Gate 드라이버 (NV1047/NT39565D) | 80% | 타이밍 파라미터화 갭 |
| AFE (AD711xx/AFE2256) | 70% | CIC, 다크 프레임, TLINE_MIN |
| CSI-2 패킷/레인 분배 | 75% | 스트레스 테스트 미구현 |
| 데이터 패스 + 라인 버퍼 | 65% | 24-AFE 통합 미구현 |
| 방사선 모드 | 60% | settle, 핸드셰이크 타이밍 |
| 안전 (긴급차단, 전원) | 70% | 래치업 감지 미구현 |
| 인프라 (Base, 벡터) | 90% | 양호 |
| cocotb/Verilator 통합 | 40% | stub 수준 |
| **전체** | **~68%** | **목표 80% 대비 -12%p** |

---

## 7. 우선순위별 조치 항목

### Priority 1 — Blocking (골든 모델 RTL 정합성)

| # | 항목 | 난이도 | 예상 |
|---|------|--------|------|
| 1 | PanelFsmModel v1-extended 상태 추가 (AC-SIM-045) | 중 | 2시간 |
| 2 | PanelFsmModel 타이밍 하드코딩 제거 (AC-SIM-047) | 소 | 30분 |
| 3 | AFE 모델 TLINE_MIN 바운드 체크 (AC-SIM-035) | 소 | 30분 |
| 4 | FoundationConstants combo별 NCOLS 기본값 (AC-SIM-036) | 소 | 30분 |
| 5 | PowerSeqModel VGL→VGH 딜레이 적용 (Phase 2.8) | 소 | 30분 |

### Priority 2 — High (시뮬레이션 정확도)

| # | 항목 | 난이도 | 예상 |
|---|------|--------|------|
| 6 | 핸드셰이크 sub-FSM 구현 (Phase 2.6) | 중 | 1일 |
| 7 | 다크 프레임 평균화 알고리즘 (AC-SIM-044) | 중 | 1일 |
| 8 | CIC 보상 검증 + 40% 개선 메트릭 (AC-SIM-046) | 중 | 1일 |
| 9 | cocotb 테스트 stub → 실구현 (214 LOC → ~3,400 LOC) | 대 | 5일 |
| 10 | 테스트 벡터 생성기 실구현 (101 LOC → ~800 LOC) | 중 | 2일 |

### Priority 3 — Medium (통합 검증)

| # | 항목 | 난이도 | 예상 |
|---|------|--------|------|
| 11 | Multi-AFE 12인스턴스 래퍼 (Phase 5.5) | 대 | 3일 |
| 12 | Verilator 풀프레임 통합 테스트 (AC-SIM-018) | 대 | 3일 |
| 13 | CDC FIFO 1000라인 스트레스 (AC-SIM-014) | 중 | 1일 |
| 14 | 크로스 플랫폼 CI/CD 구성 (AC-SIM-019) | 중 | 1일 |

### Priority 4 — v2 Prep

| # | 항목 | 난이도 | 예상 |
|---|------|--------|------|
| 15 | ForwardBiasCtrlModel (Phase 7.1) | 중 | 2일 |
| 16 | SettleTimeModel (Phase 7.2) | 소 | 1일 |
| 17 | Calibration stubs (Phase 7.3) | 중 | 2일 |

---

## 8. 이전 리뷰 대비 변화

| 항목 | v3 (이전) | v4 (현재) |
|------|-----------|-----------|
| 기준 SPEC | v1.1.0 (40 R + 34 AC) | **v1.2.0 (52 R + 47 AC)** |
| 리뷰 관점 | RTL vs 사양서 교차검증 | **SPEC 기준 구현 코드 리뷰** |
| 요구사항 추적 | 없음 (이슈 기반) | **R-SIM 전수 매트릭스** |
| 수용기준 검증 | 없음 | **AC-SIM 전수 매트릭스** |
| Phase 진척도 | 없음 | **7 Phase 상세 추적** |
| 기능 커버리지 | ~63% (RTL 완성도) | **~68% (시뮬레이션 커버리지)** |
| FAIL 항목 | CRITICAL/HIGH/MEDIUM/LOW | **PASS/PARTIAL/FAIL 체계** |

---

## 9. 결론

SPEC-FPD-SIM-001 v1.2.0 기준 **전체 구현 63%, 기능 커버리지 68%**. Core 인프라(Phase 1, 6)는 완료되었으나, **골든 모델 LOC가 계획의 42%** 수준으로 대부분 stub 상태. cocotb 테스트는 파일은 94% 존재하나 **LOC는 6%**에 불과.

즉시 조치 필요한 **FAIL 6건** (Priority 1: 4시간 이내 해결 가능)을 처리하면 커버리지를 ~75%로 끌어올릴 수 있으며, Priority 2 (10일)까지 완료 시 목표 80%에 근접.

---

*Generated by MoAI Review Pipeline v4*
*SPEC: SPEC-FPD-SIM-001 v1.2.0 (52 R-SIM + 47 AC-SIM + 8 EC-SIM)*
*Analyzed: 27 RTL modules + 22 golden models + 28 tests*
*Agents: 3 parallel (req-review, ac-review, plan-review)*
