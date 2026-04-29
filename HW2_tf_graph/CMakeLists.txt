cmake_minimum_required(VERSION 3.22)

# HW2: TF graph. Header-mostly C++ over Eigen 3.4. Skipped at configure
# time when Eigen3 isn't available so contributors who only touch the
# Python side don't hit a hard dep.

if(NOT TARGET Eigen3::Eigen)
    message(STATUS "HW2_tf_graph: Eigen3 not found, skipping")
    return()
endif()

add_library(hw2_tf STATIC
    source/buffer.cpp
    source/interpolate.cpp)
target_include_directories(hw2_tf PUBLIC include)
target_compile_features(hw2_tf PUBLIC cxx_std_20)
target_compile_options(hw2_tf PRIVATE -Wall -Wextra -Wpedantic -Werror)
target_link_libraries(hw2_tf PUBLIC Eigen3::Eigen)

# Public tests, hooked into the top-level GoogleTest fixture from
# tests/CMakeLists.txt. Three cases, one per test file.
if(TARGET GTest::gtest_main)
    foreach(name IN ITEMS basic_lookup interpolation_corners chain_compose)
        add_executable(hw2_${name}_test
            tests/public/test_${name}.cpp)
        target_link_libraries(hw2_${name}_test
            PRIVATE
                hw2_tf
                GTest::gtest_main)
        target_compile_features(hw2_${name}_test PRIVATE cxx_std_20)
        include(GoogleTest)
        gtest_discover_tests(hw2_${name}_test)
    endforeach()
endif()
