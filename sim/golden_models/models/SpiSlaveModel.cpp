#include "golden_models/models/SpiSlaveModel.h"

#include <filesystem>

#include "golden_models/core/TestVectorIO.h"

namespace fpd::sim {

SpiSlaveModel::SpiSlaveModel() {
    reset();
}

void SpiSlaveModel::reset() {
    reg_bank_.reset();
    cs_n_ = true;
    sclk_ = false;
    mosi_ = false;
    miso_ = false;
    spi_mode_ = 0;
    prev_sclk_ = false;
    bit_count_ = 0;
    current_addr_ = 0;
    write_shift_ = 0;
    read_shift_ = 0;
    is_read_ = false;
    reg_wr_en_ = false;
    reg_rd_en_ = false;
    cycle_count_ = 0;
}

void SpiSlaveModel::step() {
    TransferBit(cs_n_, sclk_, mosi_);
    ++cycle_count_;
}

void SpiSlaveModel::set_inputs(const SignalMap& inputs) {
    cs_n_ = (GetScalar(inputs, "spi_cs_n", cs_n_ ? 1U : 0U) & 0x1U) != 0U;
    sclk_ = (GetScalar(inputs, "spi_sclk", sclk_ ? 1U : 0U) & 0x1U) != 0U;
    mosi_ = (GetScalar(inputs, "spi_mosi", mosi_ ? 1U : 0U) & 0x1U) != 0U;
    spi_mode_ = static_cast<uint8_t>(GetScalar(inputs, "spi_mode", spi_mode_) & 0x3U);
}

SignalMap SpiSlaveModel::get_outputs() const {
    return {
        {"spi_miso", miso_ ? 1U : 0U},
        {"reg_addr", current_addr_ & 0x1FU},
        {"reg_wr_en", reg_wr_en_ ? 1U : 0U},
        {"reg_rd_en", reg_rd_en_ ? 1U : 0U},
    };
}

std::vector<Mismatch> SpiSlaveModel::compare(const SignalMap& rtl_outputs) const {
    return CompareSignalMaps(cycle(), get_outputs(), rtl_outputs);
}

void SpiSlaveModel::generate_vectors(const std::string& output_dir) {
    std::filesystem::create_directories(output_dir);

    TestVectorFile vector_file;
    vector_file.module_name = "spi_slave_if";
    vector_file.spec_name = "SPEC-FPD-001";
    vector_file.signal_inputs = {"spi_cs_n", "spi_sclk", "spi_mosi"};
    vector_file.signal_outputs = {"spi_miso", "reg_addr", "reg_wr_en", "reg_rd_en"};

    reset();

    const struct Sample {
        bool cs_n;
        bool sclk;
        bool mosi;
    } samples[] = {
        {true, false, false},
        {false, false, true},
        {false, true, true},
        {false, false, false},
        {true, false, false},
    };

    for (const auto& sample : samples) {
        set_inputs({
            {"spi_cs_n", sample.cs_n ? 1U : 0U},
            {"spi_sclk", sample.sclk ? 1U : 0U},
            {"spi_mosi", sample.mosi ? 1U : 0U},
        });
        step();
        vector_file.vectors.push_back(TestVector{
            cycle(),
            {
                {"spi_cs_n", sample.cs_n ? 1U : 0U},
                {"spi_sclk", sample.sclk ? 1U : 0U},
                {"spi_mosi", sample.mosi ? 1U : 0U},
                {"spi_mode", 0U},
            },
            get_outputs(),
        });
    }

    WriteHexVectors(vector_file, output_dir + "/spi_slave_if.hex");
}

void SpiSlaveModel::WriteRegister(uint8_t addr, uint16_t value) {
    reg_bank_.Write(addr, value);
}

uint16_t SpiSlaveModel::ReadRegister(uint8_t addr) {
    return reg_bank_.Read(addr);
}

void SpiSlaveModel::TransferBit(bool cs_n, bool sclk, bool mosi) {
    reg_wr_en_ = false;
    reg_rd_en_ = false;

    if (cs_n) {
        bit_count_ = 0;
        current_addr_ = 0;
        write_shift_ = 0;
        read_shift_ = 0;
        is_read_ = false;
        miso_ = false;
    } else {
        const bool rising = (!prev_sclk_) && sclk;
        const bool falling = prev_sclk_ && (!sclk);
        const bool sample_on_rise = (spi_mode_ == 0U) || (spi_mode_ == 3U);
        const bool shift_on_fall = sample_on_rise;

        if ((sample_on_rise && rising) || (!sample_on_rise && falling)) {
            if (bit_count_ < 8U) {
                current_addr_ = static_cast<uint8_t>((current_addr_ << 1) | (mosi ? 1U : 0U));
                ++bit_count_;

                if (bit_count_ == 8U) {
                    is_read_ = (current_addr_ & 0x80U) == 0U;
                    current_addr_ &= 0x1FU;
                    read_shift_ = reg_bank_.Read(current_addr_);
                    reg_rd_en_ = is_read_;
                }
            } else if (!is_read_ && bit_count_ < 24U) {
                write_shift_ = static_cast<uint16_t>((write_shift_ << 1) | (mosi ? 1U : 0U));
                ++bit_count_;
                if (bit_count_ == 24U) {
                    reg_bank_.Write(current_addr_, write_shift_);
                    reg_wr_en_ = true;
                }
            }
        }

        if ((((shift_on_fall && falling) || (!shift_on_fall && rising))) &&
            is_read_ && bit_count_ >= 8U && bit_count_ < 24U) {
            miso_ = ((read_shift_ & 0x8000U) != 0U);
            read_shift_ = static_cast<uint16_t>(read_shift_ << 1);
            ++bit_count_;
        }
    }

    prev_sclk_ = sclk;
}

}  // namespace fpd::sim
