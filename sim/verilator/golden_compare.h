#pragma once

#include <cstdint>
#include <functional>
#include <string>

#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

using CompareStimulus = std::function<void(uint64_t, GoldenModelBase&)>;
using RtlSignalReader = std::function<bool(SignalMap&)>;

int RunGoldenCompare(
    GoldenModelBase& model,
    uint64_t cycles,
    const CompareStimulus& stimulus,
    const RtlSignalReader& rtl_reader,
    const std::string& banner);

}  // namespace fpd::sim
