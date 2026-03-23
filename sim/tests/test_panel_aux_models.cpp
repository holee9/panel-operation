#include <iostream>

#include "golden_models/models/PanelFsmModel.h"
#include "golden_models/models/PanelIntegModel.h"
#include "golden_models/models/PanelResetModel.h"
#include "golden_models/models/ProtMonModel.h"
#include "tests/TestHelpers.h"

int main() {
    fpd::sim::PanelResetModel reset_model;
    reset_model.reset();
    reset_model.set_inputs({{"start", 1U}, {"cfg_nreset", 1U}, {"cfg_nrows", 1U}});
    reset_model.step();
    Expect(fpd::sim::GetScalar(reset_model.get_outputs(), "reset_done") == 1U, "single-row reset should complete immediately");

    fpd::sim::PanelIntegModel integ_model;
    integ_model.reset();
    integ_model.set_inputs({{"start", 1U}, {"cfg_tinteg", 1U}});
    integ_model.step();
    Expect(fpd::sim::GetScalar(integ_model.get_outputs(), "exposure_done") == 1U, "static integration should finish after configured length");

    fpd::sim::PanelFsmModel prep_timeout_model;
    prep_timeout_model.reset();
    prep_timeout_model.set_inputs({
        {"ctrl_start", 1U},
        {"cfg_mode", 2U},
        {"cfg_nrows", 2U},
        {"cfg_treset", 1U},
        {"cfg_tinteg", 1U},
        {"cfg_nreset", 1U},
        {"radiography_mode", 0U}
    });
    for (int i = 0; i < 10; ++i) {
        prep_timeout_model.step();
        prep_timeout_model.set_inputs({
            {"ctrl_start", 0U},
            {"cfg_mode", 2U},
            {"cfg_nrows", 2U},
            {"cfg_treset", 1U},
            {"cfg_tinteg", 1U},
            {"cfg_nreset", 1U},
            {"radiography_mode", 0U}
        });
    }
    Expect(fpd::sim::GetScalar(prep_timeout_model.get_outputs(), "sts_error") == 1U,
           "triggered mode should trip the default ready timeout");

    fpd::sim::PanelFsmModel radiog_timeout_model;
    radiog_timeout_model.reset();
    radiog_timeout_model.set_inputs({
        {"ctrl_start", 1U},
        {"cfg_mode", 2U},
        {"cfg_nrows", 2U},
        {"cfg_treset", 1U},
        {"cfg_tinteg", 1U},
        {"cfg_nreset", 1U},
        {"radiography_mode", 1U}
    });
    for (int i = 0; i < 10; ++i) {
        radiog_timeout_model.step();
        radiog_timeout_model.set_inputs({
            {"ctrl_start", 0U},
            {"cfg_mode", 2U},
            {"cfg_nrows", 2U},
            {"cfg_treset", 1U},
            {"cfg_tinteg", 1U},
            {"cfg_nreset", 1U},
            {"radiography_mode", 1U}
        });
    }
    Expect(fpd::sim::GetScalar(radiog_timeout_model.get_outputs(), "sts_error") == 0U,
           "radiography mode should not use the shorter 5-cycle timeout");
    for (int i = 0; i < 25; ++i) {
        radiog_timeout_model.step();
    }
    Expect(fpd::sim::GetScalar(radiog_timeout_model.get_outputs(), "sts_error") == 1U,
           "radiography mode should eventually trip the extended timeout");
    Expect(fpd::sim::GetScalar(radiog_timeout_model.get_outputs(), "sts_err_code") == 2U,
           "radiography timeout should report ERR_XRAY_TIMEOUT");

    fpd::sim::ProtMonModel prot_model;
    prot_model.reset();
    prot_model.set_inputs({{"fsm_state", 4U}, {"xray_active", 1U}, {"cfg_max_exposure", 2U}});
    prot_model.step();
    Expect(fpd::sim::GetScalar(prot_model.get_outputs(), "err_timeout") == 0U,
           "protection monitor should wait until the configured exposure budget expires");
    prot_model.step();
    Expect(fpd::sim::GetScalar(prot_model.get_outputs(), "force_gate_off") == 1U,
           "protection monitor should force gate off after the exposure timeout");
    prot_model.set_inputs({{"fsm_state", 0U}, {"xray_active", 0U}, {"cfg_max_exposure", 2U}});
    prot_model.step();
    Expect(fpd::sim::GetScalar(prot_model.get_outputs(), "err_timeout") == 0U,
           "protection monitor should clear timeout state when the FSM returns to idle");

    // CR-002: Dual timeout — normal mode uses short limit, radiography uses extended
    fpd::sim::ProtMonModel prot_dual;
    prot_dual.reset();
    // Normal mode (radiography_mode=0): cfg_max_exposure=0 → uses kDefaultTimeout (500000)
    prot_dual.set_inputs({{"fsm_state", 5U}, {"xray_active", 1U},
                          {"cfg_max_exposure", 0U}, {"radiography_mode", 0U}});
    for (int i = 0; i < 5; ++i) prot_dual.step();
    Expect(fpd::sim::GetScalar(prot_dual.get_outputs(), "err_timeout") == 0U,
           "normal mode with default timeout should not trip in 5 cycles");

    fpd::sim::ProtMonModel prot_radiog;
    prot_radiog.reset();
    // Radiography mode (radiography_mode=1): cfg_max_exposure=0 → uses kRadiogTimeout (3000000)
    prot_radiog.set_inputs({{"fsm_state", 5U}, {"xray_active", 1U},
                            {"cfg_max_exposure", 0U}, {"radiography_mode", 1U}});
    for (int i = 0; i < 5; ++i) prot_radiog.step();
    Expect(fpd::sim::GetScalar(prot_radiog.get_outputs(), "err_timeout") == 0U,
           "radiography mode with extended timeout should not trip in 5 cycles");

    // Verify explicit cfg_max_exposure overrides mode-based defaults
    fpd::sim::ProtMonModel prot_explicit;
    prot_explicit.reset();
    prot_explicit.set_inputs({{"fsm_state", 4U}, {"xray_active", 1U},
                              {"cfg_max_exposure", 3U}, {"radiography_mode", 1U}});
    for (int i = 0; i < 2; ++i) prot_explicit.step();
    Expect(fpd::sim::GetScalar(prot_explicit.get_outputs(), "err_timeout") == 0U,
           "explicit exposure limit should apply even in radiography mode (not yet reached)");
    prot_explicit.step();
    Expect(fpd::sim::GetScalar(prot_explicit.get_outputs(), "force_gate_off") == 1U,
           "explicit exposure limit should trigger at configured count regardless of mode");

    std::cout << "test_panel_aux_models passed\n";
    return 0;
}
