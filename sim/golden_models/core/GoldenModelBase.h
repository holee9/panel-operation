#pragma once

#include <cstdint>
#include <string>
#include <vector>

#include "golden_models/core/SignalTypes.h"

namespace fpd::sim {

class GoldenModelBase {
public:
    virtual ~GoldenModelBase() = default;

    virtual void reset() = 0;
    virtual void step() = 0;
    virtual void set_inputs(const SignalMap& inputs) = 0;
    virtual SignalMap get_outputs() const = 0;
    virtual std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const = 0;
    virtual void generate_vectors(const std::string& output_dir) = 0;

    uint64_t cycle() const { return cycle_count_; }

protected:
    uint64_t cycle_count_ = 0;
};

std::vector<Mismatch> CompareSignalMaps(
    uint64_t cycle,
    const SignalMap& expected,
    const SignalMap& actual);

}  // namespace fpd::sim
