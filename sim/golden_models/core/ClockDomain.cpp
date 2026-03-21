#include "golden_models/core/ClockDomain.h"

namespace fpd::sim {

ClockDomain::ClockDomain(uint64_t source_hz, uint64_t target_hz)
    : source_hz_(source_hz), target_hz_(target_hz) {}

bool ClockDomain::Tick() {
    accumulator_ += target_hz_;
    if (accumulator_ >= source_hz_) {
        accumulator_ -= source_hz_;
        return true;
    }
    return false;
}

void ClockDomain::Reset() {
    accumulator_ = 0;
}

}  // namespace fpd::sim
