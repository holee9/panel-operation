#pragma once

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class PanelFsmModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    uint32_t state_ = 0;
    uint32_t mode_ = 0;
    uint32_t nrows_ = 2048;
    uint32_t treset_ = 100;
    uint32_t tinteg_ = 1000;
    uint32_t nreset_ = 3;
    uint32_t sync_dly_ = 0;
    uint32_t tgate_settle_ = 0;
    uint32_t line_idx_ = 0;
    uint32_t timer_ = 0;
    uint32_t wait_timer_ = 0;
    uint32_t ctrl_start_ = 0;
    uint32_t ctrl_abort_ = 0;
    uint32_t gate_row_done_ = 0;
    uint32_t afe_config_done_ = 0;
    uint32_t afe_line_valid_ = 0;
    uint32_t xray_prep_req_ = 0;
    uint32_t xray_on_ = 0;
    uint32_t xray_off_ = 0;
    uint32_t prot_error_ = 0;
    uint32_t prot_force_stop_ = 0;
    uint32_t radiography_mode_ = 0;
    uint32_t busy_ = 0;
    uint32_t done_ = 0;
    uint32_t error_ = 0;
    uint32_t err_code_ = 0;
};

}  // namespace fpd::sim
