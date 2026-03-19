# Product Definition: X-ray Flat Panel Detector FPGA Control System

## Project Name

panel-operation — FPGA-based X-ray FPD (Flat Panel Detector) Control System

## Description

a-Si TFT 기반 X-ray Flat Panel Detector의 FPGA 구동 제어 시스템.
3종의 패널, 2종의 Gate IC, 3종의 AFE/ROIC를 조합한 7가지 하드웨어 조합(C1-C7)을 통합 지원하는
SystemVerilog RTL 설계 및 검증 프로젝트.

## Target Audience

- FPGA 설계 엔지니어 (RTL 개발 및 검증)
- X-ray 검출기 시스템 엔지니어
- 의료영상 장비 개발팀

## Core Features

### 1. 통합 패널 구동 FSM
- 6-state 메인 FSM: INIT → IDLE → CALIBRATE → ACQUIRE → DONE → ERROR
- 5 동작 모드: STATIC, CONTINUOUS, TRIGGERED, DARK_FRAME, RESET_ONLY
- MCU SPI를 통한 런타임 파라미터 설정 (하드코딩 금지)

### 2. Gate IC 하드웨어 추상화
- NV1047 드라이버 (C1-C5): 300ch, SD1/SD2 시프트 레지스터, OE/ONA 제어
- NT39565D 드라이버 (C6-C7): 541ch, 듀얼 STV, OE1/OE2 분리 제어, 6-chip 캐스케이드

### 3. AFE/ROIC 제어 인터페이스
- AD71124/AD71143 (afe_ad711xx): ACLK 기반, LVDS 2채널, tLINE 22/60us
- AFE2256 (afe_afe2256): MCLK 기반, 내부 TG, CIC 보상, 4-lane MUX LVDS

### 4. Multi-AFE 병렬 리드아웃 (최대 24 AFE)
- 모든 AFE의 LVDS 출력이 FPGA에 직접 연결 (외부 MUX 없음)
- 브로드캐스트 SYNC + SPI 데이지체인 (24-chip)
- Artix-7 35T (xc7a35tfgg484-1)에서 24 AFE 지원 확인 (실제 운용 중)

### 5. 실시간 보정 파이프라인
- FPGA 내장: 오프셋 감산 → 게인 정규화 → 결함 화소 보간 → LTI 래그 보정
- 소프트웨어 위임: NLCSC 비선형 래그 보정, 온도 인덱스 오프셋 맵

### 6. 안전 시스템
- 전원 시퀀서 (M0-M5 모드, VGL→VGH 순서 강제)
- 비상 정지 (과전압, 과온도, PLL 실패 감지)
- 과노출 타임아웃 (5초 하드 리밋)

### 7. Forward Bias 래그 보정
- +4V 바이어스 8-pixel 그룹 사이클링 (100kHz)
- 래그 2-5% → <0.3% 개선

## Hardware Combination Matrix

| ID | Panel | Gate IC | AFE | Use Case |
|----|-------|---------|-----|----------|
| C1 | R1717 17x17" | NV1047 | AD71124 | 표준 정지상 |
| C2 | R1717 | NV1047 | AD71143 | 저전력/모바일 |
| C3 | R1717 | NV1047 | AFE2256 | 고화질 (저노이즈, CIC) |
| C4 | R1714 17x14" | NV1047 | AD71124 | 비정방형 |
| C5 | R1714 | NV1047 | AFE2256 | 고화질 17x14 |
| C6 | X239AW1-102 43x43cm | NT39565D x6 | AD71124 x12 | 대형, 다중 AFE |
| C7 | X239AW1-102 | NT39565D x6 | AFE2256 x12 | 대형, 고화질 |

## Use Cases

1. **정지영상 (Radiography)**: 단일 프레임 고해상도 획득 (STATIC 모드)
2. **형광투시 (Fluoroscopy)**: 연속 프레임 15-30fps (CONTINUOUS 모드)
3. **외부 트리거 촬영**: X-ray 제너레이터 동기화 (TRIGGERED 모드)
4. **캘리브레이션**: 다크 프레임 획득, 플랫필드 보정 (DARK_FRAME 모드)

## Performance Targets

| Metric | C1-C5 (2048x2048) | C6-C7 (3072x3072) |
|--------|--------------------|--------------------|
| 정지상 프레임 레이트 | 1-2 fps | 1 fps |
| 형광투시 프레임 레이트 | 30 fps | 15 fps |
| 라인 타임 | 22-60 us | 10.85-21.7 us |
| 래그 보정 후 잔류 | < 0.3% | < 0.5% |
| 보정 파이프라인 지연 | < 5 pixel clocks | < 5 pixel clocks |

## Constraints

- IEC 62220-1 준수 (DQE, MTF, 래그 특성)
- a-Si TFT RC 시정수: τ = 3-9 µs (Gate 펄스 ≥ 5τ 필요)
- VGL → VGH 전원 순서 강제 (위반 시 Gate IC 래치업)
- 소프트 스타트 슬루율 ≤ 5 V/ms
