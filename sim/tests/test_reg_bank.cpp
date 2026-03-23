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
    model.Write(fpd::sim::kRegCombo, fpd::sim::kComboC6);
    Expect(model.Read(fpd::sim::kRegNCols) == 3072U,
           "Combo write should clamp NCOLS to the combo default");
    Expect(model.Read(fpd::sim::kRegTLine) == 2200U,
           "Combo C6 should preserve the default minimum TLINE");
    model.Write(fpd::sim::kRegNCols, 4095U);
    Expect(model.Read(fpd::sim::kRegNCols) == 3072U,
           "NCOLS should clamp to the combo-specific maximum");
    model.Write(fpd::sim::kRegCombo, fpd::sim::kComboC2);
    model.Write(fpd::sim::kRegTLine, 1024U);
    Expect(model.Read(fpd::sim::kRegTLine) == 6000U,
           "TLINE should clamp to the combo-specific minimum");
    model.Write(fpd::sim::kRegTIntegHi, 0x01FFU);
    Expect(model.Read(fpd::sim::kRegTIntegHi) == 0x00FFU,
           "TINTEG high register should store only the low 8 bits");

    // CR-005: TLINE_CLAMPED error flag
    fpd::sim::RegBankModel clamp_model;
    clamp_model.Write(fpd::sim::kRegCombo, fpd::sim::kComboC1);
    clamp_model.clear_tline_clamped();
    clamp_model.Write(fpd::sim::kRegTLine, 5000U);
    Expect(!clamp_model.tline_clamped(),
           "TLINE above minimum should not set clamped flag");
    clamp_model.Write(fpd::sim::kRegCombo, fpd::sim::kComboC2);
    Expect(clamp_model.tline_clamped(),
           "Switching to C2 with TLINE=5000 < 6000 should set clamped flag");
    Expect(clamp_model.Read(fpd::sim::kRegTLine) == 6000U,
           "TLINE should be clamped to C2 minimum after combo switch");

    fpd::sim::RegBankModel clamp_model2;
    clamp_model2.Write(fpd::sim::kRegCombo, fpd::sim::kComboC3);
    clamp_model2.clear_tline_clamped();
    clamp_model2.Write(fpd::sim::kRegTLine, 1000U);
    Expect(clamp_model2.tline_clamped(),
           "Writing TLINE below C3 minimum should set clamped flag");
    Expect(clamp_model2.Read(fpd::sim::kRegTLine) == 5120U,
           "TLINE should be clamped to C3 minimum 5120");

    std::cout << "test_reg_bank passed\n";
    return 0;
}
