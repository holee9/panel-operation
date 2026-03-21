#include <iostream>

#include "golden_models/models/RadiogModel.h"
#include "tests/TestHelpers.h"

int main() {
    try {
        fpd::sim::RadiogModel model;
        model.reset();
        model.set_inputs({{"start", 1U}, {"xray_ready", 1U}, {"xray_on", 1U}, {"xray_off", 1U}, {"cfg_tsettle", 1U}});
        for (int i = 0; i < 4; ++i) {
            model.step();
        }
        Expect(fpd::sim::GetScalar(model.get_outputs(), "done") == 1U, "radiography flow should reach done");

        model.reset();
        model.set_inputs({
            {"start", 1U},
            {"dark_frame_mode", 1U},
            {"frame_valid", 1U},
            {"cfg_dark_cnt", 2U},
            {"frame_pixels", std::vector<uint16_t>{10U, 20U, 30U, 40U}}
        });
        model.step();
        model.set_inputs({
            {"dark_frame_mode", 1U},
            {"frame_valid", 1U},
            {"cfg_dark_cnt", 2U},
            {"frame_pixels", std::vector<uint16_t>{14U, 24U, 34U, 44U}}
        });
        model.step();
        Expect(fpd::sim::GetScalar(model.get_outputs(), "dark_avg_ready") == 1U, "dark frame averaging should complete");
        const auto dark_avg = fpd::sim::GetVector(model.get_outputs(), "dark_avg_frame");
        Expect(dark_avg.size() == 4U && dark_avg[0] == 12U && dark_avg[3] == 42U,
               "dark frame average should be computed per pixel");

        std::cout << "test_radiog_model passed\n";
    } catch (const std::exception& e) {
        std::cerr << "FAIL: " << e.what() << "\n";
        return 1;
    }
    return 0;
}
