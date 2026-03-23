#include "golden_models/models/Csi2PacketModel.h"
#include "verilator/golden_compare.h"

int main() {
    fpd::sim::Csi2PacketModel model;
    return fpd::sim::RunGoldenCompare(
        model,
        2U,
        [](uint64_t, fpd::sim::GoldenModelBase& base) {
            auto& csi2 = static_cast<fpd::sim::Csi2PacketModel&>(base);
            csi2.set_inputs({{"pixels", std::vector<uint16_t>{0x1234U, 0x5678U}}});
        },
        [](fpd::sim::SignalMap& rtl_outputs) {
            rtl_outputs.clear();
            return false;
        },
        "CSI-2 compare requires real RTL binding");
}
