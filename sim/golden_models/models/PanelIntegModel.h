#pragma once

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class PanelIntegModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    uint32_t start_ = 0;
    uint32_t triggered_mode_ = 0;
    uint32_t radiography_mode_ = 0;
    uint32_t xray_prep_req_ = 0;
    uint32_t xray_on_ = 0;
    uint32_t xray_off_ = 0;
    uint32_t cfg_tinteg_ = 1000;
    uint32_t prep_timeout_ = 0;
    uint32_t xray_enable_ = 0;
    uint32_t integrate_active_ = 0;
    uint32_t exposure_done_ = 0;
    uint32_t timeout_error_ = 0;
    uint32_t timer_ = 0;
};

}  // namespace fpd::sim
