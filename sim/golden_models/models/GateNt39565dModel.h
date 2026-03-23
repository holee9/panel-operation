#pragma once

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class GateNt39565dModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    uint32_t row_index_ = 0;
    uint32_t gate_on_pulse_ = 0;
    uint32_t scan_dir_ = 0;
    uint32_t chip_sel_ = 0;
    uint32_t mode_sel_ = 0;
    uint32_t cascade_stv_return_ = 0;
    uint32_t stv1l_ = 0;
    uint32_t stv2l_ = 0;
    uint32_t stv1r_ = 0;
    uint32_t stv2r_ = 0;
    uint32_t oe1l_ = 0;
    uint32_t oe1r_ = 0;
    uint32_t oe2l_ = 0;
    uint32_t oe2r_ = 0;
    uint32_t cascade_complete_ = 0;
};

}  // namespace fpd::sim
