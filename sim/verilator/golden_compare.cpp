#include "verilator/golden_compare.h"

#include <iostream>

namespace fpd::sim {

int RunGoldenCompare(
    GoldenModelBase& model,
    uint64_t cycles,
    const CompareStimulus& stimulus,
    const RtlSignalReader& rtl_reader,
    const std::string& banner) {
    std::cout << "[golden_compare] " << banner << "\n";
    model.reset();
    uint64_t mismatches = 0;
    for (uint64_t cycle = 0; cycle < cycles; ++cycle) {
        if (stimulus) {
            stimulus(cycle, model);
        }
        model.step();
        SignalMap rtl_outputs;
        if (!rtl_reader || !rtl_reader(rtl_outputs)) {
            std::cout << "golden compare aborted: RTL signal reader is not bound\n";
            return 2;
        }
        const auto diffs = model.compare(rtl_outputs);
        mismatches += diffs.size();
        if (!diffs.empty()) {
            std::cout << "mismatch cycle=" << diffs.front().cycle
                      << " signal=" << diffs.front().signal_name << "\n";
            break;
        }
    }
    return mismatches == 0 ? 0 : 1;
}

}  // namespace fpd::sim
