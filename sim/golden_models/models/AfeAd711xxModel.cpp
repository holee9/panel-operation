#include "golden_models/models/AfeAd711xxModel.h"

namespace fpd::sim {

void AfeAd711xxModel::reset() {
    afe_start_ = 0;
    config_req_ = 0;
    cfg_tline_ = 2200;
    cfg_ifs_ = 0;
    cfg_lpf_ = 0;
    cfg_pmode_ = 0;
    afe_ready_ = 0;
    config_done_ = 0;
    dout_window_valid_ = 0;
    line_count_ = 0;
    cycle_count_ = 0;
}

void AfeAd711xxModel::step() {
    config_done_ = 0;
    if (config_req_ != 0U && afe_ready_ == 0U) {
        afe_ready_ = 1;
        config_done_ = 1;
    }
    if (afe_start_ != 0U && afe_ready_ != 0U) {
        dout_window_valid_ = 1;
        ++line_count_;
        if (line_count_ >= cfg_tline_) {
            line_count_ = 0;
            dout_window_valid_ = 0;
        }
    } else {
        dout_window_valid_ = 0;
        line_count_ = 0;
    }
    ++cycle_count_;
}

void AfeAd711xxModel::set_inputs(const SignalMap& inputs) {
    afe_start_ = GetScalar(inputs, "afe_start", afe_start_);
    config_req_ = GetScalar(inputs, "config_req", config_req_);
    cfg_tline_ = GetScalar(inputs, "cfg_tline", cfg_tline_);
    cfg_ifs_ = GetScalar(inputs, "cfg_ifs", cfg_ifs_);
    cfg_lpf_ = GetScalar(inputs, "cfg_lpf", cfg_lpf_);
    cfg_pmode_ = GetScalar(inputs, "cfg_pmode", cfg_pmode_);
}

SignalMap AfeAd711xxModel::get_outputs() const {
    return {
        {"config_done", config_done_},
        {"afe_ready", afe_ready_},
        {"dout_window_valid", dout_window_valid_},
        {"cfg_mix", (cfg_ifs_ ^ cfg_lpf_ ^ cfg_pmode_) & 0xFFU},
    };
}

std::vector<Mismatch> AfeAd711xxModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void AfeAd711xxModel::generate_vectors(const std::string& output_dir) {
    (void)output_dir;
}

}  // namespace fpd::sim
