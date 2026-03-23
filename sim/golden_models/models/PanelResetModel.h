#pragma once

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class PanelResetModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    uint32_t start_ = 0;
    uint32_t cfg_nreset_ = 3;
    uint32_t cfg_nrows_ = 2048;
    uint32_t reset_done_ = 0;
    uint32_t busy_ = 0;
    uint32_t dummy_scan_count_ = 0;
    uint32_t row_index_ = 0;
};

}  // namespace fpd::sim
