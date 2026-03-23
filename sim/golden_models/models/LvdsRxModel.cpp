#include "golden_models/models/LvdsRxModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

void LvdsRxModel::reset() {
    rx_enable_ = 0;
    bitslip_req_ = 0;
    lvds_dout_a_ = 0;
    lvds_dout_b_ = 0;
    lvds_fclk_ = 0;
    pixel_data_ = 0;
    pixel_valid_ = 0;
    pixel_col_idx_ = 0;
    line_complete_ = 0;
    shift_reg_ = 0;
    bit_count_ = 0;
    max_channels_ = 256;
    cycle_count_ = 0;
}

void LvdsRxModel::step() {
    pixel_valid_ = 0U;
    line_complete_ = 0U;
    if (rx_enable_ == 0U) {
        bit_count_ = 0U;
        pixel_col_idx_ = 0U;
        ++cycle_count_;
        return;
    }

    if (lvds_fclk_ != 0U) {
        bit_count_ = 0U;
        pixel_col_idx_ = 0U;
    }

    shift_reg_ = ((shift_reg_ << 2U) | ((lvds_dout_a_ & 0x1U) << 1U) | (lvds_dout_b_ & 0x1U)) & 0xFFFFU;
    if (bitslip_req_ != 0U) {
        shift_reg_ = ((shift_reg_ << 1U) | (shift_reg_ >> 15U)) & 0xFFFFU;
    }
    if (bit_count_ == 7U) {
        pixel_data_ = shift_reg_;
        pixel_valid_ = 1U;
        if (pixel_col_idx_ + 1U >= max_channels_) {
            line_complete_ = 1U;
            pixel_col_idx_ = 0U;
        } else {
            ++pixel_col_idx_;
        }
        bit_count_ = 0U;
    } else {
        ++bit_count_;
    }
    ++cycle_count_;
}

void LvdsRxModel::set_inputs(const SignalMap& inputs) {
    rx_enable_ = GetScalar(inputs, "rx_enable", rx_enable_);
    bitslip_req_ = GetScalar(inputs, "bitslip_req", bitslip_req_);
    lvds_dout_a_ = GetScalar(inputs, "lvds_dout_a", lvds_dout_a_);
    lvds_dout_b_ = GetScalar(inputs, "lvds_dout_b", lvds_dout_b_);
    lvds_fclk_ = GetScalar(inputs, "lvds_fclk", lvds_fclk_);
    max_channels_ = GetScalar(inputs, "max_channels", max_channels_);
}

SignalMap LvdsRxModel::get_outputs() const {
    return {
        {"pixel_data", pixel_data_},
        {"pixel_valid", pixel_valid_},
        {"pixel_col_idx", pixel_col_idx_},
        {"line_complete", line_complete_},
    };
}

std::vector<Mismatch> LvdsRxModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void LvdsRxModel::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "line_data_rx";
    vectors.spec_name = "SPEC-FPD-007";
    vectors.clock_name = "dclk";
    vectors.signal_inputs = {"rx_enable", "bitslip_req", "lvds_dout_a", "lvds_dout_b", "lvds_fclk", "max_channels"};
    vectors.signal_outputs = {"pixel_data", "pixel_valid", "pixel_col_idx", "line_complete"};
    reset();
    set_inputs({{"rx_enable", 1U}, {"lvds_dout_a", 1U}, {"lvds_dout_b", 0U}, {"max_channels", 4U}});
    for (int i = 0; i < 8; ++i) {
        step();
        vectors.vectors.push_back({cycle(), {{"rx_enable", 1U}, {"lvds_dout_a", 1U}, {"lvds_dout_b", 0U}, {"max_channels", 4U}}, get_outputs()});
    }
    WriteHexVectors(vectors, output_dir + "/lvds_rx_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/lvds_rx_vectors.bin");
}

}  // namespace fpd::sim
