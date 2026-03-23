#pragma once

#include <vector>

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class AfeAd711xxModel : public GoldenModelBase {
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
    uint32_t cfg_combo_ = 1;
    uint32_t cfg_tline_ = 0;
    uint32_t cfg_ifs_ = 0;
    uint32_t cfg_lpf_ = 0;
    uint32_t cfg_pmode_ = 0;
    uint32_t cfg_nchip_ = 1;
    uint32_t afe_type_ = 0;
    uint32_t afe_ready_ = 0;
    uint32_t config_done_ = 0;
    uint32_t dout_window_valid_ = 0;
    uint32_t line_count_ = 0;
    uint32_t tline_error_ = 0;
    uint32_t ifs_width_error_ = 0;
    uint32_t expected_ncols_ = 2048;
    std::vector<uint16_t> sample_line_;
};

}  // namespace fpd::sim
