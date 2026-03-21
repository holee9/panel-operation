#pragma once

#include <array>
#include <vector>

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class LineBufModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    static constexpr std::size_t kCols = 2048;
    std::array<std::vector<uint16_t>, 2> banks_{{std::vector<uint16_t>(kCols), std::vector<uint16_t>(kCols)}};
    uint32_t wr_addr_ = 0;
    uint32_t wr_data_ = 0;
    uint32_t wr_en_ = 0;
    uint32_t wr_bank_sel_ = 0;
    uint32_t rd_addr_ = 0;
    uint32_t rd_en_ = 0;
    uint32_t rd_bank_sel_ = 0;
    uint32_t rd_data_ = 0;
    uint32_t wr_line_done_ = 0;
    uint32_t bank_swap_ = 0;
};

}  // namespace fpd::sim
