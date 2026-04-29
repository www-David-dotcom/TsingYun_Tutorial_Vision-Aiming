cmake_minimum_required(VERSION 3.22)

# HW5: MPC gimbal controller. Two-target build:
#   * `hw5_pid_baseline`  — always built, no acados dependency.
#   * `hw5_mpc_controller`— only built when the candidate has run
#                            `python src/generate_acados.py` and the
#                            generated_solver/acados_aiming_mpc/
#                            CMakeLists.txt exists.

add_library(hw5_pid_baseline STATIC
    source/pid_baseline.cpp)
target_include_directories(hw5_pid_baseline PUBLIC include)
target_compile_features(hw5_pid_baseline PUBLIC cxx_std_20)
target_compile_options(hw5_pid_baseline PRIVATE -Wall -Wextra -Wpedantic -Werror)

set(_hw5_acados_dir
    "${CMAKE_CURRENT_SOURCE_DIR}/generated_solver/acados_aiming_mpc")
if(EXISTS "${_hw5_acados_dir}/CMakeLists.txt")
    message(STATUS "HW5: acados-codegened solver found, building hw5_mpc_controller")
    add_subdirectory("${_hw5_acados_dir}")
    add_library(hw5_mpc_controller STATIC
        source/controller.cpp)
    target_include_directories(hw5_mpc_controller
        PUBLIC include
        PRIVATE "${_hw5_acados_dir}")
    target_compile_features(hw5_mpc_controller PUBLIC cxx_std_20)
    target_link_libraries(hw5_mpc_controller
        PUBLIC hw5_pid_baseline
        PRIVATE acados_aiming_mpc)
else()
    message(STATUS "HW5: no acados solver under generated_solver/, skipping MPC target")
    # A header-only stub so callers can still `#include` controller.hpp;
    # the MPC class compiles but its step() is a no-op trampoline. The
    # candidate's test for the MPC path bails when this stub is in
    # play.
    add_library(hw5_mpc_controller STATIC
        source/controller.cpp)
    target_include_directories(hw5_mpc_controller PUBLIC include)
    target_compile_features(hw5_mpc_controller PUBLIC cxx_std_20)
    target_link_libraries(hw5_mpc_controller PUBLIC hw5_pid_baseline)
endif()

if(TARGET GTest::gtest_main)
    foreach(name IN ITEMS step_response sinusoid_tracking)
        add_executable(hw5_${name}_test
            tests/public/test_${name}.cpp)
        target_link_libraries(hw5_${name}_test
            PRIVATE
                hw5_pid_baseline
                GTest::gtest_main)
        target_compile_features(hw5_${name}_test PRIVATE cxx_std_20)
        include(GoogleTest)
        gtest_discover_tests(hw5_${name}_test)
    endforeach()
endif()
