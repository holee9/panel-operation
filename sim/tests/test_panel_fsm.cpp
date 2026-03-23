#include <iostream>

#include "golden_models/models/PanelFsmModel.h"
#include "tests/TestHelpers.h"

int main() {
    try {
        fpd::sim::PanelFsmModel model;
        model.reset();
        model.set_inputs({
            {"ctrl_start", 1U},
            {"cfg_mode", 0U},
            {"cfg_nrows", 2U},
            {"cfg_treset", 1U},
            {"cfg_tinteg", 1U},
            {"cfg_nreset", 1U},
            {"cfg_sync_dly", 1U},
            {"cfg_tgate_settle", 1U},
            {"afe_config_done", 1U},
            {"gate_row_done", 1U},
            {"afe_line_valid", 1U},
        });

        bool seen_done = false;
        bool seen_settle = false;
        for (int i = 0; i < 20; ++i) {
            model.step();
            if (fpd::sim::GetScalar(model.get_outputs(), "fsm_state") == 8U) {
                seen_settle = true;
            }
            if (fpd::sim::GetScalar(model.get_outputs(), "sts_done") == 1U) {
                seen_done = true;
                break;
            }
        }

        Expect(seen_settle, "Panel FSM should visit the settle state before done");
        Expect(seen_done, "Panel FSM should assert done within 20 cycles");

        std::cout << "test_panel_fsm passed\n";
    } catch (const std::exception& e) {
        std::cerr << "FAIL: " << e.what() << "\n";
        return 1;
    }
    return 0;
}
