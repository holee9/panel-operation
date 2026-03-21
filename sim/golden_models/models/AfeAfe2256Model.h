#pragma once

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class AfeAfe2256Model : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    uint32_t afe_start_ = 0;
    uint32_t config_req_ = 0;
    uint32_t cfg_tline_ = 5120;
    uint32_t cfg_cic_en_ = 0;
    uint32_t cfg_pipeline_en_ = 0;
    uint32_t cfg_tp_sel_ = 0;
    uint32_t afe_ready_ = 0;
    uint32_t config_done_ = 0;
    uint32_t dout_window_valid_ = 0;
    uint32_t fclk_expected_ = 0;
    uint32_t line_count_ = 0;
};

}  // namespace fpd::sim
