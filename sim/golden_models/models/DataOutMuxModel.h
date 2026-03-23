#pragma once

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class DataOutMuxModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    uint32_t cfg_ncols_ = 2048;
    uint32_t line_pixel_data_ = 0;
    uint32_t line_data_valid_ = 0;
    uint32_t line_pixel_idx_ = 0;
    uint32_t mcu_pixel_data_ = 0;
    uint32_t mcu_data_valid_ = 0;
    uint32_t mcu_line_start_ = 0;
    uint32_t mcu_line_end_ = 0;
};

}  // namespace fpd::sim
