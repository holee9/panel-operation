#include "golden_models/models/PowerSeqModel.h"

#include <cmath>

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

namespace {

constexpr double kStepDtMs = 0.00001;
constexpr double kVghDelayMs = 5.0;
constexpr double kSlewRateVPerMs = 5.0;
constexpr double kTargetVgl = -10.0;
constexpr double kTargetVgh = 20.0;
constexpr double kVoltageTolerance = 0.05;

double MoveToward(const double current, const double target, const double max_delta) {
    const auto delta = target - current;
    if (std::abs(delta) <= max_delta) {
        return target;
    }

    return current + (delta > 0.0 ? max_delta : -max_delta);
}

bool NeedsVgh(const uint32_t target_mode) {
    return target_mode == 0U || target_mode == 1U || target_mode == 2U || target_mode == 5U;
}

bool NeedsVgl(const uint32_t target_mode) {
    return NeedsVgh(target_mode) || target_mode == 3U;
}

bool NeedsAvdd1(const uint32_t target_mode) {
    return target_mode != 4U;
}

bool NeedsAvdd2(const uint32_t target_mode) {
    return NeedsVgh(target_mode);
}

bool IsAtTarget(const double current, const double target) {
    return std::abs(current - target) <= kVoltageTolerance;
}

}  // namespace

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
    current_time_ms_ = 0.0;
    vgl_stable_time_ms_ = 0.0;
    vgl_voltage_ = 0.0;
    vgh_voltage_ = 0.0;
    state_ = PowerState::Off;
    cycle_count_ = 0;
}

void PowerSeqModel::step() {
    en_dvdd_ = 1;
    en_avdd1_ = NeedsAvdd1(target_mode_) ? 1U : 0U;
    en_avdd2_ = NeedsAvdd2(target_mode_) ? 1U : 0U;
    en_vgl_ = NeedsVgl(target_mode_) ? 1U : 0U;
    en_vgh_ = 0U;
    power_good_ = 0U;

    vgl_voltage_ = MoveToward(vgl_voltage_, en_vgl_ != 0U ? kTargetVgl : 0.0, kSlewRateVPerMs * kStepDtMs);

    if (en_vgl_ == 0U) {
        state_ = PowerState::Off;
        vgl_stable_time_ms_ = 0.0;
    }
    else if (!IsAtTarget(vgl_voltage_, kTargetVgl) || vgl_stable_ == 0U) {
        state_ = PowerState::VglRamp;
        vgl_stable_time_ms_ = 0.0;
    }
    else if (!NeedsVgh(target_mode_)) {
        state_ = PowerState::VghDelay;
        current_mode_ = target_mode_;
    }
    else if (state_ == PowerState::Off || state_ == PowerState::VglRamp) {
        state_ = PowerState::VghDelay;
        vgl_stable_time_ms_ = current_time_ms_;
    }

    if (NeedsVgh(target_mode_) && (state_ == PowerState::VghDelay || state_ == PowerState::VghRamp || state_ == PowerState::AllOn)) {
        if ((current_time_ms_ - vgl_stable_time_ms_) >= kVghDelayMs) {
            en_vgh_ = 1U;
            state_ = PowerState::VghRamp;
        }
    }

    vgh_voltage_ = MoveToward(vgh_voltage_, en_vgh_ != 0U ? kTargetVgh : 0.0, kSlewRateVPerMs * kStepDtMs);
    if (NeedsVgh(target_mode_) && en_vgh_ != 0U && IsAtTarget(vgh_voltage_, kTargetVgh) && vgh_stable_ != 0U) {
        state_ = PowerState::AllOn;
        current_mode_ = target_mode_;
        power_good_ = 1U;
    }
    else if (!NeedsVgh(target_mode_)) {
        vgh_voltage_ = MoveToward(vgh_voltage_, 0.0, kSlewRateVPerMs * kStepDtMs);
    }

    if (target_mode_ == 4U) {
        current_mode_ = 4U;
    }

    seq_error_ = (en_vgh_ != 0U && en_vgl_ == 0U) ? 1U : 0U;
    current_time_ms_ += kStepDtMs;
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
