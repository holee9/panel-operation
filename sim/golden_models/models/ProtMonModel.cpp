#include "golden_models/models/ProtMonModel.h"

namespace fpd::sim {

void ProtMonModel::reset() {
    fsm_state_ = 0;
    xray_active_ = 0;
    cfg_max_exposure_ = 0;
    exposure_count_ = 0;
    err_timeout_ = 0;
    err_flag_ = 0;
    force_gate_off_ = 0;
    cycle_count_ = 0;
}

void ProtMonModel::step() {
    if (fsm_state_ == 2U && xray_active_ != 0U) {
        ++exposure_count_;
        if (exposure_count_ >= cfg_max_exposure_) {
            err_timeout_ = 1;
            err_flag_ = 1;
            force_gate_off_ = 1;
        }
    } else if (fsm_state_ == 0U) {
        exposure_count_ = 0;
        err_timeout_ = 0;
        err_flag_ = 0;
        force_gate_off_ = 0;
    }
    ++cycle_count_;
}

void ProtMonModel::set_inputs(const SignalMap& inputs) {
    fsm_state_ = GetScalar(inputs, "fsm_state", fsm_state_);
    xray_active_ = GetScalar(inputs, "xray_active", xray_active_);
    cfg_max_exposure_ = GetScalar(inputs, "cfg_max_exposure", cfg_max_exposure_);
}

SignalMap ProtMonModel::get_outputs() const {
    return {
        {"err_timeout", err_timeout_},
        {"err_flag", err_flag_},
        {"force_gate_off", force_gate_off_},
    };
}

std::vector<Mismatch> ProtMonModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void ProtMonModel::generate_vectors(const std::string& output_dir) {
    (void)output_dir;
}

}  // namespace fpd::sim
