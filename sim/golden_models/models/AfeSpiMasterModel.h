#pragma once

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class AfeSpiMasterModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    uint32_t start_ = 0;
    uint32_t cfg_word_ = 0xA55A;
    uint32_t cfg_nchip_ = 1;
    uint32_t spi_sck_ = 0;
    uint32_t spi_sdi_ = 0;
    uint32_t spi_cs_n_ = 1;
    uint32_t done_ = 0;
    uint32_t bit_index_ = 0;
};

}  // namespace fpd::sim
