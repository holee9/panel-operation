#include "golden_models/models/Csi2PacketModel.h"

#include "golden_models/core/CRC16.h"
#include "golden_models/core/ECC.h"

namespace fpd::sim {

void Csi2PacketModel::reset() {
    pixels_.clear();
    packet_size_ = 0;
    crc_ = 0;
    cycle_count_ = 0;
}

std::vector<uint8_t> Csi2PacketModel::BuildLongPacket(const std::vector<uint16_t>& pixels) const {
    std::vector<uint8_t> payload;
    payload.reserve(pixels.size() * 2U + 6U);
    for (const auto pixel : pixels) {
        payload.push_back(static_cast<uint8_t>((pixel >> 8) & 0xFFU));
        payload.push_back(static_cast<uint8_t>(pixel & 0xFFU));
    }

    const uint16_t word_count = static_cast<uint16_t>(payload.size());
    const uint32_t header24 =
        static_cast<uint32_t>(0x2EU) |
        (static_cast<uint32_t>(word_count & 0x00FFU) << 8U) |
        (static_cast<uint32_t>((word_count >> 8U) & 0x00FFU) << 16U);
    const uint8_t ecc = ComputeCsi2Ecc(header24);
    const uint16_t crc = Crc16Ccitt(payload);

    std::vector<uint8_t> packet;
    packet.push_back(0x2EU);
    packet.push_back(static_cast<uint8_t>(word_count & 0xFFU));
    packet.push_back(static_cast<uint8_t>((word_count >> 8U) & 0xFFU));
    packet.push_back(ecc);
    packet.insert(packet.end(), payload.begin(), payload.end());
    packet.push_back(static_cast<uint8_t>(crc & 0xFFU));
    packet.push_back(static_cast<uint8_t>((crc >> 8U) & 0xFFU));
    return packet;
}

void Csi2PacketModel::step() {
    auto packet = BuildLongPacket(pixels_);
    packet_size_ = static_cast<uint32_t>(packet.size());
    if (packet.size() >= 2U) {
        crc_ = static_cast<uint32_t>(packet[packet.size() - 2U]) |
               (static_cast<uint32_t>(packet[packet.size() - 1U]) << 8U);
    } else {
        crc_ = 0;
    }
    ++cycle_count_;
}

void Csi2PacketModel::set_inputs(const SignalMap& inputs) {
    pixels_ = GetVector(inputs, "pixels");
}

SignalMap Csi2PacketModel::get_outputs() const {
    return {
        {"packet_size", packet_size_},
        {"crc16", crc_},
    };
}

std::vector<Mismatch> Csi2PacketModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void Csi2PacketModel::generate_vectors(const std::string& output_dir) {
    (void)output_dir;
}

}  // namespace fpd::sim
