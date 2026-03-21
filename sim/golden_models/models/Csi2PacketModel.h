#pragma once

#include <vector>

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

class Csi2PacketModel : public GoldenModelBase {
public:
    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

    std::vector<uint8_t> BuildLongPacket(const std::vector<uint16_t>& pixels) const;

private:
    std::vector<uint16_t> pixels_;
    uint32_t packet_size_ = 0;
    uint32_t crc_ = 0;
};

}  // namespace fpd::sim
