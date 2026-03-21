#include "golden_models/core/CRC16.h"

namespace fpd::sim {

uint16_t Crc16Ccitt(const uint8_t* data, std::size_t size, uint16_t seed) {
    uint16_t crc = seed;
    for (std::size_t i = 0; i < size; ++i) {
        crc ^= static_cast<uint16_t>(data[i]) << 8;
        for (int bit = 0; bit < 8; ++bit) {
            if ((crc & 0x8000U) != 0U) {
                crc = static_cast<uint16_t>((crc << 1) ^ 0x1021U);
            } else {
                crc = static_cast<uint16_t>(crc << 1);
            }
        }
    }
    return crc;
}

uint16_t Crc16Ccitt(const std::vector<uint8_t>& data, uint16_t seed) {
    return Crc16Ccitt(data.data(), data.size(), seed);
}

}  // namespace fpd::sim
