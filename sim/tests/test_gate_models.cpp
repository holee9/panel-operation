#include <iostream>

#include "golden_models/models/GateNt39565dModel.h"
#include "golden_models/models/GateNv1047Model.h"
#include "tests/TestHelpers.h"

int main() {
    try {
        fpd::sim::GateNv1047Model nv;
        nv.reset();
        nv.set_inputs({{"row_index", 3U}, {"gate_on_pulse", 1U}, {"cfg_clk_period", 2U}, {"cfg_gate_settle", 2U}});
        nv.step();
        auto nv_out = nv.get_outputs();
        Expect(fpd::sim::GetScalar(nv_out, "nv_oe") == 0U, "NV gate should drive OE active during pulse");
        nv.set_inputs({{"row_index", 3U}, {"gate_on_pulse", 0U}, {"cfg_clk_period", 2U}, {"cfg_gate_settle", 2U}});
        nv.step();
        nv_out = nv.get_outputs();
        Expect(fpd::sim::GetScalar(nv_out, "nv_oe") == 1U, "NV gate should keep OE inactive during the BBM gap");
        nv.step();
        nv.step();
        nv_out = nv.get_outputs();
        Expect(fpd::sim::GetScalar(nv_out, "row_done") == 1U, "NV gate should report row done after the BBM gap");

        fpd::sim::GateNt39565dModel nt;
        nt.reset();
        nt.set_inputs({{"row_index", 0U}, {"gate_on_pulse", 1U}, {"chip_sel", 2U}, {"mode_sel", 1U}, {"cascade_stv_return", 1U}});
        nt.step();
        auto nt_out = nt.get_outputs();
        Expect(fpd::sim::GetScalar(nt_out, "cascade_complete") == 1U, "NT cascade should complete when return asserted");
        Expect(fpd::sim::GetScalar(nt_out, "stv1l") == 1U, "NT gate should assert left STV on even rows");
        Expect(fpd::sim::GetScalar(nt_out, "stv1r") == 1U, "NT gate should assert right STV when both banks are active");

        std::cout << "test_gate_models passed\n";
    } catch (const std::exception& e) {
        std::cerr << "FAIL: " << e.what() << "\n";
        return 1;
    }
    return 0;
}
