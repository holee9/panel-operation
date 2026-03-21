#include "golden_models/models/RegBankModel.h"

#include <filesystem>

#include "golden_models/core/TestVectorIO.h"
#include "golden_models/models/FoundationConstants.h"

namespace fpd::sim {

RegBankModel::RegBankModel() {
    reset();
}

void RegBankModel::reset() {
    regs_ = MakeDefaultRegisters();
    regs_[kRegCtrl] = 0;
    sts_busy_ = false;
    sts_done_ = false;
    sts_error_ = false;
    sts_line_rdy_ = false;
    sts_line_idx_ = 0;
    sts_err_code_ = 0;
    in_reg_addr_ = 0;
    in_reg_wdata_ = 0;
    in_reg_wr_en_ = 0;
    in_reg_rd_en_ = 0;
    cycle_count_ = 0;
}

void RegBankModel::step() {
    if (in_reg_wr_en_ != 0U) {
        Write(static_cast<uint8_t>(in_reg_addr_), static_cast<uint16_t>(in_reg_wdata_));
    }

    regs_[kRegCtrl] &= static_cast<uint16_t>(~0x0003U);
    ++cycle_count_;
}

void RegBankModel::set_inputs(const SignalMap& inputs) {
    in_reg_addr_ = GetScalar(inputs, "reg_addr", in_reg_addr_) & 0x1FU;
    in_reg_wdata_ = GetScalar(inputs, "reg_wdata", in_reg_wdata_) & 0xFFFFU;
    in_reg_wr_en_ = GetScalar(inputs, "reg_wr_en", in_reg_wr_en_) & 0x1U;
    in_reg_rd_en_ = GetScalar(inputs, "reg_rd_en", in_reg_rd_en_) & 0x1U;
    sts_busy_ = (GetScalar(inputs, "sts_busy", sts_busy_ ? 1U : 0U) & 0x1U) != 0U;
    sts_done_ = (GetScalar(inputs, "sts_done", sts_done_ ? 1U : 0U) & 0x1U) != 0U;
    sts_error_ = (GetScalar(inputs, "sts_error", sts_error_ ? 1U : 0U) & 0x1U) != 0U;
    sts_line_rdy_ =
        (GetScalar(inputs, "sts_line_rdy", sts_line_rdy_ ? 1U : 0U) & 0x1U) != 0U;
    sts_line_idx_ = static_cast<uint16_t>(
        GetScalar(inputs, "sts_line_idx", sts_line_idx_) & 0x0FFFU);
    sts_err_code_ = static_cast<uint8_t>(
        GetScalar(inputs, "sts_err_code", sts_err_code_) & 0xFFU);
}

SignalMap RegBankModel::get_outputs() const {
    const uint16_t status = static_cast<uint16_t>(
        (sts_busy_ ? 0x1U : 0U) |
        (sts_done_ ? 0x2U : 0U) |
        (sts_error_ ? 0x4U : 0U) |
        (sts_line_rdy_ ? 0x8U : 0U));

    return {
        {"reg_rdata", Read(static_cast<uint8_t>(in_reg_addr_))},
        {"cfg_mode", regs_[kRegMode] & 0x7U},
        {"cfg_combo", regs_[kRegCombo] & 0x7U},
        {"cfg_nrows", regs_[kRegNRows] & 0x0FFFU},
        {"cfg_ncols", regs_[kRegNCols] & 0x0FFFU},
        {"cfg_tline", regs_[kRegTLine]},
        {"cfg_treset", regs_[kRegTReset]},
        {"cfg_tinteg", regs_[kRegTInteg]},
        {"cfg_tgate_on", regs_[kRegTGateOn] & 0x0FFFU},
        {"cfg_tgate_settle", regs_[kRegTGateSettle] & 0xFFU},
        {"cfg_afe_ifs", regs_[kRegAfeIfs] & 0x3FU},
        {"cfg_afe_lpf", regs_[kRegAfeLpf] & 0xFU},
        {"cfg_afe_pmode", regs_[kRegAfePMode] & 0x3U},
        {"cfg_cic_en", regs_[kRegCicEn] & 0x1U},
        {"cfg_cic_profile", regs_[kRegCicProfile] & 0xFU},
        {"cfg_pipeline_en", (regs_[kRegCicProfile] >> 4) & 0x1U},
        {"cfg_tp_sel", (regs_[kRegCicProfile] >> 5) & 0x1U},
        {"cfg_scan_dir", regs_[kRegScanDir] & 0x1U},
        {"cfg_gate_sel", regs_[kRegGateSel] & 0x3U},
        {"cfg_afe_nchip", regs_[kRegAfeNChip] & 0xFU},
        {"cfg_sync_dly", regs_[kRegSyncDly] & 0xFFU},
        {"cfg_nreset", regs_[kRegNReset] & 0xFFU},
        {"cfg_irq_en", (regs_[kRegCtrl] >> 3) & 0xFFU},
        {"ctrl_start", regs_[kRegCtrl] & 0x1U},
        {"ctrl_abort", (regs_[kRegCtrl] >> 1) & 0x1U},
        {"ctrl_irq_global_en", (regs_[kRegCtrl] >> 2) & 0x1U},
        {"status_word", status},
    };
}

std::vector<Mismatch> RegBankModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void RegBankModel::generate_vectors(const std::string& output_dir) {
    std::filesystem::create_directories(output_dir);

    TestVectorFile vector_file;
    vector_file.module_name = "reg_bank";
    vector_file.spec_name = "SPEC-FPD-001";
    vector_file.signal_inputs = {"reg_addr", "reg_wdata", "reg_wr_en", "reg_rd_en"};
    vector_file.signal_outputs = {"reg_rdata", "cfg_mode", "cfg_combo"};

    reset();

    set_inputs({
        {"reg_addr", kRegMode},
        {"reg_wdata", 0x0004},
        {"reg_wr_en", 1},
        {"reg_rd_en", 0},
    });
    step();
    vector_file.vectors.push_back(TestVector{
        cycle(),
        {{"reg_addr", kRegMode}, {"reg_wdata", 0x0004}, {"reg_wr_en", 1}, {"reg_rd_en", 0}},
        get_outputs(),
    });

    set_inputs({
        {"reg_addr", kRegMode},
        {"reg_wdata", 0x0000},
        {"reg_wr_en", 0},
        {"reg_rd_en", 1},
    });
    step();
    vector_file.vectors.push_back(TestVector{
        cycle(),
        {{"reg_addr", kRegMode}, {"reg_wdata", 0x0000}, {"reg_wr_en", 0}, {"reg_rd_en", 1}},
        get_outputs(),
    });

    WriteHexVectors(vector_file, output_dir + "/reg_bank.hex");
}

uint16_t RegBankModel::Read(uint8_t addr) const {
    switch (addr) {
        case kRegStatus:
            return static_cast<uint16_t>(
                (sts_busy_ ? 0x1U : 0U) |
                (sts_done_ ? 0x2U : 0U) |
                (sts_error_ ? 0x4U : 0U) |
                (sts_line_rdy_ ? 0x8U : 0U));
        case kRegLineIdx:
            return static_cast<uint16_t>(sts_line_idx_ & 0x0FFFU);
        case kRegErrCode:
            return sts_err_code_;
        default:
            return regs_[addr & 0x1FU];
    }
}

void RegBankModel::Write(uint8_t addr, uint16_t value) {
    addr &= 0x1FU;
    if (!IsReadOnlyRegister(addr)) {
        regs_[addr] = value;
    }
}

void RegBankModel::SetStatus(
    bool busy,
    bool done,
    bool error,
    bool line_rdy,
    uint16_t line_idx,
    uint8_t err_code) {
    sts_busy_ = busy;
    sts_done_ = done;
    sts_error_ = error;
    sts_line_rdy_ = line_rdy;
    sts_line_idx_ = static_cast<uint16_t>(line_idx & 0x0FFFU);
    sts_err_code_ = err_code;
}

}  // namespace fpd::sim
