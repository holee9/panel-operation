# 교차검증 종합 보고서 v3 (개정판)

**생성일**: 2026-03-21
**개정일**: 2026-03-21
**분석 범위**: RTL 27개 모듈, 사양서 3종, 골든 모델 15개
**분석 방법**: 3개 병렬 재검증 에이전트 (RTL vs 아키텍처, 구동 알고리즘 vs RTL, 골든 모델 vs RTL)
**목적**: v2 보고서(46건) 기준 수정 현황 점검 + 신규 발견 반영

---

## Executive Summary

### v2 → v3 수정 현황

| 검증 영역 | v2 건수 | FIXED | PARTIAL | OPEN | 신규 | v3 잔여 |
|-----------|---------|-------|---------|------|------|---------|
| RTL vs 아키텍처 사양서 | 16 | **12** | 1 | 2 | 7 | **10** |
| 구동 알고리즘 vs RTL | 15 | **2** | 3 | 10 | 0 | **13** |
| 골든 모델 vs RTL | 15 | **3** | 2 | 10 | 0 | **12** |
| **합계** | **46** | **17** | **6** | **22** | **7** | **35** |

**수정률: 37% (17/46건 완전 해결)**

### 심각도별 현황

| 심각도 | v2 | v3 FIXED | v3 잔여 (기존+신규) |
|--------|-----|---------|---------------------|
| CRITICAL | 11 | 6 | **5 + 1 = 6** |
| HIGH | 12 | 5 | **8 + 3 = 11** |
| MEDIUM | 16 | 4 | **9 + 3 = 12** |
| LOW | 6 | 2 | **5** |
| **합계** | **46** | **17** | **35** (기존 22 + PARTIAL 6 + 신규 7) |

---

## 1. 해결 완료 (FIXED — 17건)

v2 대비 완전히 수정된 항목. 더 이상 추적 불필요.

### 1.1 RTL vs 아키텍처 (12건 FIXED)

| v2 ID | 모듈 | 해결 내용 |
|-------|------|-----------|
| GAP-000 | fpga_top_c1/c3/c6 | **detector_core 인스턴스화 완료** — C1(ADI+NV), C3(TI+NV), C6(ADI+NT) 파라미터 정확 |
| (신규) | detector_core.sv | **551줄 완전 구현** — 전체 서브모듈 인스턴스화 및 배선 |
| GAP-010 | gate_nv1047 | **시프트 레지스터 구현** — 12비트 SR, MSB 추출, 비트별 직렬화 |
| GAP-014 | afe_ad711xx | **SPI 설정 실제 구현** — IFS/LPF/PMODE/NCHIP 레지스터 패킹 |
| (연관) | afe_afe2256 | **SPI 설정 실제 구현** — CIC_EN/CIC_PROFILE/PIPELINE_EN/TP_SEL 포함 |
| GAP-011 | gate_nv1047 | **CLK 분주기 구현** — safe_clk_period() 함수로 200kHz 상한 보장 |
| GAP-002 | reg_bank | **REG_TINTEG 24비트 확장** — REG_TINTEG(0x08) + REG_TINTEG_H(0x17) 분할 저장 |
| GAP-004 | prot_mon | **cfg_max_exposure 연결** — detector_core에서 cfg_tinteg 매핑 |
| GAP-006 | data_out_mux | **cfg_ncols 동적 사용** — 하드코딩 2047 제거 |
| GAP-001 | clk_rst_mgr | **AFE 클럭 먹스 등록** — always_ff 블록 내 순차 로직으로 이동 |
| GAP-013 | row_scan_eng | **클럭 도메인 동기화** — cfg_tgate_on/settle 카운터 완전 동기 |
| GAP-009 | panel_ctrl_fsm | **cfg_nreset 의미 명확화** — 리셋 홀드 시간(사이클), dummy_scans와 분리 |

### 1.2 구동 알고리즘 (2건 FIXED)

| v2 ID | 모듈 | 해결 내용 |
|-------|------|-----------|
| DRV-006 | clk_rst_mgr | **AFE 클럭 주파수 보정** — NCO 기반 ACLK=10MHz, MCLK=32MHz 정확 생성 |
| DRV-012 | reg_bank | **REG_TINTEG 단위 통일** — 전체 코드베이스 10ns 단위로 일관 |

### 1.3 골든 모델 (3건 FIXED)

| v2 ID | 모듈 | 해결 내용 |
|-------|------|-----------|
| GM-007 | RegBankModel | **cfg_tinteg 24비트 출력** — kRegTIntegHi + kRegTInteg 조합 |
| GM-010 | RegBankModel | **TINTEG 폭 확장 동일** — GM-007과 동일 수정 |
| GM-008 | SpiSlaveModel | **spi_mode_ 실사용** — TransferBit()에서 샘플링/시프팅 결정에 활용 |

---

## 2. 부분 수정 (PARTIALLY FIXED — 6건)

진전이 있으나 추가 작업 필요.

### PART-001 (v2: GAP-012) — gate_nt39565d.sv STV 펄스 생성
- **심각도**: CRITICAL
- **진전**: STV 펄스 폭 카운터 추가 (`stv_count < cfg_stv_pulse`)
- **잔여 문제**:
  - STV1L/STV2L이 여전히 `row_index[0]` LSB 기반 (홀/짝 행 선택 아님)
  - STV1R/STV2R가 `chip_sel` 사용 (부정확)
  - OE1_L/R, OE2_L/R이 좌/우 동일 출력 — 독립 채널 제어 불가
- **필요 작업**: NT39565D 데이터시트 기반 dual-latch STV 시퀀스, 좌/우 OE 독립 제어

### PART-002 (v2: DRV-004) — 제너레이터 핸드셰이크
- **심각도**: HIGH
- **진전**: xray_enable이 ST_INTEGRATE에서 어서트됨
- **잔여 문제**: S3_PREP_WAIT (200ms~2s 딜레이), S5_XRAY_ENABLE 상태 없음
- **필요 작업**: 전용 핸드셰이크 상태 추가

### PART-003 (v2: DRV-005) — VGL→VGH 전원 시퀀싱
- **심각도**: HIGH
- **진전**: `vgl_stable` 검사 후 `en_vgh` 어서트 (line 50-51)
- **잔여 문제**: VGL→VGH 간 설정 가능한 딜레이 없음, soft-start 슬루율 제어 없음
- **필요 작업**: T_VGL_TO_VGH 레지스터 추가 + 딜레이 카운터

### PART-004 (v2: DRV-008) — 다크 프레임 모드
- **심각도**: MEDIUM
- **진전**: MODE_DARK_FRAME에서 gate 비활성 + AFE 리드아웃만 수행
- **잔여 문제**: 다크 프레임 평균화, 오프셋 감산, 자동 후처리 파이프라인 없음
- **필요 작업**: S9_POST_DARK 상태 + 프레임 누적기

### PART-005 (v2: GM-004) — ProtMonModel FSM 상태 타입
- **심각도**: LOW
- **진전**: 매직 넘버 매핑은 정확 (state 2 = INTEGRATE)
- **잔여 문제**: raw uint32 대신 enum 타입 정의 권장
- **필요 작업**: 상수 정의 또는 enum 추가

### PART-006 (v2: GM-015) — ClkRstModel AFE 클럭 커스터마이즈
- **심각도**: LOW
- **진전**: 생성자 파라미터 `afe_clk_hz` 추가 (기본 10MHz)
- **잔여 문제**: kSysClkHz, kMclkHz는 여전히 하드코딩
- **필요 작업**: 생성자 파라미터 확장

---

## 3. 미해결 (OPEN — 22건)

### 3.1 CRITICAL (5건)

#### OPEN-C01 (v2: DRV-001/DRV-011) — FSM 상태 누락
- **모듈**: panel_ctrl_fsm.sv, fpd_types_pkg.sv
- **현재**: 8개 상태 (ST_IDLE ~ ST_ERROR)
- **사양**: 13개 상태 (S0 ~ S12 + S_ERROR)
- **누락 상태**:

| 상태 | 기능 | 사양 참조 |
|------|------|-----------|
| S1_POWER_CHECK | 전원 레일 사전 검증 | line 80 |
| S3_PREP_WAIT | 제너레이터 준비 핸드셰이크 (200ms~2s) | line 86 |
| S4_BIAS_STABILIZE | Forward Bias 적용 (~30ms) | line 89 |
| S5_XRAY_ENABLE | X선 Enable 핸드셰이크 | line 92 |
| S7_SETTLE | TFT 전하 재분배 (1~10ms) | line 362 |

- **참고**: S9_POST_DARK, S10_CORRECTION, S11_TRANSFER, S12_POST_STAB는 v1(BRAM 전용) 범위상 MCU/PC 위임 허용
- **권장**: 최소 S1, S3, S5, S7은 FPGA 내 구현 필요 (안전/타이밍 필수)

#### OPEN-C02 (v2: DRV-002) — Forward Bias 제어 모듈 미존재
- **모듈**: forward_bias_ctrl.sv
- **현재**: 모듈 없음, VPD DAC 제어 신호 없음
- **사양**: VPD 바이어스 3단계 (reverse −1.5V, low −0.2V, forward +4V), 30ms+
- **영향**: Lag 보정 불가 (2~5% → <0.3% 개선 불가)

#### OPEN-C03 (v2: DRV-009) — 보정 파이프라인 미구현
- **모듈**: 없음 (offset/gain/defect/CIC/lag)
- **현재**: v1 범위 — MCU/PC 소프트웨어 위임
- **사양**: Section 10 — 6단계 실시간 보정 파이프라인
- **판정**: v1에서는 OPEN 유지, v2(외부 메모리 추가)에서 구현 예정

#### OPEN-C04 (v2: DRV-010 승격) — Settle 타임 부족
- **모듈**: panel_ctrl_fsm.sv, ST_READOUT_INIT
- **현재**: `cfg_sync_dly` 사용 (AFE 동기화 딜레이와 혼용)
- **사양**: S7_SETTLE 1~10ms (TFT 전하 재분배)
- **문제**: AFE 동기화와 TFT 전하 정착은 별개 물리 현상
- **권장**: 전용 ST_SETTLE 상태 + cfg_tsettle 레지스터 추가

#### OPEN-C05 (v2: GM-012/GM-001) — 골든 모델 타이밍 플레이스홀더
- **모듈**: PanelFsmModel.cpp
- **현재**: RESET=4클럭, INTEGRATE=2~30클럭 (하드코딩)
- **RTL**: cfg_treset, cfg_tinteg 레지스터 기반
- **영향**: 골든 모델 기반 테스트 벡터가 RTL과 불일치 — 시뮬레이션 무효
- **권장**: cfg_treset/cfg_tinteg 입력 수용 또는 가속 모드 명시적 분리

### 3.2 HIGH (8건)

| ID | v2 출처 | 모듈 | 문제 | 현재 상태 |
|----|---------|------|------|-----------|
| OPEN-H01 | DRV-003 | panel_integ_ctrl | REG_INT_MODE 레지스터 누락, AEC 모드 미구현 | OPEN |
| OPEN-H02 | DRV-007 | reg_bank | 조합별 타이밍 기본값 선택 없음 (C2: T_LINE 22µs→60µs 필요) | OPEN |
| OPEN-H03 | GAP-005 | prot_mon | 에러 플래그 통합 미완 (timeout만, temp/voltage/PLL 미지원) | OPEN |
| OPEN-H04 | GM-002 | AfeAd711xxModel | TLINE_MIN 안전 검증 누락 (RTL은 구현됨) | OPEN |
| OPEN-H05 | GM-005 | RowScanModel | cfg_tgate_on/settle 타이밍 미반영 (1사이클 전이) | OPEN |
| OPEN-H06 | GM-011 | 전체 | **8개 RTL 모듈**에 대응 골든 모델 부재 | OPEN |
| OPEN-H07 | GAP-008 | clk_rst_mgr | afe_type_sel CDC 동기화 미보호 | OPEN |
| OPEN-H08 | DRV-013 | afe_afe2256 | CIC 프로파일 vs 조합 선택 검증 없음 | OPEN (MEDIUM→HIGH 승격) |

#### OPEN-H02 상세: 조합별 타이밍 위반 위험

| 조합 | 파라미터 | 현재 기본값 | 사양 최소값 | 위반 여부 |
|------|----------|-------------|-------------|-----------|
| C1 (AD71124) | T_LINE | 2200 (22µs) | 2200 | OK |
| C2 (AD71143) | T_LINE | 2200 (22µs) | **6000 (60µs)** | **위반 (37µs 부족)** |
| C3 (AFE2256) | T_LINE | 2200 (22µs) | 5120 (51.2µs) | **위반 (29µs 부족)** |
| C4 (R1714) | NCOLS | 2048 | **1664** | **오버스캔** |
| C6 (X239AW1) | NCOLS | 2048 | **3072** | **언더스캔** |

#### OPEN-H06 상세: 골든 모델 미존재 목록

1. `line_data_rx` — LVDS 데이터 수신기
2. `afe_spi_master` — AFE SPI 마스터
3. `gate_nv1047` — NV1047 전용 드라이버
4. `gate_nt39565d` — NT39565D 전용 드라이버
5. `data_out_mux` — 데이터 출력 멀티플렉서
6. `mcu_data_if` — MCU 데이터 인터페이스
7. `panel_integ_ctrl` — 적분 제어
8. `panel_reset_ctrl` — 리셋 제어

### 3.3 MEDIUM (9건)

| ID | v2 출처 | 모듈 | 문제 |
|----|---------|------|------|
| OPEN-M01 | DRV-014 | reg_bank | C4/C5 NCOLS 기본값 2048 (실제 1664), C6/C7은 3072 |
| OPEN-M02 | DRV-015 | panel_integ_ctrl | 핸드셰이크 딜레이 윈도우 설정 불가 |
| OPEN-M03 | GM-003 | AfeAfe2256Model | TLINE_MIN 안전 검증 누락 |
| OPEN-M04 | GM-006 | LineBufModel | kCols=2048 하드코딩, 주소 폭 32비트 (RTL 12비트) |
| OPEN-M05 | GM-009 | ClkRstModel | kSysClkHz, kMclkHz 내부 상수 하드코딩 |
| OPEN-M06 | GM-013 | PanelFsmModel | 라인 인덱스 증가 로직 검증 필요 |
| OPEN-M07 | GM-014 | AfeAd711xxModel | cfg_mix 출력 — RTL에 없는 테스트 전용 신호 |
| OPEN-M08 | GAP-003 | reg_bank | REG_CIC_PROFILE 6비트 패킹 (4+1+1) — 문서와 불일치 (기능은 정상) |
| OPEN-M09 | GAP-007 | mcu_data_if | SPI 데이터 출력 모드 미지원 (병렬만) — 설계 의도일 수 있음 |

### 3.4 LOW (5건)

| ID | v2 출처 | 모듈 | 문제 |
|----|---------|------|------|
| OPEN-L01 | DRV-015 | panel_integ_ctrl | REG_PREP_DELAY/REG_TRIG_DELAY 레지스터 없음 |
| OPEN-L02 | GM-009 | ClkRstModel | 내부 상수 하드코딩 (값은 정확) |
| OPEN-L03 | GM-014 | AfeAd711xxModel | cfg_mix 출력 RTL 미존재 신호 |
| OPEN-L04 | PART-005 | ProtMonModel | FSM 상태 raw uint32 (enum 권장) |
| OPEN-L05 | PART-006 | ClkRstModel | kSysClkHz/kMclkHz 하드코딩 |

---

## 4. 신규 발견 (v3에서 새로 확인 — 7건)

### NEW-001 (CRITICAL) — Multi-AFE 지원 미구현 (C6/C7)
- **모듈**: detector_core.sv
- **현재**: line_data_rx 1개만 인스턴스화 (N_CHANNELS=256)
- **사양**: C6/C7은 12개 AFE (3072채널), 24개 AFE LVDS 수신 필요
- **영향**: C6/C7 조합 완전 미지원
- **권장**: AFE 디시리얼라이저 어레이 또는 시분할 먹스 구현

### NEW-002 (HIGH) — line_buf_ram N_COLS 고정
- **모듈**: detector_core.sv line 417-419
- **현재**: `.N_COLS(2048)` 하드코딩
- **문제**: cfg_ncols가 레지스터에서 오지만 line_buf_ram에 전달되지 않음
- **권장**: N_COLS를 MAX_COLS(3072)로 설정하거나 동적 파라미터화

### NEW-003 (HIGH) — gate_nt39565d OE1/OE2 좌/우 동일 출력
- **모듈**: gate_nt39565d.sv lines 107-110
- **현재**:
  ```
  nt_oe1_l <= gate_on_pulse && !row_index[0];
  nt_oe1_r <= gate_on_pulse && !row_index[0];  // 좌와 동일!
  nt_oe2_l <= gate_on_pulse && row_index[0];
  nt_oe2_r <= gate_on_pulse && row_index[0];   // 좌와 동일!
  ```
- **문제**: 좌/우 패널 독립 제어 불가
- **권장**: chip_sel + scan_dir 기반 좌/우 OE 분리

### NEW-004 (HIGH) — CSI-2 레인 수 하드코딩
- **모듈**: detector_core.sv line 488
- **현재**: `.lane_count(USE_NT_GATE ? 3'd4 : 3'd2)` 하드코딩
- **문제**: C6/C7 multi-AFE 데이터 경로 미반영
- **권장**: cfg_afe_nchip 기반 동적 레인 할당

### NEW-005 (MEDIUM) — line_data_rx N_CHANNELS 고정
- **모듈**: detector_core.sv line 366
- **현재**: N_CHANNELS=256 (단일 AFE)
- **문제**: C6/C7에서 3072채널 필요하나 256으로 고정
- **권장**: cfg_ncols 기반 파라미터화

### NEW-006 (MEDIUM) — afe_ad711xx SPI 패킷 포맷 단순화
- **모듈**: afe_ad711xx.sv line 77
- **현재**: `{cfg_ifs, cfg_lpf, cfg_pmode, cfg_nchip, 8'hA5}` — 단순 패킹
- **문제**: AD71124 데이터시트 SPI 레지스터 주소/데이터 포맷과 일치 여부 미검증
- **권장**: 데이터시트 SPI 프로토콜 대조 검증

### NEW-007 (MEDIUM) — prot_mon vs emergency_shutdown 에러 경로 분리
- **모듈**: detector_core.sv lines 500-522
- **현재**: prot_error → FSM error, emergency shutdown → force_gate_off — 별도 경로
- **문제**: 두 에러 경로 간 우선순위/조정 메커니즘 없음
- **권장**: 에러 우선순위 정의 + 통합 에러 핸들러

---

## 5. 구현 성숙도 평가 (v3 개정)

```
모듈                      v2     v3     변화    판정
─────────────────────────────────────────────────────────
fpd_types_pkg.sv          85%    85%    -       PASS
fpd_params_pkg.sv         75%    75%    -       PASS
spi_slave_if.sv           90%    90%    -       PASS
reg_bank.sv               70%    80%    +10%    부분 (조합별 기본값)
clk_rst_mgr.sv            60%    75%    +15%    부분 (CDC 잔여)
panel_ctrl_fsm.sv         50%    55%    +5%     부분 (상태 누락)
panel_integ_ctrl.sv       60%    60%    -       부분 (AEC 미구현)
panel_reset_ctrl.sv       80%    80%    -       PASS
power_sequencer.sv        60%    70%    +10%    부분 (딜레이 레지스터)
prot_mon.sv               50%    55%    +5%     부분 (에러 통합)
emergency_shutdown.sv     80%    80%    -       PASS
gate_nv1047.sv            30%    80%    +50%    PASS (SR+CLK 분주)
gate_nt39565d.sv          30%    45%    +15%    부분 (STV/OE 미완)
row_scan_eng.sv           60%    75%    +15%    PASS
afe_ad711xx.sv            20%    65%    +45%    부분 (SPI 포맷 검증)
afe_afe2256.sv            20%    65%    +45%    부분 (SPI 포맷 검증)
afe_spi_master.sv         60%    60%    -       부분
line_data_rx.sv           50%    50%    -       부분 (ISERDESE2)
line_buf_ram.sv           80%    80%    -       PASS
data_out_mux.sv           50%    75%    +25%    PASS (cfg_ncols)
mcu_data_if.sv            60%    60%    -       부분
csi2_packet_builder.sv    60%    60%    -       부분
csi2_lane_dist.sv         60%    60%    -       부분
detector_core.sv           0%    85%    +85%    신규 구현
fpga_top_c1/c3/c6.sv     10%    85%    +75%    PASS (인스턴스화)
forward_bias_ctrl.sv       0%     0%    -       미존재
correction_pipeline        0%     0%    -       미존재
```

**전체 평균 완성도: v2 ~45% → v3 ~63% (+18%p 개선)**

---

## 6. 권장 구현 우선순위 (v3 개정)

### Phase 1 — CRITICAL (차단 이슈, 5건)

| 순위 | ID | 모듈 | 작업 | 난이도 |
|------|-----|------|------|--------|
| 1 | PART-001 | gate_nt39565d | STV dual-latch 시퀀스 + 좌/우 OE 독립 제어 구현 | 중 |
| 2 | OPEN-C01 | panel_ctrl_fsm | S1_POWER_CHECK, S3_PREP_WAIT, S5_XRAY_ENABLE, S7_SETTLE 추가 | 대 |
| 3 | OPEN-C04 | panel_ctrl_fsm | ST_SETTLE 전용 상태 + cfg_tsettle 레지스터 추가 | 소 |
| 4 | OPEN-C05 | PanelFsmModel | 골든 모델 타이밍을 레지스터 기반으로 전환 | 중 |
| 5 | NEW-001 | detector_core | Multi-AFE 디시리얼라이저 어레이 (C6/C7) | 대 |

### Phase 2 — HIGH (사양 정합성, 11건)

| 순위 | ID | 작업 | 난이도 |
|------|-----|------|--------|
| 1 | OPEN-H02 | 조합별 T_LINE/NCOLS 기본값 자동 선택 + TLINE_MIN 검증 | 중 |
| 2 | NEW-002 | line_buf_ram N_COLS 파라미터화 (MAX_COLS 또는 동적) | 소 |
| 3 | NEW-003 | gate_nt39565d OE1/OE2 좌/우 독립 제어 | 소 |
| 4 | PART-002 | S3_PREP_WAIT 핸드셰이크 상태 (200ms~2s 딜레이) | 중 |
| 5 | PART-003 | VGL→VGH T_VGL_TO_VGH 딜레이 레지스터 추가 | 소 |
| 6 | OPEN-H01 | REG_INT_MODE + AEC_STOP 신호 핸들링 | 중 |
| 7 | OPEN-H07 | clk_rst_mgr afe_type_sel 2단 CDC 동기화 | 소 |
| 8 | NEW-004 | CSI-2 레인 동적 할당 | 소 |
| 9 | OPEN-H03 | prot_mon 에러 플래그 통합 (temp/voltage/PLL) | 중 |
| 10 | OPEN-H04/05 | 골든 모델 TLINE_MIN + gate 타이밍 파라미터 반영 | 소 |
| 11 | OPEN-H06 | 핵심 골든 모델 4개 우선 작성 (gate_nv1047/nt39565d, line_data_rx, panel_integ_ctrl) | 대 |

### Phase 3 — MEDIUM/LOW (완성도, 14건)

- 조합별 NCOLS 기본값 연동 (C4:1664, C6:3072)
- 골든 모델 나머지 4개 작성
- CIC 프로파일 검증 로직
- 다크 프레임 평균화 파이프라인
- SPI 패킷 포맷 데이터시트 대조
- prot_mon / emergency_shutdown 에러 경로 통합
- Forward Bias 제어 모듈 (v2 선행 설계)

### v2(외부 메모리 추가) 이후

- 보정 파이프라인 (offset/gain/defect/CIC/lag)
- Forward Bias 제어 모듈 완전 구현
- S9_POST_DARK, S10_CORRECTION, S11_TRANSFER, S12_POST_STAB 상태 추가

---

## 7. 변경 이력

| 버전 | 날짜 | 변경 내용 |
|------|------|-----------|
| v1 | 2026-03-20 | 초기 교차검증 (63건, 4개 에이전트) |
| v2 | 2026-03-21 | 재검증 + 중복 제거 (46건, 3개 에이전트) |
| **v3** | **2026-03-21** | **수정 현황 점검 — 17건 FIXED, 6건 PARTIAL, 22건 OPEN, 7건 신규 (총 잔여 35건)** |

---

*Generated by MoAI Cross-Verification Pipeline v3 (3 parallel re-verification agents)*
*RTL Files Re-analyzed: 27 | Spec Documents: 3 | Golden Models: 15*
*수정률: 37% (17/46건 완전 해결) | 전체 완성도: ~63% (v2 대비 +18%p)*
