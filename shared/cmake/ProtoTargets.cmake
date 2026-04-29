# ProtoTargets.cmake — small helper that wraps protoc + grpc_cpp_plugin into
# a single CMake target so individual subprojects don't have to repeat the
# incantation. Stage 1 only needs C++ codegen; Python codegen happens via
# the grpcio-tools `python -m grpc_tools.protoc` invocation in pyproject
# scripts and does not need a CMake target.
#
# Usage:
#     aiming_add_proto(aiming_proto
#         PROTOS
#             shared/proto/aiming.proto
#             shared/proto/sensor.proto
#         IMPORT_DIRS
#             shared/proto)
#
# Produces a static library named `aiming_proto` exposing the generated
# .pb.h / .grpc.pb.h headers; depend on it with target_link_libraries(...).

include_guard(GLOBAL)

function(aiming_add_proto target)
    cmake_parse_arguments(ARG "" "" "PROTOS;IMPORT_DIRS" ${ARGN})
    if(NOT ARG_PROTOS)
        message(FATAL_ERROR "aiming_add_proto: PROTOS is required")
    endif()

    find_program(PROTOC_EXECUTABLE protoc REQUIRED)
    find_program(GRPC_CPP_PLUGIN grpc_cpp_plugin REQUIRED)

    set(_gen_dir "${CMAKE_CURRENT_BINARY_DIR}/${target}_gen")
    file(MAKE_DIRECTORY "${_gen_dir}")

    set(_import_args)
    foreach(_dir ${ARG_IMPORT_DIRS})
        list(APPEND _import_args "-I${CMAKE_CURRENT_SOURCE_DIR}/${_dir}")
    endforeach()

    set(_generated_srcs)
    foreach(_proto ${ARG_PROTOS})
        get_filename_component(_proto_name "${_proto}" NAME_WE)
        set(_pb_cc      "${_gen_dir}/${_proto_name}.pb.cc")
        set(_pb_h       "${_gen_dir}/${_proto_name}.pb.h")
        set(_grpc_pb_cc "${_gen_dir}/${_proto_name}.grpc.pb.cc")
        set(_grpc_pb_h  "${_gen_dir}/${_proto_name}.grpc.pb.h")

        add_custom_command(
            OUTPUT  "${_pb_cc}" "${_pb_h}" "${_grpc_pb_cc}" "${_grpc_pb_h}"
            COMMAND "${PROTOC_EXECUTABLE}"
                ${_import_args}
                "--cpp_out=${_gen_dir}"
                "--grpc_out=${_gen_dir}"
                "--plugin=protoc-gen-grpc=${GRPC_CPP_PLUGIN}"
                "${CMAKE_CURRENT_SOURCE_DIR}/${_proto}"
            DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/${_proto}"
            COMMENT "protoc + grpc_cpp_plugin: ${_proto}"
            VERBATIM)

        list(APPEND _generated_srcs "${_pb_cc}" "${_grpc_pb_cc}")
    endforeach()

    add_library(${target} STATIC ${_generated_srcs})
    target_include_directories(${target} PUBLIC "${_gen_dir}")
    target_link_libraries(${target} PUBLIC protobuf::libprotobuf gRPC::grpc++)

    if(CMAKE_CXX_COMPILER_ID MATCHES "GNU|Clang|AppleClang")
        # Generated code triggers -Wshadow / -Wpedantic; quiet just for the
        # generated TU, not for downstream callers.
        target_compile_options(${target} PRIVATE -w)
    endif()
endfunction()
