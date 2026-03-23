#include "golden_models/models/GateNt39565dModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

void GateNt39565dModel::reset() {
    row_index_ = 0;
    gate_on_pulse_ = 0;
    scan_dir_ = 0;
    chip_sel_ = 0;
    mode_sel_ = 0;
    cascade_stv_return_ = 0;
    stv1l_ = 0;
    stv2l_ = 0;
    stv1r_ = 0;
    stv2r_ = 0;
    oe1l_ = 0;
    oe1r_ = 0;
    oe2l_ = 0;
    oe2r_ = 0;
    cascade_complete_ = 0;
    cycle_count_ = 0;
}

void GateNt39565dModel::step() {
    const uint32_t phase = scan_dir_ != 0U ? ((row_index_ + 1U) & 0x1U) : (row_index_ & 0x1U);
    const uint32_t chip_phase = row_index_ / 541U;
    const bool left_active = chip_sel_ != 1U;
    const bool right_active = chip_sel_ != 0U;
    stv1l_ = gate_on_pulse_ != 0U && left_active && phase == 0U;
    stv2l_ = gate_on_pulse_ != 0U && left_active && phase == 1U;
    stv1r_ = gate_on_pulse_ != 0U && right_active && phase == 0U;
    stv2r_ = gate_on_pulse_ != 0U && right_active && phase == 1U;
    oe1l_ = gate_on_pulse_ != 0U && left_active && (row_index_ & 0x1U) == 0U;
    oe1r_ = gate_on_pulse_ != 0U && right_active && (row_index_ & 0x1U) == 0U;
    oe2l_ = gate_on_pulse_ != 0U && left_active && (row_index_ & 0x1U) != 0U;
    oe2r_ = gate_on_pulse_ != 0U && right_active && (row_index_ & 0x1U) != 0U;
    cascade_complete_ = gate_on_pulse_ != 0U && cascade_stv_return_ != 0U &&
                        (mode_sel_ != 0U || chip_sel_ == 2U || chip_phase >= 5U);
    ++cycle_count_;
}

void GateNt39565dModel::set_inputs(const SignalMap& inputs) {
    row_index_ = GetScalar(inputs, "row_index", row_index_);
    gate_on_pulse_ = GetScalar(inputs, "gate_on_pulse", gate_on_pulse_);
    scan_dir_ = GetScalar(inputs, "scan_dir", scan_dir_);
    chip_sel_ = GetScalar(inputs, "chip_sel", chip_sel_);
    mode_sel_ = GetScalar(inputs, "mode_sel", mode_sel_);
    cascade_stv_return_ = GetScalar(inputs, "cascade_stv_return", cascade_stv_return_);
}

SignalMap GateNt39565dModel::get_outputs() const {
    return {
        {"stv1l", stv1l_}, {"stv2l", stv2l_}, {"stv1r", stv1r_}, {"stv2r", stv2r_},
        {"oe1l", oe1l_}, {"oe1r", oe1r_}, {"oe2l", oe2l_}, {"oe2r", oe2r_},
        {"cascade_complete", cascade_complete_},
    };
}

std::vector<Mismatch> GateNt39565dModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void GateNt39565dModel::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "gate_nt39565d";
    vectors.spec_name = "SPEC-FPD-004";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {"row_index", "gate_on_pulse", "scan_dir", "chip_sel", "mode_sel", "cascade_stv_return"};
    vectors.signal_outputs = {"stv1l", "stv2l", "stv1r", "stv2r", "oe1l", "oe1r", "oe2l", "oe2r", "cascade_complete"};
    reset();
    set_inputs({{"row_index", 0U}, {"gate_on_pulse", 1U}, {"chip_sel", 2U}, {"mode_sel", 1U}, {"cascade_stv_return", 1U}});
    step();
    vectors.vectors.push_back({cycle(), {{"row_index", 0U}, {"gate_on_pulse", 1U}, {"chip_sel", 2U}, {"mode_sel", 1U}, {"cascade_stv_return", 1U}}, get_outputs()});
    WriteHexVectors(vectors, output_dir + "/gate_nt39565d_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/gate_nt39565d_vectors.bin");
}

}  // namespace fpd::sim
