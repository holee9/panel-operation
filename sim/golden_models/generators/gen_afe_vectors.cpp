#include "golden_models/models/AfeAd711xxModel.h"
#include "golden_models/models/AfeAfe2256Model.h"
#include "golden_models/models/AfeSpiMasterModel.h"
#include "golden_models/models/LvdsRxModel.h"

int main() {
    fpd::sim::AfeAd711xxModel ad;
    fpd::sim::AfeAfe2256Model ti;
    fpd::sim::AfeSpiMasterModel spi;
    fpd::sim::LvdsRxModel lvds;
    ad.generate_vectors("sim/testvectors/spec005");
    ti.generate_vectors("sim/testvectors/spec006");
    spi.generate_vectors("sim/testvectors/spec005");
    lvds.generate_vectors("sim/testvectors/spec007");
    return 0;
}
