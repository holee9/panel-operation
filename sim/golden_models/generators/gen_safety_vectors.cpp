#include "golden_models/models/EmergencyShutdownModel.h"
#include "golden_models/models/PowerSeqModel.h"
#include "golden_models/models/ProtMonModel.h"
#include "golden_models/models/RadiogModel.h"

int main() {
    fpd::sim::ProtMonModel prot;
    fpd::sim::PowerSeqModel power;
    fpd::sim::EmergencyShutdownModel shutdown;
    fpd::sim::RadiogModel radiog;
    prot.generate_vectors("sim/testvectors/spec008");
    power.generate_vectors("sim/testvectors/spec008");
    shutdown.generate_vectors("sim/testvectors/spec008");
    radiog.generate_vectors("sim/testvectors/spec010");
    return 0;
}
