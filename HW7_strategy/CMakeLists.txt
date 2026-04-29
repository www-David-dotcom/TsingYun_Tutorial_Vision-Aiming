cmake_minimum_required(VERSION 3.22)

# HW7: strategy bonus. Pure C++; behaviour-tree runtime + leaf
# actions + the candidate's TODO targets in strategy.cpp.

add_library(hw7_strategy STATIC
    source/strategy.cpp
    source/leaf_actions.cpp)
target_include_directories(hw7_strategy PUBLIC include)
target_compile_features(hw7_strategy PUBLIC cxx_std_20)
target_compile_options(hw7_strategy PRIVATE -Wall -Wextra -Wpedantic)

if(TARGET GTest::gtest_main)
    foreach(name IN ITEMS priority_distance retreat_trigger bt_semantics)
        add_executable(hw7_${name}_test
            tests/public/test_${name}.cpp)
        target_link_libraries(hw7_${name}_test
            PRIVATE
                hw7_strategy
                GTest::gtest_main)
        target_compile_features(hw7_${name}_test PRIVATE cxx_std_20)
        include(GoogleTest)
        gtest_discover_tests(hw7_${name}_test)
    endforeach()
endif()
