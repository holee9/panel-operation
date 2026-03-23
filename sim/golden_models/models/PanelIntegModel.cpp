#include "golden_models/models/PanelIntegModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

void PanelIntegModel::reset() {
    start_ = 0;
    triggered_mode_ = 0;
    radiography_mode_ = 0;
    xray_prep_req_ = 0;
    xray_on_ = 0;
    xray_off_ = 0;
    cfg_tinteg_ = 1000;
    prep_timeout_ = 0;
    xray_enable_ = 0;
    integrate_active_ = 0;
    exposure_done_ = 0;
    timeout_error_ = 0;
    timer_ = 0;
    cycle_count_ = 0;
}

void PanelIntegModel::step() {
    exposure_done_ = 0;
    if (start_ == 0U) {
        xray_enable_ = 0U;
        integrate_active_ = 0U;
        timer_ = 0U;
        ++cycle_count_;
        return;
    }

    xray_enable_ = 1U;
    if (triggered_mode_ != 0U) {
        if (xray_prep_req_ == 0U && xray_on_ == 0U) {
            ++prep_timeout_;
            if (prep_timeout_ >= (radiography_mode_ != 0U ? 30U : 5U)) {
                timeout_error_ = 1U;
            }
        } else {
            prep_timeout_ = 0U;
            integrate_active_ = 1U;
            ++timer_;
            if (xray_off_ != 0U || timer_ >= cfg_tinteg_) {
                exposure_done_ = 1U;
                integrate_active_ = 0U;
                xray_enable_ = 0U;
                timer_ = 0U;
            }
        }
    } else {
        integrate_active_ = 1U;
        ++timer_;
        if (timer_ >= cfg_tinteg_) {
            exposure_done_ = 1U;
            integrate_active_ = 0U;
            xray_enable_ = 0U;
            timer_ = 0U;
        }
    }
    ++cycle_count_;
}

void PanelIntegModel::set_inputs(const SignalMap& inputs) {
    start_ = GetScalar(inputs, "start", start_);
    triggered_mode_ = GetScalar(inputs, "triggered_mode", triggered_mode_);
    radiography_mode_ = GetScalar(inputs, "radiography_mode", radiography_mode_);
    xray_prep_req_ = GetScalar(inputs, "xray_prep_req", xray_prep_req_);
    xray_on_ = GetScalar(inputs, "xray_on", xray_on_);
    xray_off_ = GetScalar(inputs, "xray_off", xray_off_);
    cfg_tinteg_ = GetScalar(inputs, "cfg_tinteg", cfg_tinteg_);
}

SignalMap PanelIntegModel::get_outputs() const {
    return {
        {"xray_enable", xray_enable_},
        {"integrate_active", integrate_active_},
        {"exposure_done", exposure_done_},
        {"timeout_error", timeout_error_},
    };
}

std::vector<Mismatch> PanelIntegModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void PanelIntegModel::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "panel_integ_ctrl";
    vectors.spec_name = "SPEC-FPD-002";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {"start", "triggered_mode", "radiography_mode", "xray_prep_req", "xray_on", "xray_off", "cfg_tinteg"};
    vectors.signal_outputs = {"xray_enable", "integrate_active", "exposure_done", "timeout_error"};
    reset();
    set_inputs({{"start", 1U}, {"triggered_mode", 1U}, {"xray_prep_req", 1U}, {"xray_on", 1U}, {"cfg_tinteg", 3U}});
    for (int i = 0; i < 4; ++i) {
        step();
        vectors.vectors.push_back({cycle(), {{"start", 1U}, {"triggered_mode", 1U}, {"xray_prep_req", 1U}, {"xray_on", 1U}, {"cfg_tinteg", 3U}}, get_outputs()});
    }
    WriteHexVectors(vectors, output_dir + "/panel_integ_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/panel_integ_vectors.bin");
}

}  // namespace fpd::sim
