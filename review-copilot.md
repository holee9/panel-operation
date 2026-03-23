# [review-copilot] SW Simulation Code Review (Revised v2)

- Review date: 2026-03-23
- Cross-verification date: 2026-03-23 (review-claude v8.0 교차검증 반영)
- Scope: sim/* (golden models, generators, unit tests, cocotb, Verilator scaffold, active build artifacts)
- Method: static review of current implementation and generated build state
- Note: This revision supersedes the earlier review. Several previously reported logic issues are fixed, and the remaining risks have shifted.
- v2 note: High-2 (vector path inconsistency) has been verified as RESOLVED by cross-verification.

## 1. Findings (ordered by severity)

### Critical-1: `sim/build_sa` is not reproducibly buildable due to malformed CMake compiler flags

- Evidence:
  - `sim/build_sa/CMakeCache.txt:26`
  - `sim/build_sa/CMakeFiles/CMakeConfigureLog.yaml:78`
  - `sim/build_sa/CMakeFiles/CMakeConfigureLog.yaml:102`
  - `sim/build_sa/CMakeFiles/CMakeConfigureLog.yaml:112`
- What is happening:
  - The active `build_sa` cache contains `CMAKE_CXX_FLAGS: "C:/Program Files/Git/W4 /analyze"`.
  - During `try_compile`, MSVC interprets `C:/Program` and `Files/Git/W4` as source files instead of `/W4`, producing `C1083` failures.
- Impact:
  - This build tree cannot be trusted as a valid regression environment.
  - Any claim that the current sim stack is buildable in `build_sa` is currently not reproducible.

### Critical-2: Verilator compare path remains scaffold-only and cannot perform RTL equivalence checks

- Evidence:
  - `sim/verilator/compare_spi.cpp:19`
  - `sim/verilator/compare_fsm.cpp:28`
  - `sim/verilator/compare_csi2.cpp:15`
  - `sim/verilator/golden_compare.cpp:20`
  - `sim/verilator/Makefile:1`
  - `sim/verilator/sim_main.cpp:6`
- What is happening:
  - Each `compare_*` target still supplies an RTL reader callback that returns `false`.
  - `RunGoldenCompare()` therefore aborts with `RTL signal reader is not bound` before any comparison occurs.
  - The local Verilator entrypoints are still explicit scaffolds rather than runnable integration code.
- Impact:
  - Golden-vs-RTL co-simulation is still unavailable.
  - Current simulation progress validates model behavior and selected tests, but not end-to-end RTL equivalence on this path.

### High-1: Vector regeneration — PARTIALLY RESOLVED (교차검증 2026-03-23)

- Evidence:
  - `sim/cocotb_tests/conftest.py:10`
  - `sim/golden_models/generators/gen_fsm_vectors.cpp:9`
  - `sim/golden_models/generators/gen_gate_vectors.cpp:9`
  - `sim/golden_models/generators/gen_afe_vectors.cpp:11`
  - `sim/golden_models/generators/gen_csi2_vectors.cpp:13`
  - `sim/golden_models/generators/gen_safety_vectors.cpp:11`
- Cross-verification result:
  - 5개 gen_*.cpp 모두 `sim/testvectors/spec*` 경로로 출력 확인 → **경로 통일 해결됨**
  - 잔여 이슈: gen_spi_vectors.cpp가 여전히 `golden_models/test_vectors/spec001`로 출력 가능 (미확인)
  - cocotb flat file (reg_bank_defaults.hex 등)과 생성기 파일명 매핑은 수동 확인 필요
- Impact:
  - 주요 경로 불일치는 해결됨. 파일명 매핑 자동화는 추후 개선 대상.

### Medium-1: A subset of cocotb coverage is still reset-smoke level rather than behavior-verifying

- Evidence:
  - `sim/cocotb_tests/test_spi_slave.py:5`
  - `sim/cocotb_tests/test_csi2_tx.py:5`
  - `sim/cocotb_tests/test_gate_nt39565d.py:5`
  - `sim/cocotb_tests/test_line_buf.py:5`
  - `sim/cocotb_tests/test_lvds_rx.py:5`
  - `sim/cocotb_tests/test_safety.py:5`
- What is happening:
  - These tests still rely on `Timer(...)`-based reset checks and minimal idle assertions.
  - Stronger clocked/vector-driven tests were added elsewhere, but coverage quality is still uneven across modules.
- Impact:
  - Test presence is improving faster than test discriminating power.
  - Some modules can still pass CI without exercising the meaningful state transitions introduced by the new models.

## 2. Resolved Since Previous Review

### Resolved-1: Previous Verilator self-compare false-pass risk is removed

- New status:
  - The compare path no longer self-feeds model outputs as fake RTL outputs.
  - The remaining issue is now an explicit abort, which is safer and easier to diagnose.
- Evidence:
  - `sim/verilator/compare_spi.cpp:19`
  - `sim/verilator/compare_fsm.cpp:28`
  - `sim/verilator/compare_csi2.cpp:15`

### Resolved-2: LineBuf multi-sample overwrite concern is fixed in the golden model

- New status:
  - The drain loop increments write address per sample, so burst-style writes now land on consecutive locations.
- Evidence:
  - `sim/golden_models/models/LineBufModel.cpp:58`
  - `sim/golden_models/models/LineBufModel.cpp:67`
  - `sim/tests/test_data_path_models.cpp:24`

### Resolved-3: Radiography ON/OFF ordering and dark-frame edge handling are fixed

- New status:
  - `xray_seen_on_` now gates OFF handling correctly.
  - Dark-frame capture is edge-qualified with `prev_frame_valid_`.
- Evidence:
  - `sim/golden_models/models/RadiogModel.cpp:96`
  - `sim/golden_models/models/RadiogModel.cpp:98`
  - `sim/golden_models/models/RadiogModel.cpp:111`
  - `sim/golden_models/models/RadiogModel.cpp:123`
  - `sim/tests/test_radiog_model.cpp:10`

### Resolved-4: Earlier weak cocotb examples for AFE2256 and NV1047 were upgraded

- New status:
  - These are now clock-driven and assert actual behavior rather than permissive values.
- Evidence:
  - `sim/cocotb_tests/test_afe_afe2256.py:6`
  - `sim/cocotb_tests/test_gate_nv1047.py:6`

## 3. Positive Changes Confirmed

1. Golden model scope expanded materially with PanelReset, PanelInteg, Radiog, AfeSpi, LvdsRx, DataOutMux, and McuDataIf coverage.
2. TestVector IO now supports both hex and binary read/write round-trip.
3. C++ unit tests now cover gate, AFE, data-path, panel auxiliary, and vector I/O behavior.
4. Most vector generators were moved toward `sim/testvectors`, which is an improvement over the earlier split-root layout.

## 4. Priority Recommendations

1. Fix the `build_sa` toolchain flags first so the active simulation build directory is actually reproducible.
2. Implement real RTL binding for `sim/verilator/compare_*.cpp` and replace the scaffold Verilator flow with a runnable one.
3. Unify vector policy completely: one output root, one naming scheme, and direct cocotb consumption of regenerated vectors.
4. Upgrade the remaining reset-smoke cocotb tests to clocked, stateful checks that validate the new model behavior.
5. Keep architecture-level RTL completeness items separate from this SW sim review so simulation closure status remains clear.

## 5. Cross-Verification Results (2026-03-23, review-claude v8.0)

교차검증 시 확인된 사항:

| Copilot 항목 | 교차검증 결과 |
|-------------|--------------|
| Critical-1 (Verilator) | **확인됨** — compare_*.cpp false callback, RTL 비운영 상태 유지 |
| High-1 (벡터 경로) | **부분 해결** — 5개 gen_*.cpp sim/testvectors/ 출력 확인, gen_spi 미확인 |
| High-2 (accept-all) | **해결됨** — afe2256, nv1047 기능 검증 전환 코드 확인 |
| Medium-1 (스모크 테스트) | **유지** — 6개 테스트 여전히 reset-smoke 수준 |
| Resolved-2 (LineBuf) | **확인됨** — ++write_addr L67 로컬 변수 증분 (외부 wr_addr_ 미변경 주의) |
| Resolved-3 (Radiog) | **확인됨** — xray_seen_on_ latch L95-98 (상태머신 분리는 미완) |

---

This revision reflects the latest code state as of 2026-03-23 and replaces earlier findings that are already fixed in current sources.
Cross-verification performed by MoAI Code Review Pipeline v8.0 against review-claude.md v8.0.
