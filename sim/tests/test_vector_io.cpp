#include <filesystem>
#include <iostream>
#include <vector>

#include "golden_models/core/TestVectorIO.h"
#include "tests/TestHelpers.h"

int main() {
    const auto path = std::filesystem::temp_directory_path() / "fpd_test_vectors.hex";

    fpd::sim::TestVectorFile file;
    file.module_name = "demo";
    file.spec_name = "SPEC-FPD-SIM-001";
    file.clock_name = "sys_clk";
    file.signal_inputs = {"a", "vec"};
    file.signal_outputs = {"b"};
    file.vectors.push_back({1U, {{"a", 1U}, {"vec", std::vector<uint16_t>{0x12U, 0x34U}}}, {{"b", 2U}}});

    fpd::sim::WriteHexVectors(file, path.string());
    const auto loaded = fpd::sim::ReadHexVectors(path.string());
    Expect(loaded.module_name == "demo", "module metadata should round-trip");
    Expect(loaded.vectors.size() == 1U, "one vector should load back");
    Expect(std::get<uint32_t>(loaded.vectors.front().inputs.at("a")) == 1U,
           "hex vector reader should round-trip scalar inputs");

    auto bin_path = path;
    bin_path.replace_extension(".bin");
    fpd::sim::WriteBinaryVectors(file, bin_path.string());
    const auto loaded_bin = fpd::sim::ReadBinaryVectors(bin_path.string());
    Expect(loaded_bin.spec_name == "SPEC-FPD-SIM-001", "binary metadata should round-trip");
    Expect(loaded_bin.vectors.size() == 1U, "one binary vector should load back");
    Expect(std::get<std::vector<uint16_t>>(loaded_bin.vectors.front().inputs.at("vec")).size() == 2U,
           "binary vector reader should round-trip vector inputs");

    std::cout << "test_vector_io passed\n";
    return 0;
}
