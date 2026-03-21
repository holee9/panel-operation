#include "golden_models/core/ECC.h"

namespace fpd::sim {

namespace {

uint8_t Bit(uint32_t value, int index) {
    return static_cast<uint8_t>((value >> index) & 0x1U);
}

}  // namespace

uint8_t ComputeCsi2Ecc(uint32_t header24) {
    const uint8_t p0 = Bit(header24, 0) ^ Bit(header24, 1) ^ Bit(header24, 2) ^
                       Bit(header24, 4) ^ Bit(header24, 5) ^ Bit(header24, 7) ^
                       Bit(header24, 10) ^ Bit(header24, 11) ^ Bit(header24, 13) ^
                       Bit(header24, 16) ^ Bit(header24, 20) ^ Bit(header24, 21) ^
                       Bit(header24, 22) ^ Bit(header24, 23);
    const uint8_t p1 = Bit(header24, 0) ^ Bit(header24, 1) ^ Bit(header24, 3) ^
                       Bit(header24, 4) ^ Bit(header24, 6) ^ Bit(header24, 8) ^
                       Bit(header24, 10) ^ Bit(header24, 12) ^ Bit(header24, 14) ^
                       Bit(header24, 17) ^ Bit(header24, 20) ^ Bit(header24, 21) ^
                       Bit(header24, 22) ^ Bit(header24, 23);
    const uint8_t p2 = Bit(header24, 0) ^ Bit(header24, 2) ^ Bit(header24, 3) ^
                       Bit(header24, 5) ^ Bit(header24, 6) ^ Bit(header24, 9) ^
                       Bit(header24, 11) ^ Bit(header24, 12) ^ Bit(header24, 15) ^
                       Bit(header24, 18) ^ Bit(header24, 20) ^ Bit(header24, 21) ^
                       Bit(header24, 22);
    const uint8_t p3 = Bit(header24, 1) ^ Bit(header24, 2) ^ Bit(header24, 3) ^
                       Bit(header24, 7) ^ Bit(header24, 8) ^ Bit(header24, 9) ^
                       Bit(header24, 13) ^ Bit(header24, 14) ^ Bit(header24, 15) ^
                       Bit(header24, 19) ^ Bit(header24, 20) ^ Bit(header24, 21) ^
                       Bit(header24, 23);
    const uint8_t p4 = Bit(header24, 4) ^ Bit(header24, 5) ^ Bit(header24, 6) ^
                       Bit(header24, 7) ^ Bit(header24, 8) ^ Bit(header24, 9) ^
                       Bit(header24, 16) ^ Bit(header24, 17) ^ Bit(header24, 18) ^
                       Bit(header24, 19) ^ Bit(header24, 20) ^ Bit(header24, 22) ^
                       Bit(header24, 23);
    const uint8_t p5 = Bit(header24, 10) ^ Bit(header24, 11) ^ Bit(header24, 12) ^
                       Bit(header24, 13) ^ Bit(header24, 14) ^ Bit(header24, 15) ^
                       Bit(header24, 16) ^ Bit(header24, 17) ^ Bit(header24, 18) ^
                       Bit(header24, 19) ^ Bit(header24, 21) ^ Bit(header24, 22) ^
                       Bit(header24, 23);

    return static_cast<uint8_t>((p0 << 0) | (p1 << 1) | (p2 << 2) |
                                (p3 << 3) | (p4 << 4) | (p5 << 5));
}

}  // namespace fpd::sim
