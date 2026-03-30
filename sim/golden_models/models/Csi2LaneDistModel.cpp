#include "golden_models/models/Csi2LaneDistModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

namespace {

uint32_t NormalizeLaneCount(const uint32_t lane_count) {
    return lane_count >= 4U ? 4U : 2U;
}

}  // namespace

void Csi2LaneDistModel::reset() {
    packet_words_.clear();
    lane_count_ = 2;
    lane_sizes_ = {0, 0, 0, 0};
    last_lanes_ = {};
    interleave_state_ = 0;
    cycle_count_ = 0;
}

void Csi2LaneDistModel::SetLaneCount(const uint32_t lane_count) {
    lane_count_ = NormalizeLaneCount(lane_count);
}

std::array<std::vector<uint8_t>, 4> Csi2LaneDistModel::SplitLanes(
    const std::vector<uint8_t>& packet,
    uint8_t lanes) const {
    std::array<std::vector<uint8_t>, 4> result;
    const auto active_lanes = static_cast<uint8_t>(NormalizeLaneCount(lanes));
    const bool swap_pairing = active_lanes == 2U && (interleave_state_ % 2U) == 1U;
    for (std::size_t index = 0; index < packet.size(); ++index) {
        const auto lane_index = swap_pairing
            ? static_cast<std::size_t>((index + 1U) % active_lanes)
            : index % active_lanes;
        result[lane_index].push_back(packet[index]);
    }
    return result;
}

void Csi2LaneDistModel::step() {
    std::vector<uint8_t> packet_bytes;
    packet_bytes.reserve(packet_words_.size());
    for (const auto word : packet_words_) {
        packet_bytes.push_back(static_cast<uint8_t>(word & 0x00FFU));
    }
    last_lanes_ = SplitLanes(packet_bytes, static_cast<uint8_t>(lane_count_));
    for (std::size_t index = 0; index < lane_sizes_.size(); ++index) {
        lane_sizes_[index] = static_cast<uint32_t>(last_lanes_[index].size());
    }
    ++interleave_state_;
    ++cycle_count_;
}

void Csi2LaneDistModel::set_inputs(const SignalMap& inputs) {
    packet_words_ = GetVector(inputs, "packet_bytes");
    SetLaneCount(GetScalar(inputs, "lane_count", lane_count_));
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
