#pragma once

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class RowScanModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    uint32_t scan_start_ = 0;
    uint32_t scan_abort_ = 0;
    uint32_t scan_dir_ = 0;
    uint32_t cfg_nrows_ = 2048;
    uint32_t row_index_ = 0;
    uint32_t gate_on_pulse_ = 0;
    uint32_t gate_settle_ = 0;
    uint32_t scan_active_ = 0;
    uint32_t row_done_ = 0;
    uint32_t scan_done_ = 0;
};

}  // namespace fpd::sim
