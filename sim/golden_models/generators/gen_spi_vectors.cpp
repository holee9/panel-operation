#include <filesystem>
#include <iostream>

#include "golden_models/models/ClkRstModel.h"
#include "golden_models/models/RegBankModel.h"
#include "golden_models/models/SpiSlaveModel.h"

int main() {
    const std::filesystem::path out_dir =
        std::filesystem::current_path() / "sim" / "testvectors" / "spec001";
    std::filesystem::create_directories(out_dir);

    fpd::sim::RegBankModel reg_bank_model;
    reg_bank_model.generate_vectors(out_dir.string());

    fpd::sim::SpiSlaveModel spi_model;
    spi_model.generate_vectors(out_dir.string());

    fpd::sim::ClkRstModel clk_model;
    clk_model.generate_vectors(out_dir.string());

    std::cout << "Generated SPEC-FPD-001 vectors at " << out_dir.string() << "\n";
    return 0;
}
