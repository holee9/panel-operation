#include "golden_models/models/RowScanModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

void RowScanModel::reset() {
    scan_start_ = 0;
    scan_abort_ = 0;
    scan_dir_ = 0;
    cfg_nrows_ = 2048;
    row_index_ = 0;
    gate_on_pulse_ = 0;
    gate_settle_ = 0;
    scan_active_ = 0;
    row_done_ = 0;
    scan_done_ = 0;
    cycle_count_ = 0;
}

void RowScanModel::step() {
    row_done_ = 0;
    scan_done_ = 0;
    if (scan_abort_ != 0U) {
        scan_active_ = 0;
        gate_on_pulse_ = 0;
        gate_settle_ = 0;
    } else if (!scan_active_ && scan_start_ != 0U) {
        scan_active_ = 1;
        row_index_ = (scan_dir_ != 0U) ? (cfg_nrows_ - 1U) : 0U;
        gate_on_pulse_ = 1;
    } else if (scan_active_ && gate_on_pulse_ != 0U) {
        gate_on_pulse_ = 0;
        gate_settle_ = 1;
    } else if (scan_active_ && gate_settle_ != 0U) {
        gate_settle_ = 0;
        row_done_ = 1;
        if ((scan_dir_ != 0U && row_index_ == 0U) ||
            (scan_dir_ == 0U && row_index_ + 1U >= cfg_nrows_)) {
            scan_active_ = 0;
            scan_done_ = 1;
        } else {
            row_index_ = (scan_dir_ != 0U) ? (row_index_ - 1U) : (row_index_ + 1U);
            gate_on_pulse_ = 1;
        }
    }
    ++cycle_count_;
}

void RowScanModel::set_inputs(const SignalMap& inputs) {
    scan_start_ = GetScalar(inputs, "scan_start", scan_start_);
    scan_abort_ = GetScalar(inputs, "scan_abort", scan_abort_);
    scan_dir_ = GetScalar(inputs, "scan_dir", scan_dir_);
    cfg_nrows_ = GetScalar(inputs, "cfg_nrows", cfg_nrows_);
}

SignalMap RowScanModel::get_outputs() const {
    return {
        {"row_index", row_index_},
        {"gate_on_pulse", gate_on_pulse_},
        {"gate_settle", gate_settle_},
        {"scan_active", scan_active_},
        {"row_done", row_done_},
        {"scan_done", scan_done_},
    };
}

std::vector<Mismatch> RowScanModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void RowScanModel::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "row_scan_eng";
    vectors.spec_name = "SPEC-FPD-003";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {"scan_start", "scan_abort", "scan_dir", "cfg_nrows"};
    vectors.signal_outputs = {"row_index", "gate_on_pulse", "gate_settle", "scan_active", "row_done", "scan_done"};
    reset();
    set_inputs({{"scan_start", 1U}, {"scan_dir", 0U}, {"cfg_nrows", 2U}});
    for (int i = 0; i < 4; ++i) {
        step();
        vectors.vectors.push_back({cycle(), {{"scan_start", 1U}, {"scan_dir", 0U}, {"cfg_nrows", 2U}}, get_outputs()});
    }
    WriteHexVectors(vectors, output_dir + "/row_scan_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/row_scan_vectors.bin");
}

}  // namespace fpd::sim
