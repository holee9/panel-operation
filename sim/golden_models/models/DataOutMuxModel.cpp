#include "golden_models/models/DataOutMuxModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

void DataOutMuxModel::reset() {
    cfg_ncols_ = 2048;
    line_pixel_data_ = 0;
    line_data_valid_ = 0;
    line_pixel_idx_ = 0;
    mcu_pixel_data_ = 0;
    mcu_data_valid_ = 0;
    mcu_line_start_ = 0;
    mcu_line_end_ = 0;
    cycle_count_ = 0;
}

void DataOutMuxModel::step() {
    mcu_pixel_data_ = line_pixel_data_;
    mcu_data_valid_ = line_data_valid_;
    mcu_line_start_ = (line_data_valid_ != 0U && line_pixel_idx_ == 0U) ? 1U : 0U;
    mcu_line_end_ = (line_data_valid_ != 0U && line_pixel_idx_ + 1U >= cfg_ncols_) ? 1U : 0U;
    ++cycle_count_;
}

void DataOutMuxModel::set_inputs(const SignalMap& inputs) {
    cfg_ncols_ = GetScalar(inputs, "cfg_ncols", cfg_ncols_);
    line_pixel_data_ = GetScalar(inputs, "line_pixel_data", line_pixel_data_);
    line_data_valid_ = GetScalar(inputs, "line_data_valid", line_data_valid_);
    line_pixel_idx_ = GetScalar(inputs, "line_pixel_idx", line_pixel_idx_);
}

SignalMap DataOutMuxModel::get_outputs() const {
    return {
        {"mcu_pixel_data", mcu_pixel_data_},
        {"mcu_data_valid", mcu_data_valid_},
        {"mcu_line_start", mcu_line_start_},
        {"mcu_line_end", mcu_line_end_},
    };
}

std::vector<Mismatch> DataOutMuxModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void DataOutMuxModel::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "data_out_mux";
    vectors.spec_name = "SPEC-FPD-007";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {"cfg_ncols", "line_pixel_data", "line_data_valid", "line_pixel_idx"};
    vectors.signal_outputs = {"mcu_pixel_data", "mcu_data_valid", "mcu_line_start", "mcu_line_end"};
    reset();
    set_inputs({{"cfg_ncols", 4U}, {"line_pixel_data", 0x55AAU}, {"line_data_valid", 1U}, {"line_pixel_idx", 0U}});
    step();
    vectors.vectors.push_back({cycle(), {{"cfg_ncols", 4U}, {"line_pixel_data", 0x55AAU}, {"line_data_valid", 1U}, {"line_pixel_idx", 0U}}, get_outputs()});
    WriteHexVectors(vectors, output_dir + "/data_out_mux_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/data_out_mux_vectors.bin");
}

}  // namespace fpd::sim
