#include "golden_models/models/PanelFsmModel.h"

namespace fpd::sim {

void PanelFsmModel::reset() {
    state_ = 0;
    mode_ = 0;
    nrows_ = 2048;
    line_idx_ = 0;
    timer_ = 0;
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
      state_ = 7;
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
          if (ctrl_start_ != 0U) {
            state_ = 1;
            busy_ = 1;
          }
          break;
        case 1:
          if (++timer_ >= 4U) {
            timer_ = 0;
            state_ = (mode_ == 4U) ? 6U : 2U;
          }
          break;
        case 2:
          if (mode_ == 2U) {
            if (xray_on_ || xray_prep_req_) {
              state_ = 3;
            } else if (++timer_ >= (radiography_mode_ != 0U ? 30U : 5U)) {
              state_ = 7;
              error_ = 1;
              err_code_ = 2;
            }
          } else if (++timer_ >= 2U) {
            timer_ = 0;
            state_ = 3;
          }
          break;
        case 3:
          if (afe_config_done_ != 0U) {
            state_ = 4;
          }
          break;
        case 4:
          if (gate_row_done_ != 0U || afe_line_valid_ != 0U || mode_ == 3U) {
            if (line_idx_ + 1U >= nrows_) {
              state_ = 5;
            } else {
              ++line_idx_;
            }
          }
          break;
        case 5:
          state_ = 6;
          break;
        case 6:
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
