#include <iostream>

#include "golden_models/core/SignalTypes.h"
#include "golden_models/models/ProtMonModel.h"
#include "tests/TestHelpers.h"

int main() {
    try {
        fpd::sim::ProtMonModel model;

        model.reset();
        model.set_inputs({
            {"fsm_state", 4U},
            {"xray_active", 1U},
            {"cfg_max_exposure", 0U},
            {"radiography_mode", 0U},
        });
        for (uint32_t i = 0; i < 499999U; ++i) {
            model.step();
        }
        Expect(fpd::sim::GetScalar(model.get_outputs(), "err_timeout") == 0U,
               "Static mode should not trip before the short timeout budget");
        model.step();
        Expect(fpd::sim::GetScalar(model.get_outputs(), "err_timeout") == 1U,
               "Static mode should trip at the short timeout budget");

        model.reset();
        model.set_inputs({
            {"fsm_state", 5U},
            {"xray_active", 1U},
            {"cfg_max_exposure", 0U},
            {"radiography_mode", 1U},
        });
        for (uint32_t i = 0; i < 500000U; ++i) {
            model.step();
        }
        Expect(fpd::sim::GetScalar(model.get_outputs(), "err_timeout") == 0U,
               "Radiography mode should not use the short timeout budget");

        model.reset();
        model.set_inputs({
            {"fsm_state", 4U},
            {"xray_active", 1U},
            {"cfg_max_exposure", 3U},
            {"radiography_mode", 1U},
        });
        for (uint32_t i = 0; i < 3U; ++i) {
            model.step();
        }
        Expect(fpd::sim::GetScalar(model.get_outputs(), "err_timeout") == 1U,
               "Configured exposure limits should clamp the mode timeout");

        std::cout << "test_prot_mon passed\n";
    } catch (const std::exception& e) {
        std::cerr << "FAIL: " << e.what() << "\n";
        return 1;
    }
    return 0;
}
