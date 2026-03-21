#include <iostream>

#include "golden_models/models/PanelFsmModel.h"
#include "tests/TestHelpers.h"

int main() {
    fpd::sim::PanelFsmModel model;
    model.reset();
    model.set_inputs({
        {"ctrl_start", 1U},
        {"cfg_mode", 0U},
        {"cfg_nrows", 2U},
        {"afe_config_done", 1U},
        {"gate_row_done", 1U},
        {"afe_line_valid", 1U},
    });

    for (int i = 0; i < 12; ++i) {
        model.step();
    }

    const auto outputs = model.get_outputs();
    Expect(fpd::sim::GetScalar(outputs, "fsm_state") == 0U ||
               fpd::sim::GetScalar(outputs, "sts_done") == 1U,
           "Panel FSM should eventually return to idle or assert done");

    std::cout << "test_panel_fsm passed\n";
    return 0;
}
