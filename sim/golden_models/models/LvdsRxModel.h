#pragma once

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class LvdsRxModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    uint32_t rx_enable_ = 0;
    uint32_t bitslip_req_ = 0;
    uint32_t lvds_dout_a_ = 0;
    uint32_t lvds_dout_b_ = 0;
    uint32_t lvds_fclk_ = 0;
    uint32_t pixel_data_ = 0;
    uint32_t pixel_valid_ = 0;
    uint32_t pixel_col_idx_ = 0;
    uint32_t line_complete_ = 0;
    uint32_t shift_reg_ = 0;
    uint32_t bit_count_ = 0;
    uint32_t max_channels_ = 256;
};

}  // namespace fpd::sim
