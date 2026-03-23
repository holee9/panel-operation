#include "golden_models/models/Csi2LaneDistModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

void Csi2LaneDistModel::reset() {
    packet_words_.clear();
    lane_count_ = 2;
    lane_sizes_ = {0, 0, 0, 0};
    cycle_count_ = 0;
}

std::array<std::vector<uint8_t>, 4> Csi2LaneDistModel::SplitLanes(
    const std::vector<uint8_t>& packet,
    uint8_t lanes) const {
    std::array<std::vector<uint8_t>, 4> result;
    const uint8_t active_lanes = (lanes < 2U) ? 2U : lanes;
    for (std::size_t index = 0; index < packet.size(); ++index) {
        result[index % active_lanes].push_back(packet[index]);
    }
    return result;
}

void Csi2LaneDistModel::step() {
    std::vector<uint8_t> packet_bytes;
    packet_bytes.reserve(packet_words_.size());
    for (const auto word : packet_words_) {
        packet_bytes.push_back(static_cast<uint8_t>(word & 0x00FFU));
    }
    const auto lanes = SplitLanes(packet_bytes, static_cast<uint8_t>(lane_count_));
    for (std::size_t index = 0; index < lane_sizes_.size(); ++index) {
        lane_sizes_[index] = static_cast<uint32_t>(lanes[index].size());
    }
    ++cycle_count_;
}

void Csi2LaneDistModel::set_inputs(const SignalMap& inputs) {
    packet_words_ = GetVector(inputs, "packet_bytes");
    lane_count_ = GetScalar(inputs, "lane_count", lane_count_);
}

SignalMap Csi2LaneDistModel::get_outputs() const {
    return {
        {"lane0_size", lane_sizes_[0]},
        {"lane1_size", lane_sizes_[1]},
        {"lane2_size", lane_sizes_[2]},
        {"lane3_size", lane_sizes_[3]},
    };
}

std::vector<Mismatch> Csi2LaneDistModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void Csi2LaneDistModel::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "csi2_lane_dist";
    vectors.spec_name = "SPEC-FPD-007";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {"packet_bytes", "lane_count"};
    vectors.signal_outputs = {"lane0_size", "lane1_size", "lane2_size", "lane3_size"};
    reset();
    set_inputs({{"packet_bytes", std::vector<uint16_t>{0U, 1U, 2U, 3U, 4U, 5U}}, {"lane_count", 4U}});
    step();
    vectors.vectors.push_back({cycle(), {{"packet_bytes", std::vector<uint16_t>{0U, 1U, 2U, 3U, 4U, 5U}}, {"lane_count", 4U}}, get_outputs()});
    WriteHexVectors(vectors, output_dir + "/csi2_lane_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/csi2_lane_vectors.bin");
}

}  // namespace fpd::sim
