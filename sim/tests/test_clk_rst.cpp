#include <iostream>

#include "golden_models/models/ClkRstModel.h"
#include "tests/TestHelpers.h"

int main() {
    fpd::sim::ClkRstModel model(10000000ULL);

    model.set_inputs({{"rst_ext_n", 0U}, {"afe_type_sel", 0U}});
    model.step();
    Expect(fpd::sim::GetScalar(model.get_outputs(), "pll_locked") == 0U,
           "PLL should not be locked during external reset");

    for (int i = 0; i < 20; ++i) {
        model.set_inputs({{"rst_ext_n", 1U}, {"afe_type_sel", 0U}});
        model.step();
    }

    const auto outputs = model.get_outputs();
    Expect(fpd::sim::GetScalar(outputs, "pll_locked") == 1U,
           "PLL should lock after reset release");
    Expect(fpd::sim::GetScalar(outputs, "rst_sync_n") == 1U,
           "Synchronized reset should deassert after lock");

    std::cout << "test_clk_rst passed\n";
    return 0;
}
