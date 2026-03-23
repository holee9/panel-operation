#include "golden_models/models/AfeAfe2256Model.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

namespace {

constexpr uint32_t kAfe2256MinTLine = 5120U;

std::vector<uint16_t> MakeAfe2256Row(uint32_t profile, uint32_t seed) {
    std::vector<uint16_t> row(256U);
    const uint16_t gain = static_cast<uint16_t>((profile + 1U) * 7U);
    for (uint32_t index = 0; index < row.size(); ++index) {
        row[index] = static_cast<uint16_t>((seed + index * gain) & 0xFFFFU);
    }
    return row;
}

}  // namespace

void AfeAfe2256Model::reset() {
    afe_start_ = 0;
    config_req_ = 0;
    cfg_tline_ = kAfe2256MinTLine;
    cfg_cic_en_ = 0;
    cfg_cic_profile_ = 0;
    cfg_pipeline_en_ = 0;
    cfg_tp_sel_ = 0;
    cfg_nchip_ = 1;
    afe_ready_ = 0;
    config_done_ = 0;
    dout_window_valid_ = 0;
    fclk_expected_ = 0;
    line_count_ = 0;
    tline_error_ = 0;
    pipeline_latency_rows_ = 0;
    previous_row_.clear();
    current_row_.clear();
    cycle_count_ = 0;
}

void AfeAfe2256Model::step() {
    config_done_ = 0;
    tline_error_ = (cfg_tline_ < kAfe2256MinTLine) ? 1U : 0U;
    if (config_req_ != 0U && afe_ready_ == 0U) {
        afe_ready_ = 1U;
        config_done_ = 1U;
    }

    if (afe_start_ != 0U && afe_ready_ != 0U && tline_error_ == 0U) {
        dout_window_valid_ = 1U;
        fclk_expected_ = 1U;
        ++line_count_;
        current_row_ = MakeAfe2256Row(cfg_cic_profile_, cfg_tp_sel_ + (cfg_nchip_ << 4U));
        if (cfg_pipeline_en_ != 0U) {
            ++pipeline_latency_rows_;
            if (!previous_row_.empty()) {
                current_row_ = previous_row_;
            }
            previous_row_ = MakeAfe2256Row(cfg_cic_profile_, cfg_tp_sel_ + (cfg_nchip_ << 4U) + 1U);
        }
        if (cfg_cic_en_ != 0U) {
            for (auto& sample : current_row_) {
                sample = static_cast<uint16_t>((sample * (cfg_cic_profile_ + 1U)) & 0xFFFFU);
            }
        }
        if (line_count_ >= cfg_tline_) {
            line_count_ = 0U;
            dout_window_valid_ = 0U;
            fclk_expected_ = 0U;
        }
    } else {
        dout_window_valid_ = 0U;
        fclk_expected_ = 0U;
        line_count_ = 0U;
    }
    ++cycle_count_;
}

void AfeAfe2256Model::set_inputs(const SignalMap& inputs) {
    afe_start_ = GetScalar(inputs, "afe_start", afe_start_);
    config_req_ = GetScalar(inputs, "config_req", config_req_);
    cfg_tline_ = GetScalar(inputs, "cfg_tline", cfg_tline_);
    cfg_cic_en_ = GetScalar(inputs, "cfg_cic_en", cfg_cic_en_);
    cfg_cic_profile_ = GetScalar(inputs, "cfg_cic_profile", cfg_cic_profile_);
    cfg_pipeline_en_ = GetScalar(inputs, "cfg_pipeline_en", cfg_pipeline_en_);
    cfg_tp_sel_ = GetScalar(inputs, "cfg_tp_sel", cfg_tp_sel_);
    cfg_nchip_ = GetScalar(inputs, "cfg_nchip", cfg_nchip_);
}

SignalMap AfeAfe2256Model::get_outputs() const {
    return {
        {"config_done", config_done_},
        {"afe_ready", afe_ready_},
        {"dout_window_valid", dout_window_valid_},
        {"fclk_expected", fclk_expected_},
        {"tline_error", tline_error_},
        {"pipeline_latency_rows", pipeline_latency_rows_},
        {"line_pixels", current_row_},
    };
}

std::vector<Mismatch> AfeAfe2256Model::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void AfeAfe2256Model::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "afe_afe2256";
    vectors.spec_name = "SPEC-FPD-006";
    vectors.clock_name = "mclk";
    vectors.signal_inputs = {
        "afe_start", "config_req", "cfg_tline", "cfg_cic_en",
        "cfg_cic_profile", "cfg_pipeline_en", "cfg_tp_sel"
    };
    vectors.signal_outputs = {
        "config_done", "afe_ready", "dout_window_valid", "fclk_expected",
        "tline_error", "pipeline_latency_rows"
    };

    reset();
    set_inputs({{"config_req", 1U}, {"cfg_tline", 5120U}});
    step();
    vectors.vectors.push_back({cycle(), {{"config_req", 1U}, {"cfg_tline", 5120U}}, get_outputs()});

    set_inputs({
        {"afe_start", 1U},
        {"cfg_tline", 5120U},
        {"cfg_cic_en", 1U},
        {"cfg_cic_profile", 2U},
        {"cfg_pipeline_en", 1U},
        {"cfg_tp_sel", 1U},
    });
    step();
    vectors.vectors.push_back({cycle(), {
        {"afe_start", 1U}, {"cfg_tline", 5120U}, {"cfg_cic_en", 1U},
        {"cfg_cic_profile", 2U}, {"cfg_pipeline_en", 1U}, {"cfg_tp_sel", 1U}
    }, get_outputs()});

    WriteHexVectors(vectors, output_dir + "/afe_afe2256_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/afe_afe2256_vectors.bin");
}

}  // namespace fpd::sim
