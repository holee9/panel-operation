#include "golden_models/models/AfeAfe2256Model.h"

namespace fpd::sim {

void AfeAfe2256Model::reset() {
    afe_start_ = 0;
    config_req_ = 0;
    cfg_tline_ = 5120;
    cfg_cic_en_ = 0;
    cfg_pipeline_en_ = 0;
    cfg_tp_sel_ = 0;
    afe_ready_ = 0;
    config_done_ = 0;
    dout_window_valid_ = 0;
    fclk_expected_ = 0;
    line_count_ = 0;
    cycle_count_ = 0;
}

void AfeAfe2256Model::step() {
    config_done_ = 0;
    if (config_req_ != 0U && afe_ready_ == 0U) {
        afe_ready_ = 1;
        config_done_ = 1;
    }
    if (afe_start_ != 0U && afe_ready_ != 0U) {
        dout_window_valid_ = 1;
        fclk_expected_ = 1;
        ++line_count_;
        if (line_count_ >= cfg_tline_) {
            line_count_ = 0;
            dout_window_valid_ = 0;
            fclk_expected_ = 0;
        }
    } else {
        dout_window_valid_ = 0;
        fclk_expected_ = 0;
        line_count_ = 0;
    }
    ++cycle_count_;
}

void AfeAfe2256Model::set_inputs(const SignalMap& inputs) {
    afe_start_ = GetScalar(inputs, "afe_start", afe_start_);
    config_req_ = GetScalar(inputs, "config_req", config_req_);
    cfg_tline_ = GetScalar(inputs, "cfg_tline", cfg_tline_);
    cfg_cic_en_ = GetScalar(inputs, "cfg_cic_en", cfg_cic_en_);
    cfg_pipeline_en_ = GetScalar(inputs, "cfg_pipeline_en", cfg_pipeline_en_);
    cfg_tp_sel_ = GetScalar(inputs, "cfg_tp_sel", cfg_tp_sel_);
}

SignalMap AfeAfe2256Model::get_outputs() const {
    return {
        {"config_done", config_done_},
        {"afe_ready", afe_ready_},
        {"dout_window_valid", dout_window_valid_},
        {"fclk_expected", fclk_expected_},
        {"cfg_mix", (cfg_cic_en_ ^ cfg_pipeline_en_ ^ cfg_tp_sel_) & 0x1U},
    };
}

std::vector<Mismatch> AfeAfe2256Model::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void AfeAfe2256Model::generate_vectors(const std::string& output_dir) {
    (void)output_dir;
}

}  // namespace fpd::sim
