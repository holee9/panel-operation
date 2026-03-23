#include "golden_models/models/EmergencyShutdownModel.h"

#include "golden_models/core/TestVectorIO.h"

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
    TestVectorFile vectors;
    vectors.module_name = "emergency_shutdown";
    vectors.spec_name = "SPEC-FPD-008";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {"vgh_over", "vgh_under", "temp_over", "pll_unlocked", "hw_emergency_n"};
    vectors.signal_outputs = {"shutdown_req", "force_gate_off", "shutdown_code"};
    reset();
    set_inputs({{"vgh_over", 1U}, {"hw_emergency_n", 1U}});
    step();
    vectors.vectors.push_back({cycle(), {{"vgh_over", 1U}, {"hw_emergency_n", 1U}}, get_outputs()});
    WriteHexVectors(vectors, output_dir + "/emergency_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/emergency_vectors.bin");
}

}  // namespace fpd::sim
