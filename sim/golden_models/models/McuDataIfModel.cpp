#include "golden_models/models/McuDataIfModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

void McuDataIfModel::reset() {
    pixel_data_ = 0;
    data_valid_ = 0;
    line_start_ = 0;
    line_end_ = 0;
    frame_done_ = 0;
    mcu_data_ack_ = 0;
    mcu_data_ = 0;
    mcu_data_rdy_ = 0;
    irq_line_ready_ = 0;
    irq_frame_done_ = 0;
    cycle_count_ = 0;
}

void McuDataIfModel::step() {
    irq_line_ready_ = 0U;
    irq_frame_done_ = 0U;
    if (data_valid_ != 0U) {
        mcu_data_ = pixel_data_;
        mcu_data_rdy_ = 1U;
    }
    if (mcu_data_ack_ != 0U) {
        mcu_data_rdy_ = 0U;
    }
    if (line_end_ != 0U) {
        irq_line_ready_ = 1U;
    }
    if (frame_done_ != 0U) {
        irq_frame_done_ = 1U;
    }
    ++cycle_count_;
}

void McuDataIfModel::set_inputs(const SignalMap& inputs) {
    pixel_data_ = GetScalar(inputs, "pixel_data", pixel_data_);
    data_valid_ = GetScalar(inputs, "data_valid", data_valid_);
    line_start_ = GetScalar(inputs, "line_start", line_start_);
    line_end_ = GetScalar(inputs, "line_end", line_end_);
    frame_done_ = GetScalar(inputs, "frame_done", frame_done_);
    mcu_data_ack_ = GetScalar(inputs, "mcu_data_ack", mcu_data_ack_);
}

SignalMap McuDataIfModel::get_outputs() const {
    return {
        {"mcu_data", mcu_data_},
        {"mcu_data_rdy", mcu_data_rdy_},
        {"irq_line_ready", irq_line_ready_},
        {"irq_frame_done", irq_frame_done_},
    };
}

std::vector<Mismatch> McuDataIfModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void McuDataIfModel::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "mcu_data_if";
    vectors.spec_name = "SPEC-FPD-007";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {"pixel_data", "data_valid", "line_start", "line_end", "frame_done", "mcu_data_ack"};
    vectors.signal_outputs = {"mcu_data", "mcu_data_rdy", "irq_line_ready", "irq_frame_done"};
    reset();
    set_inputs({{"pixel_data", 0xCAFEU}, {"data_valid", 1U}, {"line_end", 1U}});
    step();
    vectors.vectors.push_back({cycle(), {{"pixel_data", 0xCAFEU}, {"data_valid", 1U}, {"line_end", 1U}}, get_outputs()});
    WriteHexVectors(vectors, output_dir + "/mcu_data_if_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/mcu_data_if_vectors.bin");
}

}  // namespace fpd::sim
