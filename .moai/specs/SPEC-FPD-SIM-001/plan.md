# Implementation Plan: SPEC-FPD-SIM-001

## SW-First Verification Framework 구현 계획

---

## 1. C++ Golden Model Class Hierarchy

### Base Architecture

```
sim/
├── golden_models/
│   ├── core/                         Base framework
│   │   ├── GoldenModelBase.h         Abstract base class
│   │   ├── GoldenModelBase.cpp       Common utilities
│   │   ├── SignalTypes.h             Bit-accurate types (uint16_t, packed structs)
│   │   ├── ClockDomain.h            Multi-clock domain modeling
│   │   ├── ClockDomain.cpp
│   │   ├── TestVectorIO.h           Test vector read/write (hex/binary)
│   │   ├── TestVectorIO.cpp
│   │   ├── CRC16.h                  CRC-16 CCITT (CSI-2 compatible)
│   │   ├── CRC16.cpp
│   │   ├── ECC.h                    MIPI CSI-2 ECC calculator
│   │   └── ECC.cpp
│   │
│   ├── models/                       Per-module golden models
│   │   ├── SpiSlaveModel.h/cpp       SPEC-001: SPI slave + register bank
│   │   ├── RegBankModel.h/cpp        SPEC-001: 32-register file
│   │   ├── ClkRstModel.h/cpp         SPEC-001: Clock/reset manager
│   │   ├── PanelFsmModel.h/cpp       SPEC-002: Panel control FSM (7-state, 5-mode)
│   │   ├── PanelResetModel.h/cpp     SPEC-002: Panel reset controller
│   │   ├── PanelIntegModel.h/cpp     SPEC-002: Integration + X-ray handshake
│   │   ├── GateNv1047Model.h/cpp     SPEC-003: NV1047 shift register + OE
│   │   ├── RowScanModel.h/cpp        SPEC-003: Row scan engine
│   │   ├── GateNt39565dModel.h/cpp   SPEC-004: NT39565D dual-STV + 6-chip
│   │   ├── AfeAd711xxModel.h/cpp     SPEC-005: AD71124/AD71143 (parameterized)
│   │   ├── AfeSpiMasterModel.h/cpp   SPEC-005: AFE SPI daisy-chain
│   │   ├── AfeAfe2256Model.h/cpp     SPEC-006: AFE2256 + CIC + pipeline
│   │   ├── LvdsRxModel.h/cpp         SPEC-007: LVDS receiver (per AFE)
│   │   ├── LineBufModel.h/cpp        SPEC-007: Ping-pong BRAM line buffer
│   │   ├── Csi2PacketModel.h/cpp     SPEC-007: CSI-2 packet builder
│   │   ├── Csi2LaneDistModel.h/cpp   SPEC-007: 2/4-lane byte distributor
│   │   ├── ProtMonModel.h/cpp        SPEC-008: Protection monitor
│   │   ├── EmergShutdownModel.h/cpp  SPEC-008: Emergency shutdown
│   │   ├── PowerSeqModel.h/cpp       SPEC-008: Power sequencer (VGL→VGH)
│   │   └── RadiogModel.h/cpp         SPEC-010: Radiography sub-FSM
│   │
│   ├── generators/                    Test vector generators
│   │   ├── gen_spi_vectors.cpp        SPEC-001 vector generator
│   │   ├── gen_fsm_vectors.cpp        SPEC-002 vector generator
│   │   ├── gen_gate_vectors.cpp       SPEC-003/004 vector generator
│   │   ├── gen_afe_vectors.cpp        SPEC-005/006 vector generator
│   │   ├── gen_csi2_vectors.cpp       SPEC-007 vector generator
│   │   └── gen_safety_vectors.cpp     SPEC-008 vector generator
│   │
│   └── test_vectors/                  Generated output (hex/bin)
│       ├── spec001/
│       ├── spec002/
│       ├── ...
│       └── spec010/
│
├── cocotb_tests/                      Python testbenches
│   ├── conftest.py                    Shared fixtures + vector loader
│   ├── test_spi_slave.py             SPEC-001
│   ├── test_reg_bank.py              SPEC-001
│   ├── test_clk_rst.py               SPEC-001
│   ├── test_panel_fsm.py             SPEC-002
│   ├── test_gate_nv1047.py           SPEC-003
│   ├── test_gate_nt39565d.py         SPEC-004
│   ├── test_afe_ad711xx.py           SPEC-005
│   ├── test_afe_afe2256.py           SPEC-006
│   ├── test_lvds_rx.py               SPEC-007
│   ├── test_line_buf.py              SPEC-007
│   ├── test_csi2_tx.py               SPEC-007
│   ├── test_safety.py                SPEC-008
│   ├── test_integration.py           SPEC-009
│   └── test_radiography.py           SPEC-010
│
├── verilator/                         Cycle-accurate RTL comparison
│   ├── sim_main.cpp                   Verilator top-level driver
│   ├── golden_compare.h/cpp           C++ model vs RTL comparator engine
│   ├── compare_spi.cpp                SPEC-001 comparison
│   ├── compare_fsm.cpp                SPEC-002 comparison
│   ├── compare_csi2.cpp               SPEC-007 comparison
│   ├── waveform_dump.h/cpp            VCD/FST waveform output
│   └── Makefile                       Verilator build rules
│
├── tests/                             C++ unit tests (Google Test)
│   ├── test_crc16.cpp                 CRC-16 unit test
│   ├── test_ecc.cpp                   ECC unit test
│   ├── test_spi_model.cpp            SpiSlaveModel unit test
│   ├── test_fsm_model.cpp            PanelFsmModel unit test
│   ├── test_csi2_model.cpp           Csi2PacketModel unit test
│   └── test_vector_io.cpp            TestVectorIO unit test
│
└── CMakeLists.txt                     Top-level build configuration
```

### GoldenModelBase Interface

```cpp
// sim/golden_models/core/GoldenModelBase.h
class GoldenModelBase {
public:
    virtual ~GoldenModelBase() = default;

    // Initialize to power-on reset state
    virtual void reset() = 0;

    // Advance one clock cycle (rising edge)
    virtual void step() = 0;

    // Set input signals before step()
    virtual void set_inputs(const std::map<std::string, uint32_t>& inputs) = 0;

    // Get output signals after step()
    virtual std::map<std::string, uint32_t> get_outputs() const = 0;

    // Compare outputs against RTL (returns mismatch list)
    virtual std::vector<Mismatch> compare(
        const std::map<std::string, uint32_t>& rtl_outputs) const = 0;

    // Generate test vectors for this module
    virtual void generate_vectors(const std::string& output_dir) = 0;

    // Get current cycle count
    uint64_t cycle() const { return cycle_count_; }

protected:
    uint64_t cycle_count_ = 0;
};
```

### Test Vector Format

```
# Test Vector File Format (hex)
# Line format: <cycle> <input_signals...> <expected_output_signals...>
# Header: signal names and widths
#
# @MODULE: spi_slave_if
# @SPEC: SPEC-FPD-001
# @SIGNALS_IN: sclk:1 mosi:1 cs_n:1
# @SIGNALS_OUT: miso:1 reg_data:16
# @CLOCK: sys_clk 100MHz
#
0000 0 0 1 0 0000
0001 0 0 0 0 0000
0002 1 1 0 0 0000
...
```

---

## 2. RTL Conversion Methodology (C++ → SystemVerilog)

### Stage 1: Algorithm Verification (C++ Only)

| Step | Action | Output | Duration |
|------|--------|--------|----------|
| 1.1 | C++ 골든 모델 클래스 작성 | {Module}Model.h/cpp | 1-2일/모듈 |
| 1.2 | Google Test 단위 테스트 | tests/test_{module}_model.cpp | 0.5일/모듈 |
| 1.3 | 테스트 벡터 생성기 작성 | generators/gen_{spec}_vectors.cpp | 0.5일/SPEC |
| 1.4 | 알고리즘 정확성 검증 | test_vectors/{spec}/*.hex | 검증 완료 |

### Stage 2: Architectural Mapping (C++ → SV 매핑 규칙)

| C++ Construct | SystemVerilog Construct | Notes |
|---------------|------------------------|-------|
| `class ModuleModel` | `module module_name` | 1:1 매핑 |
| `uint16_t member_var` | `logic [15:0] reg_name` | always_ff 레지스터 |
| `bool flag` | `logic flag` | 1-bit 레지스터 |
| `std::array<uint16_t, N>` | `logic [15:0] mem [0:N-1]` | N>64: BRAM, else register file |
| `step() { if (rst) ... }` | `always_ff @(posedge clk or posedge rst)` | 비동기 리셋 |
| `step() { compute(); }` | `always_ff @(posedge clk)` | 동기 로직 |
| `uint32_t combo_func()` | `always_comb` | 조합 로직 |
| `enum class State` | `typedef enum logic [N:0]` | FSM 상태 |
| `switch(state)` | `case (state)` inside always_ff | FSM 전이 |
| `std::queue<T>` | Async FIFO (dual-clock BRAM) | CDC FIFO |
| `function call()` | `module instance` or `task` | 계층적 분해 |
| `#define PARAM` | `parameter PARAM` | 컴파일 타임 상수 |
| `constructor args` | `module #(.PARAM(val))` | 파라미터화된 인스턴스 |

### Stage 3: RTL Implementation (TDD Cycle)

```
For each SPEC module:

1. [RED]   cocotb 테스트벤치 작성 (골든 모델 벡터 기반)
2. [RED]   RTL 스켈레톤 작성 (포트 선언만, 로직 없음)
3. [RED]   cocotb 실행 → FAIL (expected)
4. [GREEN] RTL 로직 구현 (C++ step() 함수를 SV always_ff로 변환)
5. [GREEN] cocotb 실행 → PASS
6. [REFACTOR] RTL 최적화:
   - BRAM inference 확인 (Vivado synth report)
   - 파이프라인 스테이지 삽입 (타이밍 개선)
   - 리소스 사용량 확인 (LUT, FF, BRAM budget)
7. [VERIFY] Verilator: golden_compare.cpp로 사이클별 비트 비교
8. [SYNTHESIZE] Vivado: 합성 + 타이밍 분석
   - 타이밍 위반 시 → RTL 수정 → Step 5로 복귀
```

### Stage 4: Bit-Accuracy Verification (Verilator)

```cpp
// sim/verilator/golden_compare.cpp (핵심 비교 엔진)
int main(int argc, char** argv) {
    Verilated::commandArgs(argc, argv);

    // Instantiate RTL (Verilated)
    auto rtl = std::make_unique<Vmodule_name>();

    // Instantiate golden model
    auto golden = std::make_unique<ModuleNameModel>(params);

    // Reset both
    rtl->rst = 1; golden->reset();
    for (int i = 0; i < 10; i++) { rtl->clk ^= 1; rtl->eval(); }
    rtl->rst = 0;

    // Run simulation
    uint64_t mismatches = 0;
    for (uint64_t cycle = 0; cycle < MAX_CYCLES; cycle++) {
        // Apply same inputs to both
        apply_inputs(rtl, golden, test_vectors[cycle]);

        // Step both
        rtl->clk = 1; rtl->eval();
        golden->step();

        // Compare outputs
        auto diffs = golden->compare(get_rtl_outputs(rtl));
        if (!diffs.empty()) {
            report_mismatch(cycle, diffs);
            mismatches++;
            if (mismatches > MAX_MISMATCHES) break;
        }

        rtl->clk = 0; rtl->eval();
    }

    return (mismatches > 0) ? 1 : 0;  // Non-zero = CI fail
}
```

### Stage 5: Synthesis Validation

| Check | Tool | Pass Criteria |
|-------|------|---------------|
| Functional | Verilator | 0 mismatches vs golden model |
| Timing (setup) | Vivado | No setup violations @ 100 MHz |
| Timing (hold) | Vivado | No hold violations (or < 20 ps) |
| BRAM usage | Vivado | <= 8 BRAM36K (v1 budget) |
| LUT usage | Vivado | <= 80% of 20,800 LUTs |
| FF usage | Vivado | <= 80% of 41,600 FFs |
| I/O usage | Vivado | <= 250 pins (LVDS included) |

---

## 3. CDC Verification Strategy

### Golden Model Abstraction

```cpp
// CDC는 golden model에서 queue로 추상화
class LineBufModel : public GoldenModelBase {
    // DCLK domain → SYS_CLK domain: queue-based CDC
    std::queue<uint16_t> cdc_fifo_;  // Async FIFO abstraction
    std::array<uint16_t, 2048> bank_a_;  // Write bank
    std::array<uint16_t, 2048> bank_b_;  // Read bank
    bool active_bank_ = false;  // false=A write, true=B write

    void step_dclk() { /* DCLK domain: push to fifo */ }
    void step_sysclk() { /* SYS_CLK domain: pop from fifo, write to bank */ }
};
```

### cocotb CDC Test Strategy

| Test | Method | Verification |
|------|--------|-------------|
| 기본 CDC 전송 | DCLK=20MHz, SYS_CLK=100MHz | 데이터 무결성 |
| 스트레스 테스트 | DCLK 지터 ±5%, FIFO 75% fill | overflow 없음 |
| 뱅크 스왑 | 라인 경계에서 ping-pong | 1 SYS_CLK 이내 전환 |
| 24-AFE 동시 | 24 독립 DCLK 스트림 | 데이터 손상 없음 |
| Backpressure | CSI-2 TX 지연 주입 | FIFO overflow 방지 |

---

## 4. Build System Architecture

### CMakeLists.txt Structure

```cmake
cmake_minimum_required(VERSION 3.20)
project(fpd_golden_models LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Options
option(BUILD_TESTS "Build unit tests" ON)
option(BUILD_VERILATOR "Build Verilator simulation" OFF)
option(BUILD_GENERATORS "Build test vector generators" ON)

# Golden model core library
add_library(golden_core STATIC
    golden_models/core/GoldenModelBase.cpp
    golden_models/core/ClockDomain.cpp
    golden_models/core/TestVectorIO.cpp
    golden_models/core/CRC16.cpp
    golden_models/core/ECC.cpp
)

# Golden model modules library
add_library(golden_models STATIC
    golden_models/models/SpiSlaveModel.cpp
    golden_models/models/RegBankModel.cpp
    golden_models/models/PanelFsmModel.cpp
    golden_models/models/GateNv1047Model.cpp
    golden_models/models/AfeAd711xxModel.cpp
    golden_models/models/Csi2PacketModel.cpp
    golden_models/models/Csi2LaneDistModel.cpp
    golden_models/models/LineBufModel.cpp
    golden_models/models/ProtMonModel.cpp
    golden_models/models/PowerSeqModel.cpp
    # ... all models
)
target_link_libraries(golden_models PUBLIC golden_core)

# Test vector generators
if(BUILD_GENERATORS)
    foreach(spec IN ITEMS spi fsm gate afe csi2 safety)
        add_executable(gen_${spec}_vectors
            golden_models/generators/gen_${spec}_vectors.cpp)
        target_link_libraries(gen_${spec}_vectors PRIVATE golden_models)
    endforeach()
endif()

# Unit tests (Google Test)
if(BUILD_TESTS)
    find_package(GTest REQUIRED)
    add_executable(golden_tests
        tests/test_crc16.cpp
        tests/test_ecc.cpp
        tests/test_spi_model.cpp
        tests/test_fsm_model.cpp
        tests/test_csi2_model.cpp
        tests/test_vector_io.cpp
    )
    target_link_libraries(golden_tests PRIVATE golden_models GTest::gtest_main)
    gtest_discover_tests(golden_tests)
endif()
```

### cocotb Makefile (per SPEC)

```makefile
# sim/cocotb_tests/Makefile
TOPLEVEL_LANG = verilog
SIM ?= xsim
VERILOG_SOURCES = $(RTL_DIR)/common/spi_slave_if.sv
TOPLEVEL = spi_slave_if
MODULE = test_spi_slave
include $(shell cocotb-config --makefiles)/Makefile.sim
```

---

## 5. Implementation Order (SPEC-FPD 순서와 정렬)

### Phase 1: Foundation (SPEC-001 기반)

| Step | Golden Model | cocotb Test | Verilator | Duration |
|------|-------------|-------------|-----------|----------|
| 1.1 | core/ framework (GoldenModelBase, SignalTypes, TestVectorIO, CRC16, ECC) | - | - | 2일 |
| 1.2 | SpiSlaveModel + RegBankModel | test_spi_slave.py, test_reg_bank.py | compare_spi.cpp | 3일 |
| 1.3 | ClkRstModel | test_clk_rst.py | - | 1일 |
| 1.4 | CMake + Google Test setup | - | Makefile | 1일 |

### Phase 2: FSM + Safety (SPEC-002 + 008, 병렬)

| Step | Golden Model | cocotb Test | Duration |
|------|-------------|-------------|----------|
| 2.1 | PanelFsmModel (7-state, 5-mode) | test_panel_fsm.py | 3일 |
| 2.2 | PanelResetModel + PanelIntegModel | test_panel_fsm.py (확장) | 2일 |
| 2.3 | ProtMonModel + EmergShutdownModel | test_safety.py | 2일 |
| 2.4 | PowerSeqModel (VGL→VGH) | test_safety.py (확장) | 1일 |

### Phase 3: Gate IC (SPEC-003 + 004)

| Step | Golden Model | cocotb Test | Duration |
|------|-------------|-------------|----------|
| 3.1 | RowScanModel (공통 엔진) | - | 1일 |
| 3.2 | GateNv1047Model (SD1/CLK/OE) | test_gate_nv1047.py | 2일 |
| 3.3 | GateNt39565dModel (dual-STV, 6-chip cascade) | test_gate_nt39565d.py | 3일 |

**NT39565D 6-Chip Cascade 검증 상세:**

```
// GateNt39565dModel cascade propagation pseudocode
void GateNt39565dModel::step_cascade() {
    // 6-chip cascade: chip[0].STVD → chip[1].STVI → ... → chip[5].STVD
    for (int i = 1; i < m_num_chips; i++) {
        chip_stvi[i] = chip_stvd[i-1];  // STVD → next STVI
    }
    // Cascade complete when last chip's STVD asserts
    m_cascade_done = chip_stvd[m_num_chips - 1];
    // Total gate lines: 541ch × 6 chips = 3246 (≥3072 required)
}

// Test vectors for cascade:
// TV-004-C1: STV1 start → chip[0] STVD propagation delay
// TV-004-C2: Full 6-chip cascade → last STVD assert
// TV-004-C3: OE1/OE2 split during cascade (odd/even channels)
// TV-004-C4: LR direction reversal → cascade order inversion
```

### Phase 4: AFE Controller (SPEC-005 + 006)

| Step | Golden Model | cocotb Test | Duration |
|------|-------------|-------------|----------|
| 4.1 | AfeSpiMasterModel (daisy-chain) | - | 1일 |
| 4.2 | AfeAd711xxModel (ACLK, SYNC, IFS) | test_afe_ad711xx.py | 3일 |
| 4.3 | AfeAfe2256Model (MCLK, CIC, pipeline) | test_afe_afe2256.py | 3일 |

**AFE2256 Pipeline Latency 모델 상세:**

```
// AfeAfe2256Model pipeline state
struct PipelineState {
    std::vector<uint16_t> m_pipeline_latch;  // row[n-1] ADC data (256ch)
    uint16_t m_pipeline_age;                  // Pipeline fill counter
    bool m_pipeline_valid;                    // First valid output after 1-row delay
};

// step() pipeline behavior:
// Cycle N: row[n] integration starts + row[n-1] ADC readout outputs
// m_pipeline_latch holds row[n-1] data while row[n] integrates
// m_pipeline_valid = false for first row (no previous data)
// m_pipeline_age increments each row, wraps at frame boundary
```

### Phase 5: Data Path (SPEC-007) — 가장 복잡

| Step | Golden Model | cocotb Test | Duration |
|------|-------------|-------------|----------|
| 5.1 | LvdsRxModel (ADI + TI mode) | test_lvds_rx.py | 2일 |
| 5.2 | LineBufModel (ping-pong + CDC) | test_line_buf.py | 3일 |
| 5.3 | Csi2PacketModel (FS/FE + RAW16 + CRC) | test_csi2_tx.py | 3일 |
| 5.4 | Csi2LaneDistModel (2/4-lane) | test_csi2_tx.py (확장) | 2일 |

### Phase 6: Integration (SPEC-009 + 010)

| Step | Golden Model | cocotb Test | Duration |
|------|-------------|-------------|----------|
| 6.1 | 전체 데이터 경로 통합 모델 | test_integration.py | 3일 |
| 6.2 | RadiogModel (정지영상 서브-FSM) | test_radiography.py | 2일 |
| 6.3 | C1/C3/C6 조합별 Verilator 검증 | compare_integration.cpp | 3일 |

### 총 예상 규모

| Category | Files | LOC (estimated) |
|----------|-------|-----------------|
| Core framework | 10 | ~800 |
| Golden models | 40 (20 .h + 20 .cpp) | ~4,000 |
| Vector generators | 6 | ~600 |
| C++ unit tests | 6 | ~1,200 |
| cocotb tests | 14 | ~2,800 |
| Verilator harness | 6 | ~800 |
| Build system | 3 (CMake + Makefiles) | ~200 |
| **Total** | **~85 files** | **~10,400 LOC** |

---

## 6. CI/CD Pipeline

```yaml
# .github/workflows/sw-first-verify.yml (개념)
name: SW-First Verification
on: [push, pull_request]

jobs:
  golden-model-tests:
    runs-on: ubuntu-latest
    steps:
      - cmake --build . --target golden_tests
      - ctest --output-junit results.xml

  test-vector-generation:
    needs: golden-model-tests
    steps:
      - cmake --build . --target gen_spi_vectors gen_fsm_vectors ...
      - archive test_vectors/

  cocotb-tests:
    needs: test-vector-generation
    steps:
      - pip install cocotb
      - make -C sim/cocotb_tests/ SIM=verilator

  verilator-comparison:
    needs: golden-model-tests
    steps:
      - cmake --build . --target verilator_sim
      - ./compare_spi --vectors=test_vectors/spec001/
```

---

## 7. Xilinx Primitive Handling (Verilator Compatibility)

Verilator는 Xilinx 프리미티브를 직접 지원하지 않음. Behavioral wrapper로 해결:

| Module | Xilinx Primitives | Verilator Strategy | xsim Strategy |
|--------|-------------------|-------------------|---------------|
| clk_rst_mgr | MMCME2_ADV | Behavioral wrapper (클럭 분주/체배) | 실제 MMCM |
| line_data_rx | IBUFDS, ISERDESE2, IDELAYE2 | Behavioral wrapper (DDR→8-bit 역직렬화) | 실제 프리미티브 |
| CSI-2 LVDS TX | OSERDESE2 | Behavioral wrapper (8-bit→DDR 직렬화) | 실제 프리미티브 |

Behavioral wrapper 위치: `sim/verilator/xilinx_behav/`

```
sim/verilator/xilinx_behav/
├── MMCME2_ADV_behav.sv     MMCM: 입력 클럭 분주/체배 behavioral
├── ISERDESE2_behav.sv      ISERDES: DDR→8-bit 역직렬화 behavioral
├── IDELAYE2_behav.sv       IDELAY: 탭 지연 (고정) behavioral
├── IBUFDS_behav.sv         IBUFDS: 차동→단일 변환 behavioral
└── OSERDESE2_behav.sv      OSERDES: 8-bit→DDR 직렬화 behavioral
```

**검증 전략**: Verilator (behavioral RTL) → xsim (실제 프리미티브) → Vivado 합성 순서로 검증 수준을 높여감.

---

## 8. Trade-off Analysis Summary

### SystemC vs Pure C++17

| Criterion | SystemC | Pure C++17 (채택) |
|-----------|---------|-------------------|
| 클럭 모델링 | sc_clock 내장 | ClockDomain 수동 구현 |
| Windows MSVC | 불안정 (POSIX 의존) | 완전 지원 |
| 학습 곡선 | 급경사 | 최소 |
| Verilator DPI-C | 동일 | 동일 |
| **결정** | | **채택**: 빌드 단순, Windows 지원, 충분한 behavioral 정확도 |

### DPI-C vs File-Based Vectors

| Phase | 방식 | 근거 |
|-------|------|------|
| Stage 3 (cocotb) | File-based (.hex) | 시뮬레이터 독립적, Python 로드 |
| Stage 4 (Verilator) | DPI-C (in-process) | 사이클별 비교, GDB 디버깅 |

### Verilator vs xsim 역할 분리

| 역할 | Tool | 근거 |
|------|------|------|
| Algorithm verification | Verilator | 10-50x 빠름, DPI-C 네이티브 |
| Xilinx primitive 검증 | xsim | ISERDESE2/MMCM 지원 |
| Post-synthesis | xsim | SDF back-annotation |

### Key Decisions

1. C++17 (structured bindings, constexpr if)
2. 정수 연산 전용 — 부동소수점 미사용 (RTL 일치)
3. MSB-first 직렬화 (ADI/TI 데이터시트 일치)
4. CRC-16-CCITT: 다항식 0x1021, init 0xFFFF
5. GoogleTest + CMake FetchContent
```

---

Version: 1.0.0
Created: 2026-03-19
