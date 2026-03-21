#pragma once

#include <cstdint>
#include <string>
#include <vector>

#include "golden_models/core/GoldenModelBase.h"
#include "golden_models/models/RegBankModel.h"

namespace fpd::sim {

class SpiSlaveModel : public GoldenModelBase {
public:
    SpiSlaveModel();

    void reset() override;
    void step() override;
    void set_inputs(const SignalMap& inputs) override;
    SignalMap get_outputs() const override;
    std::vector<Mismatch> compare(const SignalMap& rtl_outputs) const override;
    void generate_vectors(const std::string& output_dir) override;

    void WriteRegister(uint8_t addr, uint16_t value);
    uint16_t ReadRegister(uint8_t addr);

private:
    void TransferBit(bool cs_n, bool sclk, bool mosi);

    RegBankModel reg_bank_;
    bool cs_n_ = true;
    bool sclk_ = false;
    bool mosi_ = false;
    bool miso_ = false;
    uint8_t spi_mode_ = 0;
    bool prev_sclk_ = false;
    uint8_t bit_count_ = 0;
    uint8_t current_addr_ = 0;
    uint16_t write_shift_ = 0;
    uint16_t read_shift_ = 0;
    bool is_read_ = false;
    bool reg_wr_en_ = false;
    bool reg_rd_en_ = false;
};

}  // namespace fpd::sim
