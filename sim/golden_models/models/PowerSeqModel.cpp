#include "golden_models/models/PowerSeqModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

void PowerSeqModel::reset() {
    target_mode_ = 0;
    vgl_stable_ = 0;
    vgh_stable_ = 0;
    current_mode_ = 0;
    en_vgl_ = 0;
    en_vgh_ = 0;
    en_avdd1_ = 0;
    en_avdd2_ = 0;
    en_dvdd_ = 0;
    power_good_ = 0;
    seq_error_ = 0;
    cycle_count_ = 0;
}

void PowerSeqModel::step() {
    en_dvdd_ = 1;
    en_avdd1_ = (target_mode_ <= 5U) ? 1U : 0U;
    en_avdd2_ = (target_mode_ <= 2U) ? 1U : 0U;
    en_vgl_ = (target_mode_ <= 3U) ? 1U : 0U;
    en_vgh_ = en_vgl_ && vgl_stable_;
    current_mode_ = target_mode_;
    power_good_ = en_vgh_ && vgh_stable_;
    seq_error_ = (en_vgh_ != 0U && en_vgl_ == 0U) ? 1U : 0U;
    ++cycle_count_;
}

void PowerSeqModel::set_inputs(const SignalMap& inputs) {
    target_mode_ = GetScalar(inputs, "target_mode", target_mode_);
    vgl_stable_ = GetScalar(inputs, "vgl_stable", vgl_stable_);
    vgh_stable_ = GetScalar(inputs, "vgh_stable", vgh_stable_);
}

SignalMap PowerSeqModel::get_outputs() const {
    return {
        {"current_mode", current_mode_},
        {"en_vgl", en_vgl_},
        {"en_vgh", en_vgh_},
        {"en_avdd1", en_avdd1_},
        {"en_avdd2", en_avdd2_},
        {"en_dvdd", en_dvdd_},
        {"power_good", power_good_},
        {"seq_error", seq_error_},
    };
}

std::vector<Mismatch> PowerSeqModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void PowerSeqModel::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "power_sequencer";
    vectors.spec_name = "SPEC-FPD-008";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {"target_mode", "vgl_stable", "vgh_stable"};
    vectors.signal_outputs = {"current_mode", "en_vgl", "en_vgh", "power_good", "seq_error"};
    reset();
    set_inputs({{"target_mode", 1U}, {"vgl_stable", 1U}, {"vgh_stable", 1U}});
    step();
    vectors.vectors.push_back({cycle(), {{"target_mode", 1U}, {"vgl_stable", 1U}, {"vgh_stable", 1U}}, get_outputs()});
    WriteHexVectors(vectors, output_dir + "/power_seq_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/power_seq_vectors.bin");
}

}  // namespace fpd::sim
