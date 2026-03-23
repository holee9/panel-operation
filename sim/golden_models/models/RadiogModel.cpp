#include "golden_models/models/RadiogModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

namespace {

std::vector<uint16_t> BuildSyntheticDarkFrame(uint32_t frame_index, std::size_t width = 8U) {
    std::vector<uint16_t> frame(width);
    for (std::size_t i = 0; i < width; ++i) {
        frame[i] = static_cast<uint16_t>((frame_index * 11U + static_cast<uint32_t>(i) * 3U) & 0x0FFFU);
    }
    return frame;
}

}  // namespace

void RadiogModel::reset() {
    start_ = 0;
    xray_ready_ = 0;
    xray_on_ = 0;
    xray_off_ = 0;
    dark_frame_mode_ = 0;
    frame_valid_ = 0;
    cfg_dark_cnt_ = 64;
    cfg_tsettle_ = 100;
    cfg_prep_timeout_ = 30;
    state_ = 0;
    xray_enable_ = 0;
    error_ = 0;
    done_ = 0;
    dark_avg_ready_ = 0;
    dark_frames_captured_ = 0;
    timer_ = 0;
    xray_seen_on_ = 0;
    prev_frame_valid_ = 0;
    frame_pixels_.clear();
    dark_accum_.clear();
    avg_dark_frame_.clear();
    cycle_count_ = 0;
}

void RadiogModel::step() {
    done_ = 0;
    dark_avg_ready_ = 0;
    const auto capture_dark_frame = [&]() {
        const std::vector<uint16_t> frame = frame_pixels_.empty()
            ? BuildSyntheticDarkFrame(dark_frames_captured_)
            : frame_pixels_;
        if (dark_accum_.size() != frame.size()) {
            dark_accum_.assign(frame.size(), 0U);
        }
        avg_dark_frame_.resize(frame.size(), 0U);
        for (std::size_t i = 0; i < frame.size(); ++i) {
            dark_accum_[i] += frame[i];
            const uint32_t divisor = dark_frames_captured_ + 1U;
            avg_dark_frame_[i] = static_cast<uint16_t>(dark_accum_[i] / divisor);
        }
        ++dark_frames_captured_;
    };
    switch (state_) {
      case 0:
        if (start_ != 0U) {
            error_ = 0U;
            dark_frames_captured_ = 0U;
            timer_ = 0U;
            xray_seen_on_ = 0U;
            dark_accum_.clear();
            avg_dark_frame_.clear();
            state_ = dark_frame_mode_ != 0U ? 4U : 1U;
            if (dark_frame_mode_ != 0U &&
                (((frame_valid_ != 0U) && (prev_frame_valid_ == 0U)) || frame_pixels_.empty())) {
                capture_dark_frame();
                if (dark_frames_captured_ >= cfg_dark_cnt_) {
                    dark_avg_ready_ = 1U;
                    done_ = 1U;
                    state_ = 0U;
                }
            }
        }
        break;
      case 1:
        if (xray_ready_ != 0U) {
            state_ = 2U;
            xray_enable_ = 1U;
            timer_ = 0U;
        } else if (++timer_ >= cfg_prep_timeout_) {
            error_ = 1U;
            state_ = 7U;
        }
        break;
      case 2:
        xray_enable_ = 1U;
        if (xray_on_ != 0U) {
            xray_seen_on_ = 1U;
        }
        if (xray_seen_on_ != 0U && xray_off_ != 0U) {
            state_ = 3U;
            timer_ = 0U;
        }
        break;
      case 3:
        if (++timer_ >= cfg_tsettle_) {
            done_ = 1U;
            xray_enable_ = 0U;
            state_ = 0U;
        }
        break;
      case 4:
        if (((frame_valid_ != 0U) && (prev_frame_valid_ == 0U)) || frame_pixels_.empty()) {
            capture_dark_frame();
        }
        if (dark_frames_captured_ >= cfg_dark_cnt_) {
            dark_avg_ready_ = 1U;
            done_ = 1U;
            state_ = 0U;
        }
        break;
      default:
        break;
    }
    prev_frame_valid_ = frame_valid_;
    ++cycle_count_;
}

void RadiogModel::set_inputs(const SignalMap& inputs) {
    start_ = GetScalar(inputs, "start", start_);
    xray_ready_ = GetScalar(inputs, "xray_ready", xray_ready_);
    xray_on_ = GetScalar(inputs, "xray_on", xray_on_);
    xray_off_ = GetScalar(inputs, "xray_off", xray_off_);
    dark_frame_mode_ = GetScalar(inputs, "dark_frame_mode", dark_frame_mode_);
    frame_valid_ = GetScalar(inputs, "frame_valid", frame_valid_);
    cfg_dark_cnt_ = GetScalar(inputs, "cfg_dark_cnt", cfg_dark_cnt_);
    cfg_tsettle_ = GetScalar(inputs, "cfg_tsettle", cfg_tsettle_);
    cfg_prep_timeout_ = GetScalar(inputs, "cfg_prep_timeout", cfg_prep_timeout_);
    frame_pixels_ = GetVector(inputs, "frame_pixels");
}

SignalMap RadiogModel::get_outputs() const {
    return {
        {"state", state_},
        {"xray_enable", xray_enable_},
        {"error", error_},
        {"done", done_},
        {"dark_avg_ready", dark_avg_ready_},
        {"dark_frames_captured", dark_frames_captured_},
        {"dark_avg_frame", avg_dark_frame_},
    };
}

std::vector<Mismatch> RadiogModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void RadiogModel::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "radiography_subfsm";
    vectors.spec_name = "SPEC-FPD-010";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {
        "start", "xray_ready", "xray_on", "xray_off", "dark_frame_mode",
        "frame_valid", "frame_pixels", "cfg_dark_cnt", "cfg_tsettle", "cfg_prep_timeout"
    };
    vectors.signal_outputs = {
        "state", "xray_enable", "error", "done", "dark_avg_ready",
        "dark_frames_captured", "dark_avg_frame"
    };
    reset();
    set_inputs({
        {"start", 1U}, {"xray_ready", 1U}, {"xray_on", 1U},
        {"xray_off", 1U}, {"cfg_tsettle", 2U}
    });
    for (int i = 0; i < 4; ++i) {
        step();
        vectors.vectors.push_back({cycle(), {
            {"start", 1U}, {"xray_ready", 1U}, {"xray_on", 1U}, {"xray_off", 1U}, {"cfg_tsettle", 2U}
        }, get_outputs()});
    }

    reset();
    set_inputs({
        {"start", 1U}, {"dark_frame_mode", 1U}, {"frame_valid", 1U}, {"cfg_dark_cnt", 2U},
        {"frame_pixels", std::vector<uint16_t>{10U, 20U, 30U, 40U}}
    });
    step();
    vectors.vectors.push_back({cycle(), {
        {"start", 1U}, {"dark_frame_mode", 1U}, {"frame_valid", 1U}, {"cfg_dark_cnt", 2U},
        {"frame_pixels", std::vector<uint16_t>{10U, 20U, 30U, 40U}}
    }, get_outputs()});

    set_inputs({
        {"dark_frame_mode", 1U}, {"frame_valid", 1U}, {"cfg_dark_cnt", 2U},
        {"frame_pixels", std::vector<uint16_t>{14U, 24U, 34U, 44U}}
    });
    step();
    vectors.vectors.push_back({cycle(), {
        {"dark_frame_mode", 1U}, {"frame_valid", 1U}, {"cfg_dark_cnt", 2U},
        {"frame_pixels", std::vector<uint16_t>{14U, 24U, 34U, 44U}}
    }, get_outputs()});

    if (GetScalar(get_outputs(), "done") == 0U) {
        step();
        vectors.vectors.push_back({cycle(), {}, get_outputs()});
    }
    WriteHexVectors(vectors, output_dir + "/radiog_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/radiog_vectors.bin");
}

}  // namespace fpd::sim
