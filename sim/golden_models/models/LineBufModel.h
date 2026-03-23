#pragma once

#include <array>
#include <queue>
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
    static constexpr std::size_t kMaxCols = 3072;
    static constexpr std::size_t kFifoDepth = 16;
    std::array<std::vector<uint16_t>, 2> banks_{{std::vector<uint16_t>(kMaxCols), std::vector<uint16_t>(kMaxCols)}};
    std::queue<uint16_t> cdc_fifo_;
    uint32_t cfg_ncols_ = 2048;
    uint32_t cfg_afe_count_ = 1;
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
    uint32_t fifo_level_ = 0;
    uint32_t fifo_overflow_ = 0;
    uint32_t line_fill_level_ = 0;
    uint32_t active_streams_ = 1;
    std::vector<uint16_t> wr_samples_;
};

}  // namespace fpd::sim
