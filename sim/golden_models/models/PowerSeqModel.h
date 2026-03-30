#pragma once

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class PowerSeqModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

    double vgl_voltage() const { return vgl_voltage_; }
    double vgh_voltage() const { return vgh_voltage_; }

private:
    enum class PowerState : uint8_t {
        Off = 0,
        VglRamp,
        VghDelay,
        VghRamp,
        AllOn,
    };

    uint32_t target_mode_ = 0;
    uint32_t vgl_stable_ = 0;
    uint32_t vgh_stable_ = 0;
    uint32_t current_mode_ = 0;
    uint32_t en_vgl_ = 0;
    uint32_t en_vgh_ = 0;
    uint32_t en_avdd1_ = 0;
    uint32_t en_avdd2_ = 0;
    uint32_t en_dvdd_ = 0;
    uint32_t power_good_ = 0;
    uint32_t seq_error_ = 0;
    double current_time_ms_ = 0.0;
    double vgl_stable_time_ms_ = 0.0;
    double vgl_voltage_ = 0.0;
    double vgh_voltage_ = 0.0;
    PowerState state_ = PowerState::Off;
};

}  // namespace fpd::sim
