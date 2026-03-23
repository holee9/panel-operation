#include "golden_models/models/GateNt39565dModel.h"
#include "golden_models/models/GateNv1047Model.h"
#include "golden_models/models/RowScanModel.h"

int main() {
    fpd::sim::GateNv1047Model nv;
    fpd::sim::GateNt39565dModel nt;
    fpd::sim::RowScanModel row;
    nv.generate_vectors("sim/testvectors/spec003");
    nt.generate_vectors("sim/testvectors/spec004");
    row.generate_vectors("sim/testvectors/spec003");
    return 0;
}
