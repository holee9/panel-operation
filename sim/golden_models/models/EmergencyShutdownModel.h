#pragma once

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class EmergencyShutdownModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    uint32_t vgh_over_ = 0;
    uint32_t vgh_under_ = 0;
    uint32_t temp_over_ = 0;
    uint32_t pll_unlocked_ = 0;
    uint32_t hw_emergency_n_ = 1;
    uint32_t shutdown_req_ = 0;
    uint32_t force_gate_off_ = 0;
    uint32_t shutdown_code_ = 0;
};

}  // namespace fpd::sim
