#pragma once

#include <vector>

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class RadiogModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    uint32_t start_ = 0;
    uint32_t xray_ready_ = 0;
    uint32_t xray_on_ = 0;
    uint32_t xray_off_ = 0;
    uint32_t dark_frame_mode_ = 0;
    uint32_t frame_valid_ = 0;
    uint32_t cfg_dark_cnt_ = 64;
    uint32_t cfg_tsettle_ = 100;
    uint32_t cfg_prep_timeout_ = 30;
    uint32_t state_ = 0;
    uint32_t xray_enable_ = 0;
    uint32_t error_ = 0;
    uint32_t done_ = 0;
    uint32_t dark_avg_ready_ = 0;
    uint32_t dark_frames_captured_ = 0;
    uint32_t timer_ = 0;
    uint32_t xray_seen_on_ = 0;
    uint32_t prev_frame_valid_ = 0;
    std::vector<uint16_t> frame_pixels_;
    std::vector<uint32_t> dark_accum_;
    std::vector<uint16_t> avg_dark_frame_;
};

}  // namespace fpd::sim
