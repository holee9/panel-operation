#include <iostream>

#include "golden_models/models/DataOutMuxModel.h"
#include "golden_models/models/LineBufModel.h"
#include "golden_models/models/McuDataIfModel.h"
#include "tests/TestHelpers.h"

int main() {
    try {
        fpd::sim::LineBufModel line_buf;
        line_buf.reset();
        line_buf.set_inputs({{"cfg_ncols", 4U}, {"cfg_afe_count", 1U}, {"wr_addr", 0U}, {"wr_data", 0x1111U}, {"wr_en", 1U}});
        line_buf.step();
        Expect(fpd::sim::GetScalar(line_buf.get_outputs(), "fifo_level") <= 1U, "line buffer should track fifo level");
        line_buf.set_inputs({
            {"cfg_ncols", 3072U},
            {"cfg_afe_count", 12U},
            {"wr_addr", 1U},
            {"wr_samples", std::vector<uint16_t>{0xAAAAU, 0xBBBBU, 0xCCCCU}},
            {"wr_en", 1U}
        });
        line_buf.step();
        Expect(fpd::sim::GetScalar(line_buf.get_outputs(), "active_streams") == 12U,
               "line buffer should expose multi-AFE stream count");
        line_buf.set_inputs({{"cfg_ncols", 3072U}, {"wr_en", 0U}, {"rd_addr", 1U}, {"rd_en", 1U}});
        line_buf.step();
        Expect(fpd::sim::GetScalar(line_buf.get_outputs(), "rd_data") == 0xAAAAU,
               "line buffer should preserve the first multi-sample write at the requested address");
        line_buf.set_inputs({{"cfg_ncols", 3072U}, {"wr_en", 0U}, {"rd_addr", 2U}, {"rd_en", 1U}});
        line_buf.step();
        Expect(fpd::sim::GetScalar(line_buf.get_outputs(), "rd_data") == 0xBBBBU,
               "line buffer should write subsequent samples to consecutive addresses");

        fpd::sim::DataOutMuxModel mux;
        mux.reset();
        mux.set_inputs({{"cfg_ncols", 4U}, {"line_pixel_data", 0x55AAU}, {"line_data_valid", 1U}, {"line_pixel_idx", 0U}});
        mux.step();
        Expect(fpd::sim::GetScalar(mux.get_outputs(), "mcu_line_start") == 1U, "first pixel should assert line start");

        fpd::sim::McuDataIfModel mcu;
        mcu.reset();
        mcu.set_inputs({{"pixel_data", 0x1234U}, {"data_valid", 1U}, {"line_end", 1U}});
        mcu.step();
        Expect(fpd::sim::GetScalar(mcu.get_outputs(), "irq_line_ready") == 1U, "line end should raise line IRQ");

        std::cout << "test_data_path_models passed\n";
    } catch (const std::exception& e) {
        std::cerr << "FAIL: " << e.what() << "\n";
        return 1;
    }
    return 0;
}
