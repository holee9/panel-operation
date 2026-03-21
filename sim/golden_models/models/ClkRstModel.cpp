#include "golden_models/models/ClkRstModel.h"

#include <filesystem>

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

ClkRstModel::ClkRstModel(uint64_t afe_clk_hz) : afe_clk_hz_(afe_clk_hz) {
    reset();
}

void ClkRstModel::reset() {
    phase_acc_aclk_ = 0;
    phase_acc_mclk_ = 0;
    rst_ext_n_ = false;
    afe_type_sel_ = 0;
    clk_afe_ = false;
    clk_aclk_ = false;
    clk_mclk_ = false;
    pll_locked_ = false;
    rst_ff1_ = false;
    rst_ff2_ = false;
    lock_counter_ = 0;
    cycle_count_ = 0;
}

void ClkRstModel::step() {
    if (!rst_ext_n_) {
        phase_acc_aclk_ = 0;
        phase_acc_mclk_ = 0;
        clk_afe_ = false;
        clk_aclk_ = false;
        clk_mclk_ = false;
        pll_locked_ = false;
        rst_ff1_ = false;
        rst_ff2_ = false;
        lock_counter_ = 0;
    } else {
        const uint64_t phase_step_aclk = (afe_clk_hz_ << 32U) / kSysClkHz;
        const uint64_t phase_step_mclk = (kMclkHz << 32U) / kSysClkHz;
        phase_acc_aclk_ = (phase_acc_aclk_ + phase_step_aclk) & 0xFFFFFFFFULL;
        phase_acc_mclk_ = (phase_acc_mclk_ + phase_step_mclk) & 0xFFFFFFFFULL;
        clk_aclk_ = ((phase_acc_aclk_ >> 31U) & 0x1ULL) != 0ULL;
        clk_mclk_ = ((phase_acc_mclk_ >> 31U) & 0x1ULL) != 0ULL;
        clk_afe_ = (afe_type_sel_ == 2U) ? clk_mclk_ : clk_aclk_;

        if (!pll_locked_) {
            ++lock_counter_;
            if (lock_counter_ >= 16U) {
                pll_locked_ = true;
            }
        }

        rst_ff1_ = pll_locked_;
        rst_ff2_ = rst_ff1_;
    }

    ++cycle_count_;
}

void ClkRstModel::set_inputs(const SignalMap& inputs) {
    rst_ext_n_ = (GetScalar(inputs, "rst_ext_n", rst_ext_n_ ? 1U : 0U) & 0x1U) != 0U;
    afe_type_sel_ = static_cast<uint8_t>(GetScalar(inputs, "afe_type_sel", afe_type_sel_) & 0x3U);
}

SignalMap ClkRstModel::get_outputs() const {
    return {
        {"clk_afe", clk_afe_ ? 1U : 0U},
        {"clk_aclk", clk_aclk_ ? 1U : 0U},
        {"clk_mclk", clk_mclk_ ? 1U : 0U},
        {"clk_sys_out", 1U},
        {"rst_sync_n", rst_ff2_ ? 1U : 0U},
        {"pll_locked", pll_locked_ ? 1U : 0U},
    };
}

std::vector<Mismatch> ClkRstModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void ClkRstModel::generate_vectors(const std::string& output_dir) {
    std::filesystem::create_directories(output_dir);

    TestVectorFile vector_file;
    vector_file.module_name = "clk_rst_mgr";
    vector_file.spec_name = "SPEC-FPD-001";
    vector_file.signal_inputs = {"rst_ext_n", "afe_type_sel"};
    vector_file.signal_outputs = {"pll_locked", "rst_sync_n", "clk_afe"};

    reset();

    for (int i = 0; i < 20; ++i) {
        set_inputs({{"rst_ext_n", i < 2 ? 0U : 1U}, {"afe_type_sel", 0U}});
        step();
        vector_file.vectors.push_back(TestVector{
            cycle(),
            {{"rst_ext_n", i < 2 ? 0U : 1U}, {"afe_type_sel", 0U}},
            get_outputs(),
        });
    }

    WriteHexVectors(vector_file, output_dir + "/clk_rst_mgr.hex");
}

}  // namespace fpd::sim
