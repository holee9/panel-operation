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

private:
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
};

}  // namespace fpd::sim
