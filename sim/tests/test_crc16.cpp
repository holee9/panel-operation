#include <iostream>
#include <vector>

#include "golden_models/core/CRC16.h"
#include "tests/TestHelpers.h"

int main() {
    const std::vector<uint8_t> data = {'1', '2', '3', '4', '5', '6', '7', '8', '9'};
    const uint16_t crc = fpd::sim::Crc16Ccitt(data);
    Expect(crc == 0x29B1U, "CRC16-CCITT should match reference vector");

    std::cout << "test_crc16 passed\n";
    return 0;
}
