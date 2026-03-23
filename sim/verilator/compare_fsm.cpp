#include "golden_models/models/PanelFsmModel.h"
#include "verilator/golden_compare.h"

int main() {
    fpd::sim::PanelFsmModel model;
    return fpd::sim::RunGoldenCompare(
        model,
        8U,
        [](uint64_t cycle, fpd::sim::GoldenModelBase& base) {
            auto& fsm = static_cast<fpd::sim::PanelFsmModel&>(base);
            fsm.set_inputs({
                {"ctrl_start", cycle == 0U ? 1U : 0U},
                {"cfg_mode", 0U},
                {"cfg_combo", 1U},
                {"cfg_nrows", 2U},
                {"cfg_treset", 1U},
                {"cfg_tinteg", 1U},
                {"cfg_nreset", 1U},
                {"cfg_sync_dly", 1U},
                {"cfg_tgate_settle", 1U},
                {"afe_config_done", cycle >= 3U ? 1U : 0U},
                {"gate_row_done", cycle >= 4U ? 1U : 0U},
                {"afe_line_valid", cycle >= 4U ? 1U : 0U}
            });
        },
        [](fpd::sim::SignalMap& rtl_outputs) {
            rtl_outputs.clear();
            return false;
        },
        "FSM compare requires real RTL binding");
}
