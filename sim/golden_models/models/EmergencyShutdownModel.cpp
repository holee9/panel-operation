#include "golden_models/models/EmergencyShutdownModel.h"

namespace fpd::sim {

void EmergencyShutdownModel::reset() {
    vgh_over_ = 0;
    vgh_under_ = 0;
    temp_over_ = 0;
    pll_unlocked_ = 0;
    hw_emergency_n_ = 1;
    shutdown_req_ = 0;
    force_gate_off_ = 0;
    shutdown_code_ = 0;
    cycle_count_ = 0;
}

void EmergencyShutdownModel::step() {
    shutdown_req_ = 0;
    force_gate_off_ = 0;
    shutdown_code_ = 0;
    if (hw_emergency_n_ == 0U) {
        shutdown_req_ = 1;
        force_gate_off_ = 1;
        shutdown_code_ = 0xEEU;
    } else if (vgh_over_ != 0U) {
        shutdown_req_ = 1;
        force_gate_off_ = 1;
        shutdown_code_ = 1;
    } else if (temp_over_ != 0U) {
        shutdown_req_ = 1;
        force_gate_off_ = 1;
        shutdown_code_ = 2;
    } else if (pll_unlocked_ != 0U) {
        shutdown_req_ = 1;
        force_gate_off_ = 1;
        shutdown_code_ = 3;
    } else if (vgh_under_ != 0U) {
        shutdown_req_ = 1;
        force_gate_off_ = 1;
        shutdown_code_ = 4;
    }
    ++cycle_count_;
}

void EmergencyShutdownModel::set_inputs(const SignalMap& inputs) {
    vgh_over_ = GetScalar(inputs, "vgh_over", vgh_over_);
    vgh_under_ = GetScalar(inputs, "vgh_under", vgh_under_);
    temp_over_ = GetScalar(inputs, "temp_over", temp_over_);
    pll_unlocked_ = GetScalar(inputs, "pll_unlocked", pll_unlocked_);
    hw_emergency_n_ = GetScalar(inputs, "hw_emergency_n", hw_emergency_n_);
}

SignalMap EmergencyShutdownModel::get_outputs() const {
    return {
        {"shutdown_req", shutdown_req_},
        {"force_gate_off", force_gate_off_},
        {"shutdown_code", shutdown_code_},
    };
}

std::vector<Mismatch> EmergencyShutdownModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void EmergencyShutdownModel::generate_vectors(const std::string& output_dir) {
    (void)output_dir;
}

}  // namespace fpd::sim
