#pragma once

#include <cstdint>
#include <map>
#include <string>
#include <variant>
#include <vector>

namespace fpd::sim {

using SignalValue = std::variant<uint32_t, std::vector<uint16_t>>;
using SignalMap = std::map<std::string, SignalValue>;

struct Mismatch {
    uint64_t cycle = 0;
    std::string signal_name;
    uint32_t expected = 0;
    uint32_t actual = 0;
};

inline bool IsScalarSignal(const SignalValue& value) {
    return std::holds_alternative<uint32_t>(value);
}

inline uint32_t SignalScalar(const SignalValue& value, uint32_t fallback = 0U) {
    return IsScalarSignal(value) ? std::get<uint32_t>(value) : fallback;
}

inline std::vector<uint16_t> SignalVector(const SignalValue& value) {
    if (std::holds_alternative<std::vector<uint16_t>>(value)) {
        return std::get<std::vector<uint16_t>>(value);
    }
    return {};
}

inline uint32_t GetScalar(const SignalMap& signals, const std::string& name, uint32_t fallback = 0U) {
    const auto it = signals.find(name);
    if (it == signals.end()) {
        return fallback;
    }
    return SignalScalar(it->second, fallback);
}

inline std::vector<uint16_t> GetVector(const SignalMap& signals, const std::string& name) {
    const auto it = signals.find(name);
    if (it == signals.end()) {
        return {};
    }
    return SignalVector(it->second);
}

}  // namespace fpd::sim
