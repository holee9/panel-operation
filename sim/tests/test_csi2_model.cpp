#include <iostream>
#include <vector>

#include "golden_models/core/ECC.h"
#include "golden_models/models/Csi2PacketModel.h"
#include "tests/TestHelpers.h"

int main() {
    fpd::sim::Csi2PacketModel model;
    const std::vector<uint16_t> pixels = {0x1234, 0x5678};
    const auto packet = model.BuildLongPacket(pixels);
    const uint32_t header24 = 0x2EU | (4U << 8U);

    Expect(packet.size() == 10U, "CSI-2 packet should include 4-byte header and 2-byte CRC");
    Expect(packet[0] == 0x2EU, "CSI-2 packet should use RAW16 data type");
    Expect(packet[3] == fpd::sim::ComputeCsi2Ecc(header24),
           "CSI-2 packet should emit the computed header ECC");

    std::cout << "test_csi2_model passed\n";
    return 0;
}
