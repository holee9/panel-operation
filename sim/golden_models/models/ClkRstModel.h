#pragma once

#include <cstdint>
#include <string>
#include <vector>

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class ClkRstModel : public GoldenModelBase {
public:
    explicit ClkRstModel(uint64_t afe_clk_hz = 10000000ULL);

    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

private:
    static constexpr uint64_t kSysClkHz = 100000000ULL;
    static constexpr uint64_t kMclkHz = 32000000ULL;

    uint64_t afe_clk_hz_ = 0;
    uint64_t phase_acc_aclk_ = 0;
    uint64_t phase_acc_mclk_ = 0;
    bool rst_ext_n_ = false;
    uint8_t afe_type_sel_ = 0;
    bool clk_afe_ = false;
    bool clk_aclk_ = false;
    bool clk_mclk_ = false;
    bool pll_locked_ = false;
    bool rst_ff1_ = false;
    bool rst_ff2_ = false;
    uint32_t lock_counter_ = 0;
};

}  // namespace fpd::sim
