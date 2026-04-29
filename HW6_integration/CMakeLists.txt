cmake_minimum_required(VERSION 3.22)

# HW6: integration runner. Pure C++; no Eigen / ONNX dependency in the
# library itself — that surface is wired against HW1/HW3/HW4/HW5
# targets at link time inside `hw_runner` (the CLI binary).

add_library(hw6_pipeline STATIC
    source/runner.cpp
    source/watchdog.cpp)
target_include_directories(hw6_pipeline PUBLIC include)
target_compile_features(hw6_pipeline PUBLIC cxx_std_20)
target_compile_options(hw6_pipeline PRIVATE -Wall -Wextra -Wpedantic)

# Optional thread sanitizer pass — wired by setting
# -DAIMING_HW6_TSAN=ON. The Stage 8 plan calls for ThreadSanitizer
# coverage of the SPSC + watchdog code over 5 episodes.
option(AIMING_HW6_TSAN "Build hw6 with ThreadSanitizer instrumentation" OFF)
if(AIMING_HW6_TSAN)
    target_compile_options(hw6_pipeline PRIVATE -fsanitize=thread -g)
    target_link_options(hw6_pipeline PRIVATE -fsanitize=thread)
endif()

# The hw_runner CLI links the integration library to whatever
# per-HW targets are available in this configure tree. Every link is
# guarded — partial environments (no ONNX, no acados) still produce a
# compiling binary that runs the watchdog + SPSC test path.
add_executable(hw_runner source/main.cpp)
target_compile_features(hw_runner PRIVATE cxx_std_20)
target_link_libraries(hw_runner PRIVATE hw6_pipeline)
if(AIMING_HW6_TSAN)
    target_compile_options(hw_runner PRIVATE -fsanitize=thread -g)
    target_link_options(hw_runner PRIVATE -fsanitize=thread)
endif()

# Wire HW3 + HW4 + HW5 if they're configured — the candidate's
# pipeline calls into them. HW1 (ONNX detector) is not gated here
# because its dependency footprint is heavy; the candidate uncomments
# the line below once they have onnxruntime + a model.onnx.
foreach(dep IN ITEMS hw3_ekf hw4_ballistic hw5_pid_baseline)
    if(TARGET ${dep})
        target_link_libraries(hw_runner PRIVATE ${dep})
    endif()
endforeach()
# if(TARGET hw1_inferer)            target_link_libraries(hw_runner PRIVATE hw1_inferer)            endif()
# if(TARGET hw5_mpc_controller)     target_link_libraries(hw_runner PRIVATE hw5_mpc_controller)     endif()

if(TARGET GTest::gtest_main)
    foreach(name IN ITEMS ring_buffer watchdog)
        add_executable(hw6_${name}_test
            tests/public/test_${name}.cpp)
        target_link_libraries(hw6_${name}_test
            PRIVATE
                hw6_pipeline
                GTest::gtest_main)
        target_compile_features(hw6_${name}_test PRIVATE cxx_std_20)
        if(AIMING_HW6_TSAN)
            target_compile_options(hw6_${name}_test PRIVATE -fsanitize=thread -g)
            target_link_options(hw6_${name}_test PRIVATE -fsanitize=thread)
        endif()
        include(GoogleTest)
        gtest_discover_tests(hw6_${name}_test)
    endforeach()
endif()
