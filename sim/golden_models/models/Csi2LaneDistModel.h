#pragma once

#include <array>
#include <vector>

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class Csi2LaneDistModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

    std::array<std::vector<uint8_t>, 4> SplitLanes(const std::vector<uint8_t>& packet, uint8_t lanes) const;

private:
    std::vector<uint16_t> packet_words_;
    uint32_t lane_count_ = 2;
    std::array<uint32_t, 4> lane_sizes_{};
};

}  // namespace fpd::sim
