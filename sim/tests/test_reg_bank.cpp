#include <iostream>

#include "golden_models/models/FoundationConstants.h"
#include "golden_models/models/RegBankModel.h"
#include "tests/TestHelpers.h"

int main() {
    fpd::sim::RegBankModel model;

    Expect(model.Read(fpd::sim::kRegVersion) == fpd::sim::kVersion10,
           "Version register default should be 0x0010");

    model.Write(fpd::sim::kRegMode, 0x0004);
    Expect(model.Read(fpd::sim::kRegMode) == 0x0004,
           "Writable register should store written value");

    model.Write(fpd::sim::kRegVersion, 0xABCD);
    Expect(model.Read(fpd::sim::kRegVersion) == fpd::sim::kVersion10,
           "Read-only version register should ignore writes");

    model.SetStatus(true, true, false, true, 0x0123, 0x55);
    Expect(model.Read(fpd::sim::kRegStatus) == 0x000B,
           "Status register should reflect live inputs");
    Expect(model.Read(fpd::sim::kRegLineIdx) == 0x0123,
           "Line index should reflect live status input");
    Expect(model.Read(fpd::sim::kRegErrCode) == 0x0055,
           "Error code should reflect live status input");
    Expect(model.Read(fpd::sim::kRegNReset) == 0x0003,
           "NRESET register default should be 3 dummy scans");
    model.Write(fpd::sim::kRegAfeLpf, 0x0007);
    Expect(model.Read(fpd::sim::kRegAfeLpf) == 0x0007,
           "LPF register should be writable");

    std::cout << "test_reg_bank passed\n";
    return 0;
}
