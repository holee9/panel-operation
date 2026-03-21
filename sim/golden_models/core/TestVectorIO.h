#pragma once

#include <cstdint>
#include <string>
#include <vector>

#include "golden_models/core/SignalTypes.h"

namespace fpd::sim {

struct TestVector {
    uint64_t cycle = 0;
    SignalMap inputs;
    SignalMap outputs;
};

struct TestVectorFile {
    std::string module_name;
    std::string spec_name;
    std::vector<std::string> signal_inputs;
    std::vector<std::string> signal_outputs;
    std::vector<TestVector> vectors;
};

void WriteHexVectors(const TestVectorFile& vector_file, const std::string& path);

}  // namespace fpd::sim
