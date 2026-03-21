#include <iostream>
#include <vector>

#include "golden_models/models/Csi2PacketModel.h"
#include "tests/TestHelpers.h"

int main() {
    fpd::sim::Csi2PacketModel model;
    const std::vector<uint16_t> pixels = {0x1234, 0x5678};
    const auto packet = model.BuildLongPacket(pixels);

    Expect(packet.size() == 10U, "CSI-2 packet should include 4-byte header and 2-byte CRC");
    Expect(packet[0] == 0x2EU, "CSI-2 packet should use RAW16 data type");

    std::cout << "test_csi2_model passed\n";
    return 0;
}
