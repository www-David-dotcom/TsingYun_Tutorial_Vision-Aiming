cmake_minimum_required(VERSION 3.22)

# HW4: ballistic + iterative aim solver. Pure C++ on Eigen 3.4.

if(NOT TARGET Eigen3::Eigen)
    message(STATUS "HW4_ballistic: Eigen3 not found, skipping")
    return()
endif()

add_library(hw4_ballistic STATIC
    source/projectile_model.cpp
    source/solver.cpp)
target_include_directories(hw4_ballistic PUBLIC include)
target_compile_features(hw4_ballistic PUBLIC cxx_std_20)
target_compile_options(hw4_ballistic PRIVATE -Wall -Wextra -Wpedantic)
target_link_libraries(hw4_ballistic PUBLIC Eigen3::Eigen)

if(TARGET GTest::gtest_main)
    foreach(name IN ITEMS 1d_no_drag 2d_with_gravity 3d_with_drag)
        add_executable(hw4_${name}_test
            tests/public/test_${name}.cpp)
        target_link_libraries(hw4_${name}_test
            PRIVATE
                hw4_ballistic
                GTest::gtest_main)
        target_compile_features(hw4_${name}_test PRIVATE cxx_std_20)
        include(GoogleTest)
        gtest_discover_tests(hw4_${name}_test)
    endforeach()
endif()
