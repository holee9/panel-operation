#include <iostream>

#include "golden_models/core/ECC.h"
#include "tests/TestHelpers.h"

int main() {
    const uint32_t header = 0x00102EU;
    const uint8_t ecc0 = fpd::sim::ComputeCsi2Ecc(header);
    const uint8_t ecc1 = fpd::sim::ComputeCsi2Ecc(header ^ 0x1U);

    Expect(ecc0 != ecc1, "ECC should change when the 24-bit header changes");

    std::cout << "test_ecc passed\n";
    return 0;
}
