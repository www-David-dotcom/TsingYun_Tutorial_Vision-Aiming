cmake_minimum_required(VERSION 3.22)

# HW1 inferer + post-processing. Skipped at configure time if ONNX
# Runtime isn't available on the host; the rest of the project keeps
# building so contributors who only touch the Python or Godot sides
# don't need a CUDA-grade environment.

find_package(onnxruntime QUIET CONFIG)
if(NOT onnxruntime_FOUND)
    # Fall back to manual probe — many Linux distros ship the runtime
    # as a plain .so under /usr/local without a CMake config package.
    find_path(ONNXRUNTIME_INCLUDE_DIR
        NAMES onnxruntime_cxx_api.h
        PATHS /usr/local/include /usr/include /opt/onnxruntime/include
        PATH_SUFFIXES onnxruntime onnxruntime/core/session)
    find_library(ONNXRUNTIME_LIB
        NAMES onnxruntime
        PATHS /usr/local/lib /usr/lib /opt/onnxruntime/lib)
    if(NOT (ONNXRUNTIME_INCLUDE_DIR AND ONNXRUNTIME_LIB))
        message(STATUS "HW1_armor_detector: ONNX Runtime not found, skipping")
        return()
    endif()
endif()

add_library(hw1_post_process STATIC
    source/post_process.cpp)
target_include_directories(hw1_post_process PUBLIC include)
target_compile_features(hw1_post_process PUBLIC cxx_std_20)
target_compile_options(hw1_post_process PRIVATE -Wall -Wextra -Wpedantic)

add_library(hw1_inferer STATIC
    source/inferer.cpp)
target_include_directories(hw1_inferer PUBLIC include)
target_compile_features(hw1_inferer PUBLIC cxx_std_20)
target_compile_options(hw1_inferer PRIVATE -Wall -Wextra -Wpedantic)
target_link_libraries(hw1_inferer PUBLIC hw1_post_process)

if(onnxruntime_FOUND)
    target_link_libraries(hw1_inferer PUBLIC onnxruntime::onnxruntime)
else()
    target_include_directories(hw1_inferer PRIVATE ${ONNXRUNTIME_INCLUDE_DIR})
    target_link_libraries(hw1_inferer PRIVATE ${ONNXRUNTIME_LIB})
endif()

add_executable(hw1_inferer_smoke source/main_smoke.cpp)
target_link_libraries(hw1_inferer_smoke PRIVATE hw1_inferer)
target_compile_features(hw1_inferer_smoke PRIVATE cxx_std_20)

# Public C++ test (decoded boxes + NMS edge cases). Hooked into the
# top-level GoogleTest fixture from tests/CMakeLists.txt; only built
# when that fixture is configured.
if(TARGET GTest::gtest_main)
    add_executable(hw1_post_process_test
        tests/public/test_post_process.cpp)
    target_link_libraries(hw1_post_process_test
        PRIVATE
            hw1_post_process
            GTest::gtest_main)
    target_compile_features(hw1_post_process_test PRIVATE cxx_std_20)
    include(GoogleTest)
    gtest_discover_tests(hw1_post_process_test)
endif()
