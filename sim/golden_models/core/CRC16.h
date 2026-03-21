#pragma once

#include <cstddef>
#include <cstdint>
#include <vector>

namespace fpd::sim {

uint16_t Crc16Ccitt(const uint8_t* data, std::size_t size, uint16_t seed = 0xFFFFU);
uint16_t Crc16Ccitt(const std::vector<uint8_t>& data, uint16_t seed = 0xFFFFU);

}  // namespace fpd::sim
