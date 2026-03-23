#pragma once

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class ProtMonModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    uint32_t fsm_state_ = 0;
    uint32_t xray_active_ = 0;
    uint32_t cfg_max_exposure_ = 0;
    uint32_t radiography_mode_ = 0;
    uint32_t exposure_count_ = 0;
    uint32_t err_timeout_ = 0;
    uint32_t err_flag_ = 0;
    uint32_t force_gate_off_ = 0;

    // Dual timeout thresholds (in exposure count units)
    static constexpr uint32_t kDefaultTimeout = 500000U;   // ~5s at 100MHz
    static constexpr uint32_t kRadiogTimeout  = 3000000U;  // ~30s at 100MHz
};

}  // namespace fpd::sim
