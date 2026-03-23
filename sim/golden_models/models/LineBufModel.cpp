#include "golden_models/models/LineBufModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

void LineBufModel::reset() {
    banks_[0].assign(kMaxCols, 0);
    banks_[1].assign(kMaxCols, 0);
    while (!cdc_fifo_.empty()) {
        cdc_fifo_.pop();
    }
    cfg_ncols_ = 2048;
    cfg_afe_count_ = 1;
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
    fifo_level_ = 0;
    fifo_overflow_ = 0;
    line_fill_level_ = 0;
    active_streams_ = 1;
    wr_samples_.clear();
    cycle_count_ = 0;
}

void LineBufModel::step() {
    wr_line_done_ = 0;
    bank_swap_ = 0;
    fifo_overflow_ = 0;

    const uint32_t effective_streams = (cfg_afe_count_ == 0U) ? 1U : cfg_afe_count_;
    active_streams_ = effective_streams;

    if (wr_en_ != 0U) {
        if (!wr_samples_.empty()) {
            for (const auto sample : wr_samples_) {
                if (cdc_fifo_.size() < kFifoDepth) {
                    cdc_fifo_.push(sample);
                } else {
                    fifo_overflow_ = 1U;
                    break;
                }
            }
        } else if (cdc_fifo_.size() < kFifoDepth) {
            cdc_fifo_.push(static_cast<uint16_t>(wr_data_ & 0xFFFFU));
        } else {
            fifo_overflow_ = 1U;
        }
    }

    std::size_t write_addr = static_cast<std::size_t>(wr_addr_);
    const std::size_t max_writes = wr_samples_.empty() ? 1U : wr_samples_.size();
    std::size_t writes = 0U;
    while (!cdc_fifo_.empty() &&
           write_addr < cfg_ncols_ &&
           write_addr < kMaxCols &&
           writes < max_writes) {
        banks_[wr_bank_sel_ & 0x1U][write_addr] = cdc_fifo_.front();
        cdc_fifo_.pop();
        ++write_addr;
        ++writes;
        if (line_fill_level_ < cfg_ncols_) {
            ++line_fill_level_;
        }
        if (write_addr >= cfg_ncols_) {
            wr_line_done_ = 1U;
            bank_swap_ = 1U;
            line_fill_level_ = 0U;
            break;
        }
    }

    if (rd_en_ != 0U && rd_addr_ < cfg_ncols_ && rd_addr_ < kMaxCols) {
        rd_data_ = banks_[rd_bank_sel_ & 0x1U][rd_addr_];
    }
    fifo_level_ = static_cast<uint32_t>(cdc_fifo_.size());
    ++cycle_count_;
}

void LineBufModel::set_inputs(const SignalMap& inputs) {
    cfg_ncols_ = GetScalar(inputs, "cfg_ncols", cfg_ncols_);
    cfg_afe_count_ = GetScalar(inputs, "cfg_afe_count", cfg_afe_count_);
    wr_addr_ = GetScalar(inputs, "wr_addr", wr_addr_);
    wr_data_ = GetScalar(inputs, "wr_data", wr_data_);
    wr_en_ = GetScalar(inputs, "wr_en", wr_en_);
    wr_bank_sel_ = GetScalar(inputs, "wr_bank_sel", wr_bank_sel_);
    rd_addr_ = GetScalar(inputs, "rd_addr", rd_addr_);
    rd_en_ = GetScalar(inputs, "rd_en", rd_en_);
    rd_bank_sel_ = GetScalar(inputs, "rd_bank_sel", rd_bank_sel_);
    wr_samples_ = GetVector(inputs, "wr_samples");
}

SignalMap LineBufModel::get_outputs() const {
    return {
        {"rd_data", rd_data_},
        {"wr_line_done", wr_line_done_},
        {"bank_swap", bank_swap_},
        {"fifo_level", fifo_level_},
        {"fifo_overflow", fifo_overflow_},
        {"line_fill_level", line_fill_level_},
        {"active_streams", active_streams_},
    };
}

std::vector<Mismatch> LineBufModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void LineBufModel::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "line_buf_ram";
    vectors.spec_name = "SPEC-FPD-007";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {
        "cfg_ncols", "cfg_afe_count", "wr_addr", "wr_data", "wr_samples",
        "wr_en", "wr_bank_sel", "rd_addr", "rd_en", "rd_bank_sel"
    };
    vectors.signal_outputs = {
        "rd_data", "wr_line_done", "bank_swap", "fifo_level",
        "fifo_overflow", "line_fill_level", "active_streams"
    };

    reset();
    set_inputs({
        {"cfg_ncols", 2048U}, {"cfg_afe_count", 1U}, {"wr_addr", 0U}, {"wr_data", 0x1234U}, {"wr_en", 1U},
        {"wr_bank_sel", 0U}, {"rd_addr", 0U}, {"rd_en", 0U}, {"rd_bank_sel", 0U}
    });
    step();
    vectors.vectors.push_back({cycle(), {
        {"cfg_ncols", 2048U}, {"cfg_afe_count", 1U}, {"wr_addr", 0U}, {"wr_data", 0x1234U}, {"wr_en", 1U},
        {"wr_bank_sel", 0U}, {"rd_addr", 0U}, {"rd_en", 0U}, {"rd_bank_sel", 0U}
    }, get_outputs()});

    set_inputs({
        {"cfg_ncols", 3072U}, {"cfg_afe_count", 12U}, {"wr_addr", 3071U}, {"wr_data", 0x5678U}, {"wr_en", 1U},
        {"wr_bank_sel", 1U}, {"rd_addr", 0U}, {"rd_en", 0U}, {"rd_bank_sel", 1U}
    });
    step();
    vectors.vectors.push_back({cycle(), {
        {"cfg_ncols", 3072U}, {"cfg_afe_count", 12U}, {"wr_addr", 3071U}, {"wr_data", 0x5678U}, {"wr_en", 1U},
        {"wr_bank_sel", 1U}, {"rd_addr", 0U}, {"rd_en", 0U}, {"rd_bank_sel", 1U}
    }, get_outputs()});

    set_inputs({
        {"cfg_ncols", 3072U}, {"cfg_afe_count", 12U}, {"wr_addr", 32U},
        {"wr_samples", std::vector<uint16_t>{0x1010U, 0x2020U, 0x3030U, 0x4040U}},
        {"wr_en", 1U}, {"wr_bank_sel", 0U}, {"rd_addr", 0U}, {"rd_en", 0U}, {"rd_bank_sel", 0U}
    });
    step();
    vectors.vectors.push_back({cycle(), {
        {"cfg_ncols", 3072U}, {"cfg_afe_count", 12U}, {"wr_addr", 32U},
        {"wr_samples", std::vector<uint16_t>{0x1010U, 0x2020U, 0x3030U, 0x4040U}},
        {"wr_en", 1U}, {"wr_bank_sel", 0U}, {"rd_addr", 0U}, {"rd_en", 0U}, {"rd_bank_sel", 0U}
    }, get_outputs()});

    WriteHexVectors(vectors, output_dir + "/line_buf_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/line_buf_vectors.bin");
}

}  // namespace fpd::sim
