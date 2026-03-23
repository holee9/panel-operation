#include "golden_models/models/Csi2LaneDistModel.h"
#include "golden_models/models/Csi2PacketModel.h"
#include "golden_models/models/DataOutMuxModel.h"
#include "golden_models/models/LineBufModel.h"
#include "golden_models/models/McuDataIfModel.h"

int main() {
    fpd::sim::LineBufModel line_buf;
    fpd::sim::DataOutMuxModel mux;
    fpd::sim::McuDataIfModel mcu;
    fpd::sim::Csi2PacketModel csi2;
    fpd::sim::Csi2LaneDistModel lanes;
    line_buf.generate_vectors("sim/testvectors/spec007");
    mux.generate_vectors("sim/testvectors/spec007");
    mcu.generate_vectors("sim/testvectors/spec007");
    csi2.generate_vectors("sim/testvectors/spec007");
    lanes.generate_vectors("sim/testvectors/spec007");
    return 0;
}
