#pragma once

#include <array>
#include <cstdint>
#include <string>
#include <vector>

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class RegBankModel : public GoldenModelBase {
public:
    RegBankModel();

    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

    uint16_t Read(uint8_t addr) const;
    void Write(uint8_t addr, uint16_t value);
    bool tline_clamped() const { return tline_clamped_; }
    void clear_tline_clamped() { tline_clamped_ = false; }
    void SetStatus(
        bool busy,
        bool done,
        bool error,
        bool line_rdy,
        uint16_t line_idx,
        uint8_t err_code);

private:
    std::array<uint16_t, 32> regs_{};
    bool sts_busy_ = false;
    bool sts_done_ = false;
    bool sts_error_ = false;
    bool sts_line_rdy_ = false;
    uint16_t sts_line_idx_ = 0;
    uint8_t sts_err_code_ = 0;

    bool tline_clamped_ = false;

    uint32_t in_reg_addr_ = 0;
    uint32_t in_reg_wdata_ = 0;
    uint32_t in_reg_wr_en_ = 0;
    uint32_t in_reg_rd_en_ = 0;
};

}  // namespace fpd::sim
