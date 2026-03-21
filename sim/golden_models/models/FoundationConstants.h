#pragma once

#include <array>
#include <cstdint>

namespace fpd::sim {

constexpr uint8_t kRegCtrl = 0x00;
constexpr uint8_t kRegStatus = 0x01;
constexpr uint8_t kRegMode = 0x02;
constexpr uint8_t kRegCombo = 0x03;
constexpr uint8_t kRegNRows = 0x04;
constexpr uint8_t kRegNCols = 0x05;
constexpr uint8_t kRegTLine = 0x06;
constexpr uint8_t kRegTReset = 0x07;
constexpr uint8_t kRegTInteg = 0x08;
constexpr uint8_t kRegTGateOn = 0x09;
constexpr uint8_t kRegTGateSettle = 0x0A;
constexpr uint8_t kRegAfeIfs = 0x0B;
constexpr uint8_t kRegAfeLpf = 0x0C;
constexpr uint8_t kRegAfePMode = 0x0D;
constexpr uint8_t kRegCicEn = 0x0E;
constexpr uint8_t kRegCicProfile = 0x0F;
constexpr uint8_t kRegScanDir = 0x10;
constexpr uint8_t kRegGateSel = 0x11;
constexpr uint8_t kRegAfeNChip = 0x12;
constexpr uint8_t kRegSyncDly = 0x13;
constexpr uint8_t kRegLineIdx = 0x14;
constexpr uint8_t kRegErrCode = 0x15;
constexpr uint8_t kRegNReset = 0x16;
constexpr uint8_t kRegVersion = 0x1F;

constexpr uint16_t kVersion10 = 0x0010;

inline std::array<uint16_t, 32> MakeDefaultRegisters() {
    std::array<uint16_t, 32> regs{};
    regs[kRegMode] = 0x0000;
    regs[kRegCombo] = 0x0001;
    regs[kRegNRows] = 2048U;
    regs[kRegNCols] = 2048U;
    regs[kRegTLine] = 2200U;
    regs[kRegTReset] = 100U;
    regs[kRegTInteg] = 1000U;
    regs[kRegTGateOn] = 2200U;
    regs[kRegTGateSettle] = 100U;
    regs[kRegAfeIfs] = 0x0000;
    regs[kRegAfeLpf] = 0x0000;
    regs[kRegAfePMode] = 0x0000;
    regs[kRegCicEn] = 0x0000;
    regs[kRegCicProfile] = 0x0000;
    regs[kRegScanDir] = 0x0000;
    regs[kRegGateSel] = 0x0000;
    regs[kRegAfeNChip] = 0x0001;
    regs[kRegSyncDly] = 0x0000;
    regs[kRegNReset] = 0x0003;
    regs[kRegVersion] = kVersion10;
    return regs;
}

inline bool IsReadOnlyRegister(uint8_t addr) {
    return addr == kRegStatus || addr == kRegLineIdx ||
           addr == kRegErrCode || addr == kRegVersion;
}

}  // namespace fpd::sim
