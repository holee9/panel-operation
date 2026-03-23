#include <iostream>

#include "golden_models/models/AfeAd711xxModel.h"
#include "golden_models/models/AfeAfe2256Model.h"
#include "golden_models/models/AfeSpiMasterModel.h"
#include "golden_models/models/LvdsRxModel.h"
#include "tests/TestHelpers.h"

int main() {
    fpd::sim::AfeAd711xxModel ad;
    ad.reset();
    ad.set_inputs({{"config_req", 1U}, {"cfg_combo", 1U}, {"cfg_tline", 2200U}, {"afe_type", 0U}});
    ad.step();
    Expect(fpd::sim::GetScalar(ad.get_outputs(), "config_done") == 1U, "ADI config should complete on first request");
    ad.set_inputs({
        {"afe_start", 1U}, {"cfg_combo", 6U}, {"cfg_tline", 6000U},
        {"cfg_ifs", 37U}, {"cfg_nchip", 12U}, {"afe_type", 1U}
    });
    ad.step();
    Expect(fpd::sim::GetScalar(ad.get_outputs(), "expected_ncols") == 3072U,
           "combo C6 should default to 3072 columns");
    Expect(fpd::sim::GetScalar(ad.get_outputs(), "ifs_width_error") == 1U,
           "AD71143 should flag 5-bit IFS overflow");

    fpd::sim::AfeAfe2256Model ti;
    ti.reset();
    ti.set_inputs({{"config_req", 1U}, {"cfg_tline", 5120U}, {"cfg_pipeline_en", 1U}});
    ti.step();
    Expect(fpd::sim::GetScalar(ti.get_outputs(), "afe_ready") == 1U, "AFE2256 should report ready after config");

    fpd::sim::AfeSpiMasterModel spi;
    spi.reset();
    spi.set_inputs({{"start", 1U}, {"cfg_word", 0x8001U}, {"cfg_nchip", 1U}});
    spi.step();
    Expect(fpd::sim::GetScalar(spi.get_outputs(), "spi_cs_n") == 0U, "SPI master should assert CS during transfer");

    fpd::sim::LvdsRxModel lvds;
    lvds.reset();
    lvds.set_inputs({{"rx_enable", 1U}, {"lvds_dout_a", 1U}, {"lvds_dout_b", 1U}});
    for (int i = 0; i < 8; ++i) {
        lvds.step();
    }
    Expect(fpd::sim::GetScalar(lvds.get_outputs(), "pixel_valid") == 1U, "LVDS receiver should emit one pixel per 8 samples");

    std::cout << "test_afe_models passed\n";
    return 0;
}
