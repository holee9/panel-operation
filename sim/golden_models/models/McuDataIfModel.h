#pragma once

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class McuDataIfModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    uint32_t pixel_data_ = 0;
    uint32_t data_valid_ = 0;
    uint32_t line_start_ = 0;
    uint32_t line_end_ = 0;
    uint32_t frame_done_ = 0;
    uint32_t mcu_data_ack_ = 0;
    uint32_t mcu_data_ = 0;
    uint32_t mcu_data_rdy_ = 0;
    uint32_t irq_line_ready_ = 0;
    uint32_t irq_frame_done_ = 0;
};

}  // namespace fpd::sim
