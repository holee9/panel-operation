#pragma once

#include <cstdint>

namespace fpd::sim {

class ClockDomain {
public:
    ClockDomain(uint64_t source_hz, uint64_t target_hz);

    bool Tick();
    void Reset();

private:
    uint64_t source_hz_ = 0;
    uint64_t target_hz_ = 0;
    uint64_t accumulator_ = 0;
};

}  // namespace fpd::sim
