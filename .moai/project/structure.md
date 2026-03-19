# Project Structure: panel-operation

## Architecture Pattern

파라미터 기반 하드웨어 추상화 아키텍처.
공통 코어 모듈 + 조합별 선택 모듈(Gate IC, AFE) + 조합별 Top-Level 핀 매핑.

## Directory Structure

```
panel-operation/
├── docs/                           # 설계 문서 및 리서치
│   ├── fpga-design/               # FPGA 설계 사양서 (4개)
│   │   ├── fpga_module_architecture.md        # 모듈 계층 + 레지스터 맵
│   │   ├── fpga-xray-detector-driving-algorithm.pplx.md  # 구동 알고리즘 (13장)
│   │   ├── fpga-radiography-static-imaging.pplx.md       # 정지영상 전용 알고리즘
│   │   └── fpga-panel-power-settings.pplx.md             # 전원 시퀀싱
│   ├── research/                  # 부품/알고리즘 리서치 (7개)
│   └── datasheet/                 # IC 데이터시트 PDF (10개)
├── .moai/                         # MoAI 프로젝트 설정
│   ├── config/sections/           # 워크플로우 설정
│   └── project/                   # 프로젝트 문서 (product, structure, tech)
├── .claude/                       # Claude Code 에이전트/스킬/규칙
└── CLAUDE.md                      # 프로젝트 가이드
```

## Planned RTL Structure (구현 예정)

```
rtl/
├── common/                        # 공통 모듈 (모든 조합)
│   ├── spi_slave_if.sv           # MCU SPI 슬레이브 인터페이스
│   ├── clk_rst_mgr.sv           # 클럭 분배 + 리셋 동기화
│   ├── reg_bank.sv              # 32-레지스터 파일 (0x00-0x1F)
│   ├── panel_ctrl_fsm.sv        # 메인 구동 FSM (6 states)
│   ├── row_scan_eng.sv          # 행 스캔 카운터
│   ├── line_data_rx.sv          # LVDS 데이터 수신 (ADI/TI 모드 파라미터)
│   ├── line_buf_ram.sv          # 라인 버퍼 BRAM
│   ├── prot_mon.sv              # 보호 모니터 (과노출 타임아웃)
│   ├── data_out_mux.sv          # 데이터 출력 정렬
│   ├── mcu_data_if.sv           # MCU 데이터 전송
│   ├── power_sequencer.sv       # 전원 모드 관리 (M0-M5)
│   ├── emergency_shutdown.sv    # 비상 정지
│   └── forward_bias_ctrl.sv     # Forward Bias 래그 보정
│
├── gate_drivers/                  # Gate IC 드라이버 (조합별 선택)
│   ├── gate_nv1047.sv           # NV1047 드라이버 (C1-C5)
│   └── gate_nt39565d.sv         # NT39565D 드라이버 (C6-C7)
│
├── afe_controllers/               # AFE 제어 (조합별 선택)
│   ├── afe_ad711xx.sv           # AD71124/AD71143 통합 (IFS 비트폭 파라미터)
│   └── afe_afe2256.sv           # AFE2256 + CIC 제어
│
├── calibration/                   # 보정 파이프라인
│   ├── offset_subtractor.sv     # 오프셋 감산 (스트리밍)
│   ├── gain_multiplier.sv       # 게인 정규화 (고정소수점)
│   ├── defect_replacer.sv       # 결함 화소 보간 (3x3)
│   └── lag_corrector_lti.sv     # LTI 래그 보정 (4-지수 성분)
│
├── top/                           # 조합별 Top-Level
│   ├── fpga_top_c1.sv           # C1: NV1047 + AD71124
│   ├── fpga_top_c3.sv           # C3: NV1047 + AFE2256
│   ├── fpga_top_c6.sv           # C6: NT39565D + AD71124 x12
│   └── fpga_top_c7.sv           # C7: NT39565D + AFE2256 x12
│
└── packages/                      # 공통 타입/파라미터
    ├── fpd_types_pkg.sv         # 프로젝트 전역 타입 정의
    └── fpd_params_pkg.sv        # 조합별 파라미터 패키지

tb/                                # 테스트벤치
├── unit/                          # 모듈 단위 테스트
│   ├── tb_spi_slave_if.sv
│   ├── tb_reg_bank.sv
│   ├── tb_clk_rst_mgr.sv
│   ├── tb_panel_ctrl_fsm.sv
│   ├── tb_gate_nv1047.sv
│   ├── tb_gate_nt39565d.sv
│   ├── tb_afe_ad711xx.sv
│   ├── tb_afe_afe2256.sv
│   ├── tb_line_data_rx.sv
│   └── tb_prot_mon.sv
├── integration/                   # 통합 테스트
│   ├── tb_detector_core_c1.sv
│   └── tb_detector_core_c6.sv
└── system/                        # 시스템 테스트
    └── tb_fpga_top_c1.sv

constraints/
├── fpga_constraints.xdc          # Xilinx 타이밍 제약
└── pin_assignments/
    ├── c1_pin_map.xdc
    ├── c3_pin_map.xdc
    ├── c6_pin_map.xdc
    └── c7_pin_map.xdc
```

## Module Dependency Graph

```
fpga_top_cX
├── spi_slave_if ──→ reg_bank
├── clk_rst_mgr
├── panel_ctrl_fsm ←── reg_bank
│   ├── gate_ic_driver [NV1047 | NT39565D]
│   │   └── row_scan_eng
│   ├── afe_ctrl_if [AD711xx | AFE2256]
│   │   └── line_data_rx
│   │       └── line_buf_ram
│   └── prot_mon
├── calibration_pipeline (offset → gain → defect → lag)
├── data_out_mux ←── line_buf_ram
├── mcu_data_if
├── power_sequencer ←── reg_bank
├── emergency_shutdown
└── forward_bias_ctrl
```

## Key Architecture Decisions

1. **파라미터 기반 설계**: 모든 타이밍은 reg_bank에서 런타임 설정, 하드코딩 금지
2. **조합 격리**: 차이점은 gate_ic_driver + afe_ctrl_if + fpga_top 핀 매핑에만 존재
3. **공통 코어**: 13/15 모듈이 모든 조합에서 공유
4. **FPGA vs MCU 분리**: 실시간 보정(오프셋/게인/결함)은 FPGA, 비선형 래그(NLCSC)는 MCU/PC
