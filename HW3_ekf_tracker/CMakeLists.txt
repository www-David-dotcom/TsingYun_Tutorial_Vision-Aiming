cmake_minimum_required(VERSION 3.22)

# HW3: EKF + IMM tracker. Pure C++ on Eigen 3.4. Skipped at configure
# time when Eigen3 isn't available.

if(NOT TARGET Eigen3::Eigen)
    message(STATUS "HW3_ekf_tracker: Eigen3 not found, skipping")
    return()
endif()

add_library(hw3_ekf STATIC
    source/motion_models.cpp
    source/kalman_step.cpp
    source/imm.cpp
    source/data_association.cpp
    source/tracker.cpp)
target_include_directories(hw3_ekf PUBLIC include)
target_compile_features(hw3_ekf PUBLIC cxx_std_20)
target_compile_options(hw3_ekf PRIVATE -Wall -Wextra -Wpedantic)
target_link_libraries(hw3_ekf PUBLIC Eigen3::Eigen)

if(TARGET GTest::gtest_main)
    foreach(name IN ITEMS cv_predict imm_mode_probabilities da_simple)
        add_executable(hw3_${name}_test
            tests/public/test_${name}.cpp)
        target_link_libraries(hw3_${name}_test
            PRIVATE
                hw3_ekf
                GTest::gtest_main)
        target_compile_features(hw3_${name}_test PRIVATE cxx_std_20)
        include(GoogleTest)
        gtest_discover_tests(hw3_${name}_test)
    endforeach()
endif()
