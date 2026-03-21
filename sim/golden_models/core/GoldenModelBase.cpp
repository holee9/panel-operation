#include "golden_models/core/GoldenModelBase.h"

namespace fpd::sim {

std::vector<Mismatch> CompareSignalMaps(
    uint64_t cycle,
    const SignalMap& expected,
    const SignalMap& actual) {
    std::vector<Mismatch> mismatches;

    for (const auto& [name, expected_signal] : expected) {
        const auto actual_it = actual.find(name);
        const uint32_t expected_value = SignalScalar(expected_signal, 0U);
        const uint32_t actual_value =
            actual_it == actual.end() ? 0U : SignalScalar(actual_it->second, 0U);
        if (actual_it == actual.end() || actual_value != expected_value) {
            mismatches.push_back(Mismatch{cycle, name, expected_value, actual_value});
        }
    }

    return mismatches;
}

}  // namespace fpd::sim
