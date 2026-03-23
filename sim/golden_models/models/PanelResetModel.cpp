#include "golden_models/models/PanelResetModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

void PanelResetModel::reset() {
    start_ = 0;
    cfg_nreset_ = 3;
    cfg_nrows_ = 2048;
    reset_done_ = 0;
    busy_ = 0;
    dummy_scan_count_ = 0;
    row_index_ = 0;
    cycle_count_ = 0;
}

void PanelResetModel::step() {
    reset_done_ = 0;
    if (start_ != 0U) {
        busy_ = 1U;
        if (row_index_ + 1U >= cfg_nrows_) {
            row_index_ = 0U;
            ++dummy_scan_count_;
            if (dummy_scan_count_ >= cfg_nreset_) {
                reset_done_ = 1U;
                busy_ = 0U;
                dummy_scan_count_ = 0U;
            }
        } else {
            ++row_index_;
        }
    } else {
        busy_ = 0U;
        row_index_ = 0U;
        dummy_scan_count_ = 0U;
    }
    ++cycle_count_;
}

void PanelResetModel::set_inputs(const SignalMap& inputs) {
    start_ = GetScalar(inputs, "start", start_);
    cfg_nreset_ = GetScalar(inputs, "cfg_nreset", cfg_nreset_);
    cfg_nrows_ = GetScalar(inputs, "cfg_nrows", cfg_nrows_);
}

SignalMap PanelResetModel::get_outputs() const {
    return {
        {"reset_done", reset_done_},
        {"busy", busy_},
        {"dummy_scan_count", dummy_scan_count_},
        {"row_index", row_index_},
    };
}

std::vector<Mismatch> PanelResetModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void PanelResetModel::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "panel_reset_ctrl";
    vectors.spec_name = "SPEC-FPD-002";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {"start", "cfg_nreset", "cfg_nrows"};
    vectors.signal_outputs = {"reset_done", "busy", "dummy_scan_count", "row_index"};

    reset();
    set_inputs({{"start", 1U}, {"cfg_nreset", 3U}, {"cfg_nrows", 4U}});
    for (int i = 0; i < 5; ++i) {
        step();
        vectors.vectors.push_back({cycle(), {{"start", 1U}, {"cfg_nreset", 3U}, {"cfg_nrows", 4U}}, get_outputs()});
    }
    WriteHexVectors(vectors, output_dir + "/panel_reset_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/panel_reset_vectors.bin");
}

}  // namespace fpd::sim
