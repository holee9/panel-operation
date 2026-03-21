#include "golden_models/core/TestVectorIO.h"

#include <fstream>
#include <iomanip>
#include <sstream>

namespace fpd::sim {

namespace {

std::string FormatSignalValue(const SignalValue& value) {
    if (std::holds_alternative<uint32_t>(value)) {
        std::ostringstream stream;
        stream << std::hex << std::setfill('0') << std::setw(8)
               << std::get<uint32_t>(value);
        return stream.str();
    }

    std::ostringstream stream;
    stream << "[";
    const auto& values = std::get<std::vector<uint16_t>>(value);
    for (std::size_t index = 0; index < values.size(); ++index) {
        if (index != 0U) {
            stream << ",";
        }
        stream << std::hex << std::setfill('0') << std::setw(4) << values[index];
    }
    stream << "]";
    return stream.str();
}

}  // namespace

void WriteHexVectors(const TestVectorFile& vector_file, const std::string& path) {
    std::ofstream out(path, std::ios::trunc);
    out << "@MODULE " << vector_file.module_name << "\n";
    out << "@SPEC " << vector_file.spec_name << "\n";

    out << "@SIGNALS_IN";
    for (const auto& signal : vector_file.signal_inputs) {
        out << " " << signal;
    }
    out << "\n";

    out << "@SIGNALS_OUT";
    for (const auto& signal : vector_file.signal_outputs) {
        out << " " << signal;
    }
    out << "\n";

    out << std::hex << std::setfill('0');
    for (const auto& vector : vector_file.vectors) {
        out << std::setw(8) << vector.cycle;
        for (const auto& signal : vector_file.signal_inputs) {
            const auto it = vector.inputs.find(signal);
            out << " " << FormatSignalValue(
                it == vector.inputs.end() ? SignalValue{0U} : it->second);
        }
        for (const auto& signal : vector_file.signal_outputs) {
            const auto it = vector.outputs.find(signal);
            out << " " << FormatSignalValue(
                it == vector.outputs.end() ? SignalValue{0U} : it->second);
        }
        out << "\n";
    }
}

}  // namespace fpd::sim
