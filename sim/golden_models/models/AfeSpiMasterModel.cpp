#include "golden_models/models/AfeSpiMasterModel.h"

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

void AfeSpiMasterModel::reset() {
    start_ = 0;
    cfg_word_ = 0xA55A;
    cfg_nchip_ = 1;
    spi_sck_ = 0;
    spi_sdi_ = 0;
    spi_cs_n_ = 1;
    done_ = 0;
    bit_index_ = 0;
    cycle_count_ = 0;
}

void AfeSpiMasterModel::step() {
    done_ = 0;
    if (start_ != 0U) {
        spi_cs_n_ = 0U;
        spi_sck_ ^= 1U;
        spi_sdi_ = (cfg_word_ >> (15U - (bit_index_ & 0xFU))) & 0x1U;
        ++bit_index_;
        if (bit_index_ >= 16U * cfg_nchip_) {
            bit_index_ = 0U;
            done_ = 1U;
            spi_cs_n_ = 1U;
            spi_sck_ = 0U;
        }
    } else {
        spi_cs_n_ = 1U;
        spi_sck_ = 0U;
        bit_index_ = 0U;
    }
    ++cycle_count_;
}

void AfeSpiMasterModel::set_inputs(const SignalMap& inputs) {
    start_ = GetScalar(inputs, "start", start_);
    cfg_word_ = GetScalar(inputs, "cfg_word", cfg_word_);
    cfg_nchip_ = GetScalar(inputs, "cfg_nchip", cfg_nchip_);
}

SignalMap AfeSpiMasterModel::get_outputs() const {
    return {
        {"spi_sck", spi_sck_},
        {"spi_sdi", spi_sdi_},
        {"spi_cs_n", spi_cs_n_},
        {"done", done_},
        {"bit_index", bit_index_},
    };
}

std::vector<Mismatch> AfeSpiMasterModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void AfeSpiMasterModel::generate_vectors(const std::string& output_dir) {
    TestVectorFile vectors;
    vectors.module_name = "afe_spi_master";
    vectors.spec_name = "SPEC-FPD-005";
    vectors.clock_name = "sys_clk";
    vectors.signal_inputs = {"start", "cfg_word", "cfg_nchip"};
    vectors.signal_outputs = {"spi_sck", "spi_sdi", "spi_cs_n", "done", "bit_index"};
    reset();
    set_inputs({{"start", 1U}, {"cfg_word", 0xA55AU}, {"cfg_nchip", 2U}});
    for (int i = 0; i < 4; ++i) {
        step();
        vectors.vectors.push_back({cycle(), {{"start", 1U}, {"cfg_word", 0xA55AU}, {"cfg_nchip", 2U}}, get_outputs()});
    }
    WriteHexVectors(vectors, output_dir + "/afe_spi_master_vectors.hex");
    WriteBinaryVectors(vectors, output_dir + "/afe_spi_master_vectors.bin");
}

}  // namespace fpd::sim
