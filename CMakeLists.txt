cmake_minimum_required(VERSION 3.22)
project(aiming_hw
    VERSION 1.4.0
    DESCRIPTION "TsingYun RoboMaster Vision Group recruitment-cycle assignment template"
    LANGUAGES CXX)

# --- standards & warnings -----------------------------------------------------

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE RelWithDebInfo CACHE STRING "Build type" FORCE)
endif()

if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang|AppleClang")
    add_compile_options(-Wall -Wextra -Wpedantic -Wshadow -Wnon-virtual-dtor)
endif()

# --- helper modules -----------------------------------------------------------

list(PREPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/shared/cmake")
include(UvFetch)
include(ProtoTargets)

# --- dependencies (find via vcpkg manifest or system) -------------------------

find_package(Eigen3 3.4 CONFIG)
find_package(Protobuf CONFIG)
find_package(gRPC CONFIG)
find_package(absl CONFIG)

if(NOT Protobuf_FOUND OR NOT gRPC_FOUND)
    message(WARNING
        "Protobuf and/or gRPC not found via find_package. Either supply a vcpkg "
        "toolchain (-DCMAKE_TOOLCHAIN_FILE=...) or build inside the reference "
        "Docker image (shared/docker/toolchain.Dockerfile). Targets that need "
        "these will be skipped.")
endif()

# --- proto codegen ------------------------------------------------------------

if(Protobuf_FOUND AND gRPC_FOUND)
    aiming_add_proto(aiming_proto
        PROTOS
            shared/proto/aiming.proto
            shared/proto/sensor.proto
            shared/proto/episode.proto
        IMPORT_DIRS
            shared/proto)
endif()

# --- subdirs ------------------------------------------------------------------

# Stages 2+ progressively populate HW1..HW7 / shared/godot_arena. Each
# per-HW CMakeLists is responsible for guarding its own optional deps
# (e.g. HW1 skips itself when ONNX Runtime isn't installed) so a
# partial environment still produces a green build.

enable_testing()
add_subdirectory(tests)

if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/HW1_armor_detector/CMakeLists.txt")
    add_subdirectory(HW1_armor_detector)
endif()

if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/HW2_tf_graph/CMakeLists.txt")
    add_subdirectory(HW2_tf_graph)
endif()

if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/HW3_ekf_tracker/CMakeLists.txt")
    add_subdirectory(HW3_ekf_tracker)
endif()

if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/HW4_ballistic/CMakeLists.txt")
    add_subdirectory(HW4_ballistic)
endif()

if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/HW5_mpc_gimbal/CMakeLists.txt")
    add_subdirectory(HW5_mpc_gimbal)
endif()

if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/HW6_integration/CMakeLists.txt")
    add_subdirectory(HW6_integration)
endif()

if(EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/HW7_strategy/CMakeLists.txt")
    add_subdirectory(HW7_strategy)
endif()

# --- summary ------------------------------------------------------------------

message(STATUS "")
message(STATUS "  aiming_hw ${PROJECT_VERSION} configured")
message(STATUS "    build type      = ${CMAKE_BUILD_TYPE}")
message(STATUS "    C++ standard    = ${CMAKE_CXX_STANDARD}")
message(STATUS "    Eigen3          = ${Eigen3_FOUND}")
message(STATUS "    Protobuf        = ${Protobuf_FOUND}")
message(STATUS "    gRPC            = ${gRPC_FOUND}")
message(STATUS "")
