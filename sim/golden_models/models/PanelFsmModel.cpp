#include "golden_models/models/PanelFsmModel.h"

namespace fpd::sim {

void PanelFsmModel::reset() {
    state_ = 0;
    mode_ = 0;
    nrows_ = 2048;
    treset_ = 100;
    tinteg_ = 1000;
    nreset_ = 3;
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
          if (++timer_ >= (treset_ + nreset_)) {
            timer_ = 0;
            state_ = (mode_ == 4U) ? 10U : ((mode_ == 2U || radiography_mode_ != 0U) ? 3U : 4U);
          }
          break;
        case 3:
          if (++wait_timer_ >= (radiography_mode_ != 0U ? 30U : 5U)) {
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
          if (++timer_ >= tinteg_) {
            timer_ = 0;
            state_ = 6;
          }
          break;
        case 5:
          if (++wait_timer_ >= (radiography_mode_ != 0U ? 30U : 5U)) {
            state_ = 15;
            error_ = 1;
            err_code_ = 2;
          } else if (xray_on_ != 0U || xray_prep_req_ != 0U) {
            if (++timer_ >= tinteg_ || xray_off_ != 0U) {
              timer_ = 0;
              state_ = 6;
            }
          }
          break;
        case 6:
          if (afe_config_done_ != 0U || ++timer_ >= sync_dly_) {
            timer_ = 0;
            line_idx_ = 0;
            state_ = 7;
          }
          break;
        case 7:
          if (((mode_ != 3U) && gate_row_done_ != 0U && afe_line_valid_ != 0U) ||
              ((mode_ == 3U) && afe_line_valid_ != 0U)) {
            if (line_idx_ + 1U >= nrows_) {
              timer_ = 0;
              state_ = 8;
            } else {
              ++line_idx_;
            }
          }
          break;
        case 8:
          if (++timer_ >= tgate_settle_) {
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
    (void)output_dir;
}

}  // namespace fpd::sim
