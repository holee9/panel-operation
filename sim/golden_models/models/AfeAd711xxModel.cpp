#include "golden_models/models/AfeAd711xxModel.h"

#include <algorithm>

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

namespace {

uint32_t ComboDefaultNCols(uint32_t combo) {
    switch (combo) {
        case 4U:
        case 5U:
            return 1664U;
        case 6U:
        case 7U:
            return 3072U;
        default:
            return 2048U;
    }
}

uint32_t MinTLine(uint32_t afe_type) {
    return (afe_type == 1U) ? 6000U : 2200U;
}

uint32_t EffectiveIfs(uint32_t afe_type, uint32_t ifs) {
    return (afe_type == 1U) ? (ifs & 0x1FU) : (ifs & 0x3FU);
}

std::vector<uint16_t> MakeSampleLine(uint32_t channels, uint32_t seed) {
    std::vector<uint16_t> samples(channels);
    for (uint32_t index = 0; index < channels; ++index) {
        samples[index] = static_cast<uint16_t>((seed + index * 17U) & 0xFFFFU);
    }
    return samples;
}

}  // namespace

void AfeAd711xxModel::reset() {
    afe_start_ = 0;
    config_req_ = 0;
    cfg_combo_ = 1;
    cfg_tline_ = 0;
    cfg_ifs_ = 0;
    cfg_lpf_ = 0;
    cfg_pmode_ = 0;
    cfg_nchip_ = 1;
    afe_type_ = 0;
    afe_ready_ = 0;
    config_done_ = 0;
    dout_window_valid_ = 0;
    line_count_ = 0;
    tline_error_ = 0;
    ifs_width_error_ = 0;
    expected_ncols_ = 2048;
    sample_line_.clear();
    cycle_count_ = 0;
}

void AfeAd711xxModel::step() {
    config_done_ = 0;
    expected_ncols_ = ComboDefaultNCols(cfg_combo_);
    const uint32_t effective_tline = (cfg_tline_ == 0U) ? MinTLine(afe_type_) : cfg_tline_;
    const uint32_t effective_nchip = (cfg_nchip_ == 0U) ? 1U : cfg_nchip_;
    const uint32_t effective_ifs = EffectiveIfs(afe_type_, cfg_ifs_);
    tline_error_ = (effective_tline < MinTLine(afe_type_)) ? 1U : 0U;
    ifs_width_error_ = (afe_type_ == 1U && cfg_ifs_ > 31U) ? 1U : 0U;
    if (config_req_ != 0U && afe_ready_ == 0U) {
        afe_ready_ = 1U;
        config_done_ = 1U;
    }

    if (afe_start_ != 0U && afe_ready_ != 0U && tline_error_ == 0U) {
        dout_window_valid_ = 1U;
        ++line_count_;
        const uint32_t line_channels = std::min(expected_ncols_, 256U * effective_nchip);
        sample_line_ = MakeSampleLine(line_channels, effective_ifs + (effective_nchip << 8U));
        if (line_count_ >= effective_tline) {
            line_count_ = 0U;
            dout_window_valid_ = 0U;
        }
    } else {
        dout_window_valid_ = 0U;
        line_count_ = 0U;
    }
    ++cycle_count_;
}

void AfeAd711xxModel::set_inputs(const SignalMap& inputs) {
    afe_start_ = GetScalar(inputs, "afe_start", afe_start_);
    config_req_ = GetScalar(inputs, "config_req", config_req_);
    cfg_combo_ = GetScalar(inputs, "cfg_combo", cfg_combo_);
    cfg_tline_ = GetScalar(inputs, "cfg_tline", cfg_tline_);
    cfg_ifs_ = GetScalar(inputs, "cfg_ifs", cfg_ifs_);
    cfg_lpf_ = GetScalar(inputs, "cfg_lpf", cfg_lpf_);
    cfg_pmode_ = GetScalar(inputs, "cfg_pmode", cfg_pmode_);
    cfg_nchip_ = GetScalar(inputs, "cfg_nchip", cfg_nchip_);
    afe_type_ = GetScalar(inputs, "afe_type", afe_type_);
}

SignalMap AfeAd711xxModel::get_outputs() const {
    return {
        {"config_done", config_done_},
        {"afe_ready", afe_ready_},
        {"dout_window_valid", dout_window_valid_},
        {"tline_error", tline_error_},
        {"ifs_width_error", ifs_width_error_},
        {"expected_ncols", expected_ncols_},
        {"cfg_mix", (EffectiveIfs(afe_type_, cfg_ifs_) ^ cfg_lpf_ ^ cfg_pmode_ ^ cfg_nchip_) & 0xFFU},
        {"line_pixels", sample_line_},
    };
}

std::vector<Mismatch> AfeAd711xxModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void AfeAd711xxModel::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "afe_ad711xx";
    vectors.spec_name = "SPEC-FPD-005";
    vectors.clock_name = "aclk";
    vectors.signal_inputs = {"afe_start", "config_req", "cfg_combo", "cfg_tline", "cfg_ifs", "cfg_nchip", "afe_type"};
    vectors.signal_outputs = {
        "config_done", "afe_ready", "dout_window_valid", "tline_error", "ifs_width_error", "expected_ncols"
    };

    reset();
    set_inputs({
        {"config_req", 1U},
        {"cfg_combo", 1U},
        {"cfg_tline", 2200U},
        {"cfg_ifs", 3U},
        {"cfg_nchip", 1U},
        {"afe_type", 0U},
    });
    step();
    vectors.vectors.push_back({cycle(), {
        {"config_req", 1U}, {"cfg_combo", 1U}, {"cfg_tline", 2200U}, {"cfg_ifs", 3U}, {"cfg_nchip", 1U}, {"afe_type", 0U}
    }, get_outputs()});

    set_inputs({
        {"afe_start", 1U},
        {"cfg_combo", 6U},
        {"cfg_tline", 6000U},
        {"cfg_ifs", 37U},
        {"cfg_nchip", 12U},
        {"afe_type", 1U},
    });
    step();
    vectors.vectors.push_back({cycle(), {
        {"afe_start", 1U}, {"cfg_combo", 6U}, {"cfg_tline", 6000U}, {"cfg_ifs", 37U}, {"cfg_nchip", 12U}, {"afe_type", 1U}
    }, get_outputs()});

    WriteHexVectors(vectors, output_dir + "/afe_ad711xx_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/afe_ad711xx_vectors.bin");
}

}  // namespace fpd::sim
