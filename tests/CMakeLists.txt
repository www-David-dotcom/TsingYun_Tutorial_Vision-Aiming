if(NOT TARGET aiming_proto)
    message(STATUS "tests/: skipping (aiming_proto target not configured)")
    return()
endif()

include(FetchContent)
FetchContent_Declare(
    googletest
    GIT_REPOSITORY https://github.com/google/googletest.git
    GIT_TAG        v1.15.2
    GIT_SHALLOW    TRUE)
FetchContent_MakeAvailable(googletest)

add_executable(proto_roundtrip_test proto_roundtrip_test.cpp)
target_link_libraries(proto_roundtrip_test
    PRIVATE
        GTest::gtest_main
        aiming_proto)

include(GoogleTest)
gtest_discover_tests(proto_roundtrip_test)
