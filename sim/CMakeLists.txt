cmake_minimum_required(VERSION 3.20)

project(fpd_sw_first LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

enable_testing()

add_library(golden_core STATIC
    golden_models/core/GoldenModelBase.cpp
    golden_models/core/ClockDomain.cpp
    golden_models/core/TestVectorIO.cpp
    golden_models/core/CRC16.cpp
    golden_models/core/ECC.cpp
)

target_include_directories(golden_core PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)

add_library(golden_models STATIC
    golden_models/models/RegBankModel.cpp
    golden_models/models/SpiSlaveModel.cpp
    golden_models/models/ClkRstModel.cpp
    golden_models/models/PanelFsmModel.cpp
    golden_models/models/ProtMonModel.cpp
    golden_models/models/PowerSeqModel.cpp
    golden_models/models/EmergencyShutdownModel.cpp
    golden_models/models/RowScanModel.cpp
    golden_models/models/AfeAd711xxModel.cpp
    golden_models/models/AfeAfe2256Model.cpp
    golden_models/models/LineBufModel.cpp
    golden_models/models/Csi2PacketModel.cpp
    golden_models/models/Csi2LaneDistModel.cpp
)

target_include_directories(golden_models PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}
)
target_link_libraries(golden_models PUBLIC golden_core)

add_executable(gen_spi_vectors
    golden_models/generators/gen_spi_vectors.cpp
)
target_link_libraries(gen_spi_vectors PRIVATE golden_models)

add_executable(test_crc16 tests/test_crc16.cpp)
target_link_libraries(test_crc16 PRIVATE golden_core)
add_test(NAME test_crc16 COMMAND test_crc16)

add_executable(test_ecc tests/test_ecc.cpp)
target_link_libraries(test_ecc PRIVATE golden_core)
add_test(NAME test_ecc COMMAND test_ecc)

add_executable(test_reg_bank tests/test_reg_bank.cpp)
target_link_libraries(test_reg_bank PRIVATE golden_models)
add_test(NAME test_reg_bank COMMAND test_reg_bank)

add_executable(test_spi_model tests/test_spi_model.cpp)
target_link_libraries(test_spi_model PRIVATE golden_models)
add_test(NAME test_spi_model COMMAND test_spi_model)

add_executable(test_clk_rst tests/test_clk_rst.cpp)
target_link_libraries(test_clk_rst PRIVATE golden_models)
add_test(NAME test_clk_rst COMMAND test_clk_rst)

add_executable(test_panel_fsm tests/test_panel_fsm.cpp)
target_link_libraries(test_panel_fsm PRIVATE golden_models)
add_test(NAME test_panel_fsm COMMAND test_panel_fsm)

add_executable(test_csi2_model tests/test_csi2_model.cpp)
target_link_libraries(test_csi2_model PRIVATE golden_models)
add_test(NAME test_csi2_model COMMAND test_csi2_model)
