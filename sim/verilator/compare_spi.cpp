#include "golden_models/models/SpiSlaveModel.h"
#include "verilator/golden_compare.h"

int main() {
    fpd::sim::SpiSlaveModel model;
    return fpd::sim::RunGoldenCompare(
        model,
        4U,
        [](uint64_t cycle, fpd::sim::GoldenModelBase& base) {
            auto& spi = static_cast<fpd::sim::SpiSlaveModel&>(base);
            spi.set_inputs({
                {"spi_cs_n", cycle == 0U ? 0U : 1U},
                {"spi_sclk", static_cast<uint32_t>(cycle & 0x1U)},
                {"spi_mosi", cycle == 0U ? 1U : 0U}
            });
        },
        [](fpd::sim::SignalMap& rtl_outputs) {
            rtl_outputs.clear();
            return false;
        },
        "SPI compare requires real RTL binding");
}
