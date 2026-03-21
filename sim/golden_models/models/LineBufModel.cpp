#include "golden_models/models/LineBufModel.h"

namespace fpd::sim {

void LineBufModel::reset() {
    banks_[0].assign(kCols, 0);
    banks_[1].assign(kCols, 0);
    wr_addr_ = 0;
    wr_data_ = 0;
    wr_en_ = 0;
    wr_bank_sel_ = 0;
    rd_addr_ = 0;
    rd_en_ = 0;
    rd_bank_sel_ = 0;
    rd_data_ = 0;
    wr_line_done_ = 0;
    bank_swap_ = 0;
    cycle_count_ = 0;
}

void LineBufModel::step() {
    wr_line_done_ = 0;
    bank_swap_ = 0;
    if (wr_en_ != 0U && wr_addr_ < kCols) {
        banks_[wr_bank_sel_ & 0x1U][wr_addr_] = static_cast<uint16_t>(wr_data_ & 0xFFFFU);
        if (wr_addr_ + 1U >= kCols) {
            wr_line_done_ = 1;
            bank_swap_ = 1;
        }
    }
    if (rd_en_ != 0U && rd_addr_ < kCols) {
        rd_data_ = banks_[rd_bank_sel_ & 0x1U][rd_addr_];
    }
    ++cycle_count_;
}

void LineBufModel::set_inputs(const SignalMap& inputs) {
    wr_addr_ = GetScalar(inputs, "wr_addr", wr_addr_);
    wr_data_ = GetScalar(inputs, "wr_data", wr_data_);
    wr_en_ = GetScalar(inputs, "wr_en", wr_en_);
    wr_bank_sel_ = GetScalar(inputs, "wr_bank_sel", wr_bank_sel_);
    rd_addr_ = GetScalar(inputs, "rd_addr", rd_addr_);
    rd_en_ = GetScalar(inputs, "rd_en", rd_en_);
    rd_bank_sel_ = GetScalar(inputs, "rd_bank_sel", rd_bank_sel_);
}

SignalMap LineBufModel::get_outputs() const {
    return {
        {"rd_data", rd_data_},
        {"wr_line_done", wr_line_done_},
        {"bank_swap", bank_swap_},
    };
}

std::vector<Mismatch> LineBufModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void LineBufModel::generate_vectors(const std::string& output_dir) {
    (void)output_dir;
}

}  // namespace fpd::sim
