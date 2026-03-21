#include <iostream>

#include "golden_models/models/FoundationConstants.h"
#include "golden_models/models/SpiSlaveModel.h"
#include "tests/TestHelpers.h"

int main() {
    fpd::sim::SpiSlaveModel model;

    model.WriteRegister(fpd::sim::kRegMode, 0x0002);
    Expect(model.ReadRegister(fpd::sim::kRegMode) == 0x0002,
           "SPI model should be able to access backing register bank");

    model.WriteRegister(fpd::sim::kRegVersion, 0x2222);
    Expect(model.ReadRegister(fpd::sim::kRegVersion) == fpd::sim::kVersion10,
           "Read-only behavior should be preserved through SPI model");

    model.reset();
    model.set_inputs({
        {"spi_cs_n", 1U},
        {"spi_sclk", 1U},
        {"spi_mosi", 0U},
        {"spi_mode", 3U},
    });
    model.step();
    Expect(fpd::sim::GetScalar(model.get_outputs(), "reg_wr_en") == 0U,
           "Mode 3 idle should not create a write strobe");

    std::cout << "test_spi_model passed\n";
    return 0;
}
