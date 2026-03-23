#include "golden_models/models/ProtMonModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

void ProtMonModel::reset() {
    fsm_state_ = 0;
    xray_active_ = 0;
    cfg_max_exposure_ = 0;
    radiography_mode_ = 0;
    exposure_count_ = 0;
    err_timeout_ = 0;
    err_flag_ = 0;
    force_gate_off_ = 0;
    cycle_count_ = 0;
}

void ProtMonModel::step() {
    // Dual timeout: radiography mode uses extended limit (30s),
    // normal mode uses short limit (5s).
    const uint32_t effective_limit =
        (cfg_max_exposure_ != 0U)
            ? cfg_max_exposure_
            : (radiography_mode_ != 0U ? kRadiogTimeout : kDefaultTimeout);

    if ((fsm_state_ == 4U || fsm_state_ == 5U) && xray_active_ != 0U) {
        ++exposure_count_;
        if (exposure_count_ >= effective_limit) {
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
    radiography_mode_ = GetScalar(inputs, "radiography_mode", radiography_mode_);
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
    TestVectorFile vectors;
    vectors.module_name = "prot_mon";
    vectors.spec_name = "SPEC-FPD-008";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {
        "fsm_state", "xray_active", "cfg_max_exposure", "radiography_mode"};
    vectors.signal_outputs = {"err_timeout", "err_flag", "force_gate_off"};

    // Normal mode timeout scenario
    reset();
    set_inputs({{"fsm_state", 5U},
                {"xray_active", 1U},
                {"cfg_max_exposure", 2U},
                {"radiography_mode", 0U}});
    step();
    vectors.vectors.push_back(
        {cycle(),
         {{"fsm_state", 5U},
          {"xray_active", 1U},
          {"cfg_max_exposure", 2U},
          {"radiography_mode", 0U}},
         get_outputs()});

    // Radiography mode: same cfg_max_exposure but mode flag set
    reset();
    set_inputs({{"fsm_state", 5U},
                {"xray_active", 1U},
                {"cfg_max_exposure", 2U},
                {"radiography_mode", 1U}});
    step();
    vectors.vectors.push_back(
        {cycle(),
         {{"fsm_state", 5U},
          {"xray_active", 1U},
          {"cfg_max_exposure", 2U},
          {"radiography_mode", 1U}},
         get_outputs()});

    WriteHexVectors(vectors, output_dir + "/prot_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/prot_vectors.bin");
}

}  // namespace fpd::sim
