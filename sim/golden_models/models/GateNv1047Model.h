#pragma once

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class GateNv1047Model : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    uint32_t row_index_ = 0;
    uint32_t gate_on_pulse_ = 0;
    uint32_t scan_dir_ = 0;
    uint32_t reset_all_ = 0;
    uint32_t cfg_clk_period_ = 2200;
    uint32_t cfg_gate_on_ = 2200;
    uint32_t cfg_gate_settle_ = 100;
    uint32_t nv_sd1_ = 0;
    uint32_t nv_sd2_ = 0;
    uint32_t nv_clk_ = 0;
    uint32_t nv_oe_ = 1;
    uint32_t nv_ona_ = 1;
    uint32_t nv_lr_ = 0;
    uint32_t nv_rst_ = 1;
    uint32_t row_done_ = 0;
    uint32_t gate_on_prev_ = 0;
    uint32_t bbm_count_ = 0;
    uint32_t bbm_pending_ = 0;
    uint32_t clk_div_ = 0;
};

}  // namespace fpd::sim
