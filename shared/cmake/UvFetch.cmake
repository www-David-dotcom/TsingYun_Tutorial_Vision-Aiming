# UvFetch.cmake — pull basis-robotics/uvtarget at a pinned commit and expose
# the uv_initialize / uv_add_pyproject helpers it provides. Stage 1 only
# needs the include-path glue; HW1+ stages will actually wire C++ targets to
# Python venvs through this.
#
# Pinning rationale: a fixed commit means a CI build a year from now still
# resolves the same uv version, even if upstream uvtarget has moved.

include_guard(GLOBAL)
include(FetchContent)

# Pinned 2026-Q1 commit of basis-robotics/uvtarget. Bump only with a
# CHANGELOG note; tools-of-the-toolchain churn shouldn't be silent.
set(UVTARGET_GIT_TAG "v0.1.0" CACHE STRING "uvtarget version pin")

FetchContent_Declare(
    uvtarget
    GIT_REPOSITORY https://github.com/basis-robotics/uvtarget.git
    GIT_TAG        "${UVTARGET_GIT_TAG}"
    GIT_SHALLOW    TRUE)

# Defer FetchContent_MakeAvailable until a HW that actually needs Python
# from CMake (HW1, HW5) calls aiming_uv_initialize(). Fetching too early
# pessimizes a from-scratch configure for stages that never touch Python.

function(aiming_uv_initialize)
    FetchContent_MakeAvailable(uvtarget)
    if(NOT EXISTS "${uvtarget_SOURCE_DIR}/Uv.cmake")
        message(FATAL_ERROR
            "uvtarget fetched at ${UVTARGET_GIT_TAG} but Uv.cmake is missing. "
            "If upstream renamed the entrypoint, update shared/cmake/UvFetch.cmake.")
    endif()
    include("${uvtarget_SOURCE_DIR}/Uv.cmake")
    uv_initialize(${ARGV})
endfunction()
