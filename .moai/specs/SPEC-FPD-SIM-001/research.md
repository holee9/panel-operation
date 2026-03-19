# Research: SPEC-FPD-SIM-001 — SW-First Verification Framework

## Team Research Summary

3명 연구원의 병렬 딥리서치 결과를 통합한 문서.

- **researcher** (haiku): C++ 골든 모델 패턴, Verilator/cocotb 통합, 업계 사례
- **analyst** (sonnet): EARS 요구사항 R1-R7, 수용기준 19개, 리스크 6개, NFR 5개
- **architect** (opus): C++ 클래스 계층, RTL 변환 방법론, CDC 전략, 빌드 시스템

---

## 1. Industry Best Practices (Researcher Findings)

### Recommended Architecture: Hybrid Behavioral → Cycle-Accurate

- **Phase 1 (RTL 전)**: Pure C++ behavioral 모델 (500-2000 LOC/모듈)
- **Phase 2 (RTL 후)**: Verilator DPI-C로 line-by-line 비교
- **Phase 3 (최종)**: cocotb + xsim으로 FPGA 툴체인 호환성 확인

### Tool Stack Selection Rationale

| Tool | Selection Reason |
|------|-----------------|
| **Pure C++17** (not SystemC) | 빌드 단순, Windows MSVC 완전 지원, SystemC 20% 오버헤드 불필요 |
| **cocotb** (not UVM) | Python 기반, UVM 대비 10x 적은 코드, BSD 라이선스 |
| **Verilator** (primary) | 10-50x 빠른 시뮬레이션, DPI-C 네이티브, open-source |
| **Vivado xsim** (secondary) | Xilinx primitive 지원, 무료 라이선스, 합성 상관성 |
| **GoogleTest** | C++ 단위 테스트 표준, CMake 통합 우수 |

### Reference Implementations

- **OpenTitan**: SW-first 방법론 (C++ model → Verilator → xsim)
- **LiteX**: Verilator + cocotb 패턴 (CPU 시뮬레이션)
- CSI-2 packet builder: 공개 레퍼런스 없음 → MIPI spec 기반 자체 구현

---

## 2. Detailed C++ Class Hierarchy (Architect Findings)

### Core Framework

```cpp
// GoldenModelBase — 모든 골든 모델의 추상 기반 클래스
class GoldenModelBase {
public:
    virtual ~GoldenModelBase() = default;
    virtual void reset() = 0;                        // POR 상태로 초기화
    virtual void step() = 0;                         // 1 클럭 사이클 진행
    virtual void set_inputs(const std::map<std::string, uint32_t>& inputs) = 0;
    virtual std::map<std::string, uint32_t> get_outputs() const = 0;
    virtual std::vector<Mismatch> compare(           // RTL 출력과 비교
        const std::map<std::string, uint32_t>& rtl_outputs) const = 0;
    virtual void generate_vectors(const std::string& output_dir) = 0;
    uint64_t cycle() const { return cycle_count_; }
protected:
    uint64_t cycle_count_ = 0;
};

// SignalTypes — RTL 비트 폭과 정확히 일치하는 타입
using reg8_t = uint8_t;
using reg12_t = uint16_t;   // 12-bit in 16
using reg16_t = uint16_t;
using reg24_t = uint32_t;   // 24-bit in 32

// RegisterBank — 32개 16-bit 레지스터 (RTL reg_bank.sv와 1:1)
struct RegisterBank {
    reg16_t regs[32] = {};
    bool is_readonly(uint8_t addr) const;
    void write(uint8_t addr, reg16_t data);
    reg16_t read(uint8_t addr) const;
};

// FSM 상태 열거형 (fpd_types_pkg.sv와 동일)
enum class FsmState : uint8_t {
    IDLE=0, RESET, INTEGRATE, READOUT_INIT, SCAN_LINE, READOUT_DONE, DONE, ERROR
};
enum class OpMode : uint8_t {
    STATIC=0, CONTINUOUS, TRIGGERED, DARK_FRAME, RESET_ONLY
};

// CSI-2 패킷 구조체
struct Csi2ShortPacket { uint8_t di; uint16_t wc; uint8_t ecc; };
struct Csi2LongPacket { uint8_t di; uint16_t wc; uint8_t ecc;
                        std::vector<uint8_t> payload; uint16_t crc; };
```

### Per-Module Model Signatures

**SpiSlaveModel** (SPEC-001):
- Inputs: `sclk_in, mosi_in, cs_n_in`
- Outputs: `miso_out`
- Internal: `RegisterBank* reg_bank`, SPI Mode 0/3 지원
- step(): SPI 클럭 에지 처리, 8-bit addr + 16-bit data 프레임

**PanelFsmModel** (SPEC-002):
- Inputs: `start_cmd, abort_cmd, xray_ready, exposure_done, ext_trigger`
- Outputs: `FsmState state, gate_en, afe_en, scan_start, prep_request, xray_enable`
- Sub-controllers: `step_reset_ctrl()` (dummy scan), `step_integ_ctrl()` (적분 타이머)
- 7-state FSM + 5 operating modes

**GateNv1047Model** (SPEC-003):
- Inputs: `row_index, gate_on_pulse, scan_dir, t_gate_on, t_gate_settle`
- Outputs: `sd1, sd2, clk, oe, ona, lr, rst_out`
- GatePhase enum: IDLE→SHIFT→GATE_ON→SETTLE

**GateNt39565dModel** (SPEC-004):
- Inputs: `row_index, gate_on_pulse, scan_dir, chip_sel, mode`
- Outputs: `stv1l/r, stv2l/r, cpv_l/r, oe1_l/r, oe2_l/r`
- 6-chip cascade: `stvd_out[6]` 전파 모니터링

**AfeAd711xxModel** (SPEC-005):
- Variant enum: AD71124 (IFS 6-bit, tLINE≥22us) / AD71143 (IFS 5-bit, tLINE≥60us)
- Inputs: `afe_start, sync_in, ifs_code, lpf_code, pmode, sync_delay`
- Outputs: `aclk_out, sync_out, dout_window_valid, line_data[256*N]`
- SPI daisy-chain: `spi_chain[24]` (24-bit per AFE)

**AfeAfe2256Model** (SPEC-006):
- Inputs: `sync_in, tp_sel, pipeline_en, cic_en, cic_profile`
- Outputs: `mclk_out, sync_out, fclk_out, line_data[256*N]`
- CIC profile: 256ch x 16-bit, pipeline 1-row latency

**LvdsRxModel** (SPEC-007):
- Mode enum: ADI (2 LVDS data + DCLK) / TI (4 LVDS data + DCLK + FCLK)
- Per-AFE shift registers: `m_shift_reg[24]`, 16-bit 역직렬화

**LineBufModel** (SPEC-007):
- `write_pixel(col, data)` / `read_pixel(col)` / `swap_banks()`
- Ping-pong: `m_bank_a[2048]`, `m_bank_b[2048]`
- CDC: `std::queue<uint16_t>` 추상화 (RTL은 dual-clock BRAM)

**Csi2PacketModel** (SPEC-007):
- `build_frame_start()` / `build_frame_end()` → Short Packet
- `build_raw16_line(pixels)` → Long Packet (Header + Payload + CRC)
- Static verification: `verify_ecc()`, `verify_crc()`

**Csi2LaneDistModel** (SPEC-007):
- Constructor: `num_lanes` (2 or 4)
- `feed_bytes(packet_bytes)` → `lane_data[lane][byte_idx]`
- Interleaving: Lane N = byte[N, N+num_lanes, N+2*num_lanes, ...]

**ProtMonModel** (SPEC-008):
- 5초 타임아웃 카운터: `TIMEOUT_5S_CYCLES = 500,000,000`
- Inputs: `integrating, vgh_adc, temp_adc, pll_locked`
- Outputs: `timeout_flag, force_gate_off, emergency_shutdown, err_code`

**PowerSeqModel** (SPEC-008):
- 8-state FSM: S_OFF→S_VGL→S_VGL_WAIT→S_VGH→S_VGH_WAIT→S_AFE_AVDD→S_AFE_DVDD→S_READY
- T_STABLE_10MS = 1,000,000 cycles @ 100MHz
- Outputs: `vgl_en, vgh_en, afe_avdd_en, afe_dvdd_en, power_good`

---

## 3. Xilinx Primitive Handling Strategy (Architect Findings)

Verilator는 Xilinx 프리미티브를 직접 지원하지 않음. 모듈별 전략:

| Module | Xilinx Primitives | Verilator Strategy | xsim Strategy |
|--------|-------------------|-------------------|---------------|
| clk_rst_mgr | MMCME2_ADV | Behavioral wrapper (출력 클럭 직접 생성) | 실제 MMCM |
| line_data_rx | IBUFDS, ISERDESE2, IDELAYE2 | Behavioral wrapper (직렬→병렬 변환) | 실제 프리미티브 |
| CSI-2 LVDS TX | OSERDESE2 | Behavioral wrapper (병렬→직렬 변환) | 실제 프리미티브 |

Behavioral wrapper 위치: `sim/verilator/xilinx_behav/`

```
sim/verilator/xilinx_behav/
├── MMCME2_ADV_behav.sv    # MMCM behavioral: 입력 클럭 분주/체배
├── ISERDESE2_behav.sv     # ISERDES behavioral: DDR→8-bit 역직렬화
├── IDELAYE2_behav.sv      # IDELAY behavioral: 탭 지연 (고정)
├── IBUFDS_behav.sv        # IBUFDS behavioral: 차동→단일 변환
└── OSERDESE2_behav.sv     # OSERDES behavioral: 8-bit→DDR 직렬화
```

---

## 4. Detailed Trade-off Analysis (Architect Findings)

### SystemC vs Pure C++17

| Criterion | SystemC | Pure C++17 (채택) |
|-----------|---------|-------------------|
| 클럭 모델링 | sc_clock 내장 | ClockDomain 수동 구현 |
| CDC 모델링 | 네이티브 멀티클럭 | 추상화 (즉시 전송) |
| 학습 곡선 | 급경사 (SystemC 커널) | 최소 |
| Windows MSVC | 불안정 (POSIX 의존) | 완전 지원 |
| Verilator 통합 | DPI-C (동일) | DPI-C (동일) |

**결정**: Pure C++17. 팀 C++ 전문성, Windows 지원, 빌드 단순성이 결정적.

### DPI-C vs File-Based Test Vectors

**하이브리드 채택**:
- **DPI-C** (Verilator Stage 4): 동일 프로세스, 사이클별 비교, GDB 디버깅
- **File-based** (cocotb Stage 3): 시뮬레이터 독립적, Python에서 로드, 회귀 아티팩트

### Verilator vs xsim 역할 분리

- **Verilator** (primary): 알고리즘 검증, golden_compare, 빠른 회귀 (behavioral RTL)
- **xsim** (secondary): Xilinx 프리미티브 (ISERDESE2 등), 합성 후 시뮬레이션

### Key Design Decisions

1. **C++17** (structured bindings, constexpr if, std::optional)
2. **정수 연산 전용** — 부동소수점 미사용 (RTL과 일치)
3. **MSB-first** 직렬화 (ADI/TI 데이터시트 일치)
4. **CRC-16-CCITT**: 다항식 0x1021, init 0xFFFF (MIPI 표준)
5. **RegisterBank**: C++에서 shared 포인터, RTL에서 별도 모듈

---

## 5. Risk Assessment (Analyst Findings)

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| RISK-1: 모델링 충실도 (ISERDESE2 타이밍) | HIGH | MEDIUM | Behavioral 정확도로 제한; Vivado SDF로 타이밍 검증 |
| RISK-2: Verilator SV 호환성 (interface array) | MEDIUM | MEDIUM | 조기 테스트; workaround 문서화 |
| RISK-3: MSVC/GCC 비트필드 차이 | LOW | LOW | 고정폭 타입, 양 플랫폼 CI |
| RISK-4: 테스트 벡터 파일 크기 | LOW | LOW | 대표 벡터 커밋, 전체 프레임 런타임 생성 |
| RISK-5: cocotb + xsim 통합 불안정 | MEDIUM | LOW | Verilator primary, xsim secondary |
| RISK-6: 24-AFE 동시 시뮬레이션 성능 | MEDIUM | MEDIUM | 인스턴스 루프 최적화, 벡터화 |

---

Version: 1.0.0
Created: 2026-03-19
Team: researcher (haiku) + analyst (sonnet) + architect (opus)
