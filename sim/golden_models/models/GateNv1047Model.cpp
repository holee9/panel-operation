#include "golden_models/models/GateNv1047Model.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

void GateNv1047Model::reset() {
    row_index_ = 0;
    gate_on_pulse_ = 0;
    scan_dir_ = 0;
    reset_all_ = 0;
    cfg_clk_period_ = 2200;
    cfg_gate_on_ = 2200;
    cfg_gate_settle_ = 100;
    nv_sd1_ = 0;
    nv_sd2_ = 0;
    nv_clk_ = 0;
    nv_oe_ = 1;
    nv_ona_ = 1;
    nv_lr_ = 0;
    nv_rst_ = 1;
    row_done_ = 0;
    gate_on_prev_ = 0;
    bbm_count_ = 0;
    bbm_pending_ = 0;
    clk_div_ = 0;
    cycle_count_ = 0;
}

void GateNv1047Model::step() {
    row_done_ = 0;
    nv_lr_ = scan_dir_;
    if (gate_on_prev_ != 0U && gate_on_pulse_ == 0U) {
        bbm_count_ = (cfg_gate_settle_ == 0U) ? 1U : cfg_gate_settle_;
        bbm_pending_ = 1U;
    } else if (bbm_count_ != 0U) {
        if (bbm_pending_ != 0U && bbm_count_ == 1U) {
            row_done_ = 1U;
            bbm_pending_ = 0U;
        }
        --bbm_count_;
    }
    if (reset_all_ != 0U) {
        nv_ona_ = 0U;
        nv_rst_ = 0U;
        nv_oe_ = 1U;
        bbm_pending_ = 0U;
    } else {
        nv_rst_ = 1U;
        nv_ona_ = 1U;
        if (gate_on_pulse_ != 0U) {
            ++clk_div_;
            if (clk_div_ >= (cfg_clk_period_ / 2U)) {
                clk_div_ = 0U;
                nv_clk_ ^= 1U;
            }
            nv_sd1_ = (row_index_ >> 0U) & 0x1U;
            nv_sd2_ = (row_index_ >> 1U) & 0x1U;
            nv_oe_ = (bbm_count_ == 0U) ? 0U : 1U;
        } else {
            nv_clk_ = 0U;
            nv_oe_ = 1U;
            clk_div_ = 0U;
        }
    }
    gate_on_prev_ = gate_on_pulse_;
    ++cycle_count_;
}

void GateNv1047Model::set_inputs(const SignalMap& inputs) {
    row_index_ = GetScalar(inputs, "row_index", row_index_);
    gate_on_pulse_ = GetScalar(inputs, "gate_on_pulse", gate_on_pulse_);
    scan_dir_ = GetScalar(inputs, "scan_dir", scan_dir_);
    reset_all_ = GetScalar(inputs, "reset_all", reset_all_);
    cfg_clk_period_ = GetScalar(inputs, "cfg_clk_period", cfg_clk_period_);
    cfg_gate_on_ = GetScalar(inputs, "cfg_gate_on", cfg_gate_on_);
    cfg_gate_settle_ = GetScalar(inputs, "cfg_gate_settle", cfg_gate_settle_);
}

SignalMap GateNv1047Model::get_outputs() const {
    return {
        {"nv_sd1", nv_sd1_},
        {"nv_sd2", nv_sd2_},
        {"nv_clk", nv_clk_},
        {"nv_oe", nv_oe_},
        {"nv_ona", nv_ona_},
        {"nv_lr", nv_lr_},
        {"nv_rst", nv_rst_},
        {"row_done", row_done_},
    };
}

std::vector<Mismatch> GateNv1047Model::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void GateNv1047Model::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "gate_nv1047";
    vectors.spec_name = "SPEC-FPD-003";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {"row_index", "gate_on_pulse", "scan_dir", "reset_all", "cfg_clk_period"};
    vectors.signal_outputs = {"nv_sd1", "nv_sd2", "nv_clk", "nv_oe", "nv_ona", "nv_lr", "nv_rst", "row_done"};
    reset();
    set_inputs({{"row_index", 5U}, {"gate_on_pulse", 1U}, {"scan_dir", 0U}, {"cfg_clk_period", 2200U}});
    step();
    vectors.vectors.push_back({cycle(), {{"row_index", 5U}, {"gate_on_pulse", 1U}, {"scan_dir", 0U}, {"cfg_clk_period", 2200U}}, get_outputs()});
    WriteHexVectors(vectors, output_dir + "/gate_nv1047_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/gate_nv1047_vectors.bin");
}

}  // namespace fpd::sim
