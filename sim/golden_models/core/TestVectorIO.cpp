#include "golden_models/core/TestVectorIO.h"

#include <filesystem>
#include <fstream>
#include <iomanip>
#include <sstream>
#include <stdexcept>

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

SignalValue ParseSignalValue(const std::string& token) {
    if (!token.empty() && token.front() == '[' && token.back() == ']') {
        std::vector<uint16_t> values;
        std::stringstream stream(token.substr(1, token.size() - 2));
        std::string item;
        while (std::getline(stream, item, ',')) {
            if (!item.empty()) {
                values.push_back(static_cast<uint16_t>(std::stoul(item, nullptr, 16)));
            }
        }
        return values;
    }
    return static_cast<uint32_t>(std::stoul(token, nullptr, 16));
}

void EnsureParentDir(const std::string& path) {
    const std::filesystem::path fs_path(path);
    if (fs_path.has_parent_path()) {
        std::filesystem::create_directories(fs_path.parent_path());
    }
}

void WriteStringWithSize(std::ofstream& out, const std::string& value) {
    const uint32_t size = static_cast<uint32_t>(value.size());
    out.write(reinterpret_cast<const char*>(&size), sizeof(size));
    out.write(value.data(), static_cast<std::streamsize>(value.size()));
}

std::string ReadStringWithSize(std::ifstream& in) {
    uint32_t size = 0U;
    in.read(reinterpret_cast<char*>(&size), sizeof(size));
    std::string value(size, '\0');
    if (size != 0U) {
        in.read(value.data(), static_cast<std::streamsize>(size));
    }
    return value;
}

void WriteSignalValue(std::ofstream& out, const SignalValue& value) {
    if (std::holds_alternative<uint32_t>(value)) {
        const uint8_t tag = 0U;
        const uint32_t scalar = std::get<uint32_t>(value);
        out.write(reinterpret_cast<const char*>(&tag), sizeof(tag));
        out.write(reinterpret_cast<const char*>(&scalar), sizeof(scalar));
        return;
    }

    const uint8_t tag = 1U;
    const auto& vector = std::get<std::vector<uint16_t>>(value);
    const uint32_t size = static_cast<uint32_t>(vector.size());
    out.write(reinterpret_cast<const char*>(&tag), sizeof(tag));
    out.write(reinterpret_cast<const char*>(&size), sizeof(size));
    out.write(reinterpret_cast<const char*>(vector.data()),
              static_cast<std::streamsize>(vector.size() * sizeof(uint16_t)));
}

SignalValue ReadSignalValue(std::ifstream& in) {
    uint8_t tag = 0U;
    in.read(reinterpret_cast<char*>(&tag), sizeof(tag));
    if (tag == 0U) {
        uint32_t scalar = 0U;
        in.read(reinterpret_cast<char*>(&scalar), sizeof(scalar));
        return scalar;
    }

    uint32_t size = 0U;
    in.read(reinterpret_cast<char*>(&size), sizeof(size));
    std::vector<uint16_t> values(size, 0U);
    if (size != 0U) {
        in.read(reinterpret_cast<char*>(values.data()),
                static_cast<std::streamsize>(size * sizeof(uint16_t)));
    }
    return values;
}

}  // namespace

void WriteHexVectors(const TestVectorFile& vector_file, const std::string& path) {
    EnsureParentDir(path);
    std::ofstream out(path, std::ios::trunc);
    out << "@MODULE " << vector_file.module_name << "\n";
    out << "@SPEC " << vector_file.spec_name << "\n";
    out << "@CLOCK " << (vector_file.clock_name.empty() ? "sys_clk" : vector_file.clock_name) << "\n";

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

void WriteBinaryVectors(const TestVectorFile& vector_file, const std::string& path) {
    EnsureParentDir(path);
    std::ofstream out(path, std::ios::binary | std::ios::trunc);
    const uint32_t magic = 0x46505631U;  // FPV1
    out.write(reinterpret_cast<const char*>(&magic), sizeof(magic));
    WriteStringWithSize(out, vector_file.module_name);
    WriteStringWithSize(out, vector_file.spec_name);
    WriteStringWithSize(out, vector_file.clock_name);

    const uint32_t input_count = static_cast<uint32_t>(vector_file.signal_inputs.size());
    const uint32_t output_count = static_cast<uint32_t>(vector_file.signal_outputs.size());
    const uint32_t vector_count = static_cast<uint32_t>(vector_file.vectors.size());
    out.write(reinterpret_cast<const char*>(&input_count), sizeof(input_count));
    out.write(reinterpret_cast<const char*>(&output_count), sizeof(output_count));
    out.write(reinterpret_cast<const char*>(&vector_count), sizeof(vector_count));
    for (const auto& signal : vector_file.signal_inputs) {
        WriteStringWithSize(out, signal);
    }
    for (const auto& signal : vector_file.signal_outputs) {
        WriteStringWithSize(out, signal);
    }

    for (const auto& vector : vector_file.vectors) {
        out.write(reinterpret_cast<const char*>(&vector.cycle), sizeof(vector.cycle));
        for (const auto& signal : vector_file.signal_inputs) {
            const auto it = vector.inputs.find(signal);
            WriteSignalValue(out, it == vector.inputs.end() ? SignalValue{0U} : it->second);
        }
        for (const auto& signal : vector_file.signal_outputs) {
            const auto it = vector.outputs.find(signal);
            WriteSignalValue(out, it == vector.outputs.end() ? SignalValue{0U} : it->second);
        }
    }
}

TestVectorFile ReadHexVectors(const std::string& path) {
    std::ifstream in(path);
    if (!in.is_open()) {
        throw std::runtime_error("Unable to open vector file: " + path);
    }

    TestVectorFile file;
    std::string line;
    while (std::getline(in, line)) {
        if (line.empty()) {
            continue;
        }
        if (line.rfind("@MODULE ", 0) == 0) {
            file.module_name = line.substr(8);
            continue;
        }
        if (line.rfind("@SPEC ", 0) == 0) {
            file.spec_name = line.substr(6);
            continue;
        }
        if (line.rfind("@CLOCK ", 0) == 0) {
            file.clock_name = line.substr(7);
            continue;
        }
        if (line.rfind("@SIGNALS_IN", 0) == 0) {
            std::stringstream signals(line.substr(11));
            std::string token;
            while (signals >> token) {
                file.signal_inputs.push_back(token);
            }
            continue;
        }
        if (line.rfind("@SIGNALS_OUT", 0) == 0) {
            std::stringstream signals(line.substr(12));
            std::string token;
            while (signals >> token) {
                file.signal_outputs.push_back(token);
            }
            continue;
        }
        if (line.front() == '#') {
            continue;
        }

        std::stringstream values(line);
        TestVector vector;
        std::string token;
        values >> token;
        vector.cycle = std::stoull(token, nullptr, 16);
        for (const auto& signal : file.signal_inputs) {
            values >> token;
            vector.inputs.emplace(signal, ParseSignalValue(token));
        }
        for (const auto& signal : file.signal_outputs) {
            values >> token;
            vector.outputs.emplace(signal, ParseSignalValue(token));
        }
        file.vectors.push_back(vector);
    }

    return file;
}

TestVectorFile ReadBinaryVectors(const std::string& path) {
    std::ifstream in(path, std::ios::binary);
    if (!in.is_open()) {
        throw std::runtime_error("Unable to open binary vector file: " + path);
    }

    uint32_t magic = 0U;
    in.read(reinterpret_cast<char*>(&magic), sizeof(magic));
    if (magic != 0x46505631U) {
        throw std::runtime_error("Invalid binary vector file magic: " + path);
    }

    TestVectorFile file;
    file.module_name = ReadStringWithSize(in);
    file.spec_name = ReadStringWithSize(in);
    file.clock_name = ReadStringWithSize(in);

    uint32_t input_count = 0U;
    uint32_t output_count = 0U;
    uint32_t vector_count = 0U;
    in.read(reinterpret_cast<char*>(&input_count), sizeof(input_count));
    in.read(reinterpret_cast<char*>(&output_count), sizeof(output_count));
    in.read(reinterpret_cast<char*>(&vector_count), sizeof(vector_count));

    for (uint32_t i = 0; i < input_count; ++i) {
        file.signal_inputs.push_back(ReadStringWithSize(in));
    }
    for (uint32_t i = 0; i < output_count; ++i) {
        file.signal_outputs.push_back(ReadStringWithSize(in));
    }

    for (uint32_t i = 0; i < vector_count; ++i) {
        TestVector vector;
        in.read(reinterpret_cast<char*>(&vector.cycle), sizeof(vector.cycle));
        for (const auto& signal : file.signal_inputs) {
            vector.inputs.emplace(signal, ReadSignalValue(in));
        }
        for (const auto& signal : file.signal_outputs) {
            vector.outputs.emplace(signal, ReadSignalValue(in));
        }
        file.vectors.push_back(vector);
    }

    return file;
}

}  // namespace fpd::sim
