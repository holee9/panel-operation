#include "golden_models/models/PanelFsmModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

namespace {

uint32_t ComboDefaultRows(uint32_t combo) {
    switch (combo) {
        case 6U:
        case 7U:
            return 3072U;
        default:
            return 2048U;
    }
}

uint32_t ComboDefaultReset(uint32_t combo) {
    return (combo >= 6U) ? 4U : 2U;
}

uint32_t ComboDefaultIntegrate(uint32_t combo) {
    switch (combo) {
        case 2U:
            return 6000U;
        case 3U:
            return 5120U;
        default:
            return 2200U;
    }
}

uint32_t EffectiveOrDefault(uint32_t value, uint32_t fallback) {
    return value == 0U ? fallback : value;
}

}  // namespace

void PanelFsmModel::reset() {
    state_ = 0;
    mode_ = 0;
    combo_ = 1;
    nrows_ = 0;
    treset_ = 0;
    tinteg_ = 0;
    nreset_ = 0;
    sync_dly_ = 0;
    tgate_settle_ = 0;
    line_idx_ = 0;
    timer_ = 0;
    wait_timer_ = 0;
    ctrl_start_ = 0;
    ctrl_abort_ = 0;
    gate_row_done_ = 0;
    afe_config_done_ = 0;
    afe_line_valid_ = 0;
    xray_prep_req_ = 0;
    xray_on_ = 0;
    xray_off_ = 0;
    prot_error_ = 0;
    prot_force_stop_ = 0;
    radiography_mode_ = 0;
    busy_ = 0;
    done_ = 0;
    error_ = 0;
    err_code_ = 0;
    cycle_count_ = 0;
}

void PanelFsmModel::step() {
    const uint32_t eff_nrows = EffectiveOrDefault(nrows_, ComboDefaultRows(combo_));
    const uint32_t eff_treset = EffectiveOrDefault(treset_, ComboDefaultReset(combo_));
    const uint32_t eff_tinteg = EffectiveOrDefault(tinteg_, ComboDefaultIntegrate(combo_));
    const uint32_t eff_nreset = EffectiveOrDefault(nreset_, 1U);
    const uint32_t eff_sync_dly = EffectiveOrDefault(sync_dly_, 1U);
    const uint32_t eff_tgate_settle = EffectiveOrDefault(tgate_settle_, 1U);
    const uint32_t eff_ready_timeout = radiography_mode_ != 0U ? 30U : 5U;
    done_ = 0;
    if (prot_error_ || prot_force_stop_) {
      state_ = 15;
      busy_ = 0;
      error_ = 1;
      err_code_ = 3;
    } else if (ctrl_abort_) {
      state_ = 0;
      busy_ = 0;
      error_ = 1;
      err_code_ = 1;
    } else {
      switch (state_) {
        case 0:
          busy_ = 0;
          error_ = 0;
          err_code_ = 0;
          line_idx_ = 0;
          timer_ = 0;
          wait_timer_ = 0;
          if (ctrl_start_ != 0U) {
            state_ = 1;
            busy_ = 1;
          }
          break;
        case 1:
          timer_ = 0;
          wait_timer_ = 0;
          state_ = 2;
          break;
        case 2:
          if (++timer_ >= (eff_treset + eff_nreset)) {
            timer_ = 0;
            state_ = (mode_ == 4U) ? 10U : ((mode_ == 2U || radiography_mode_ != 0U) ? 3U : 4U);
          }
          break;
        case 3:
          if (++wait_timer_ >= eff_ready_timeout) {
            state_ = 15;
            error_ = 1;
            err_code_ = 2;
          } else if (xray_prep_req_ != 0U) {
            wait_timer_ = 0;
            timer_ = 0;
            state_ = 5;
          }
          break;
        case 4:
          if (++timer_ >= eff_tinteg) {
            timer_ = 0;
            state_ = 6;
          }
          break;
        case 5:
          if (++wait_timer_ >= eff_ready_timeout) {
            state_ = 15;
            error_ = 1;
            err_code_ = 2;
          } else if (xray_on_ != 0U || xray_prep_req_ != 0U) {
            if (++timer_ >= eff_tinteg || xray_off_ != 0U) {
              timer_ = 0;
              state_ = 6;
            }
          }
          break;
        case 6:
          if (afe_config_done_ != 0U || ++timer_ >= eff_sync_dly) {
            timer_ = 0;
            line_idx_ = 0;
            state_ = 7;
          }
          break;
        case 7:
          if (((mode_ != 3U) && gate_row_done_ != 0U && afe_line_valid_ != 0U) ||
              ((mode_ == 3U) && afe_line_valid_ != 0U)) {
            if (line_idx_ + 1U >= eff_nrows) {
              timer_ = 0;
              state_ = 8;
            } else {
              ++line_idx_;
            }
          }
          break;
        case 8:
          if (++timer_ >= eff_tgate_settle) {
            timer_ = 0;
            state_ = 9;
          }
          break;
        case 9:
          state_ = 10;
          break;
        case 10:
          busy_ = 0;
          done_ = 1;
          state_ = (mode_ == 1U) ? 1U : 0U;
          break;
        default:
          if (ctrl_start_ == 0U) {
            state_ = 0;
          }
          break;
      }
    }
    ++cycle_count_;
}

void PanelFsmModel::set_inputs(const SignalMap& inputs) {
    ctrl_start_ = GetScalar(inputs, "ctrl_start", ctrl_start_);
    ctrl_abort_ = GetScalar(inputs, "ctrl_abort", ctrl_abort_);
    mode_ = GetScalar(inputs, "cfg_mode", mode_);
    combo_ = GetScalar(inputs, "cfg_combo", combo_);
    nrows_ = GetScalar(inputs, "cfg_nrows", nrows_);
    treset_ = GetScalar(inputs, "cfg_treset", treset_);
    tinteg_ = GetScalar(inputs, "cfg_tinteg", tinteg_);
    nreset_ = GetScalar(inputs, "cfg_nreset", nreset_);
    sync_dly_ = GetScalar(inputs, "cfg_sync_dly", sync_dly_);
    tgate_settle_ = GetScalar(inputs, "cfg_tgate_settle", tgate_settle_);
    gate_row_done_ = GetScalar(inputs, "gate_row_done", gate_row_done_);
    afe_config_done_ = GetScalar(inputs, "afe_config_done", afe_config_done_);
    afe_line_valid_ = GetScalar(inputs, "afe_line_valid", afe_line_valid_);
    xray_prep_req_ = GetScalar(inputs, "xray_prep_req", xray_prep_req_);
    xray_on_ = GetScalar(inputs, "xray_on", xray_on_);
    xray_off_ = GetScalar(inputs, "xray_off", xray_off_);
    prot_error_ = GetScalar(inputs, "prot_error", prot_error_);
    prot_force_stop_ = GetScalar(inputs, "prot_force_stop", prot_force_stop_);
    radiography_mode_ = GetScalar(inputs, "radiography_mode", radiography_mode_);
}

SignalMap PanelFsmModel::get_outputs() const {
    return {
        {"fsm_state", state_},
        {"sts_busy", busy_},
        {"sts_done", done_},
        {"sts_error", error_},
        {"sts_line_idx", line_idx_},
        {"sts_err_code", err_code_},
    };
}

std::vector<Mismatch> PanelFsmModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void PanelFsmModel::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "panel_ctrl_fsm";
    vectors.spec_name = "SPEC-FPD-002";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {
        "ctrl_start", "cfg_mode", "cfg_combo", "cfg_nrows", "cfg_treset", "cfg_tinteg",
        "cfg_nreset", "cfg_sync_dly", "cfg_tgate_settle", "xray_prep_req",
        "xray_on", "xray_off", "gate_row_done", "afe_config_done", "afe_line_valid"
    };
    vectors.signal_outputs = {"fsm_state", "sts_busy", "sts_done", "sts_error", "sts_line_idx", "sts_err_code"};

    reset();
    set_inputs({
        {"ctrl_start", 1U}, {"cfg_mode", 0U}, {"cfg_combo", 1U}, {"cfg_nrows", 2U},
        {"cfg_treset", 1U}, {"cfg_tinteg", 1U}
    });
    for (int i = 0; i < 6; ++i) {
        if (i == 3) {
            set_inputs({
                {"cfg_combo", 1U}, {"afe_config_done", 1U}, {"afe_line_valid", 1U},
                {"gate_row_done", 1U}, {"cfg_nrows", 2U}
            });
        }
        step();
        vectors.vectors.push_back({cycle(), {}, get_outputs()});
    }
    WriteHexVectors(vectors, output_dir + "/panel_fsm_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/panel_fsm_vectors.bin");
}

}  // namespace fpd::sim
