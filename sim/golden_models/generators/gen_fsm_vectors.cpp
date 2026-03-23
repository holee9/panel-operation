#include "golden_models/models/PanelFsmModel.h"
#include "golden_models/models/PanelIntegModel.h"
#include "golden_models/models/PanelResetModel.h"

int main() {
    fpd::sim::PanelFsmModel fsm;
    fpd::sim::PanelResetModel reset;
    fpd::sim::PanelIntegModel integ;
    fsm.generate_vectors("sim/testvectors/spec002");
    reset.generate_vectors("sim/testvectors/spec002");
    integ.generate_vectors("sim/testvectors/spec002");
    return 0;
}
