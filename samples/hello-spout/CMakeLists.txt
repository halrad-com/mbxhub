cmake_minimum_required(VERSION 3.15)   # 3.15 is what makes MSVC_RUNTIME_LIBRARY (CMP0091) work
project(hello_spout CXX)

# The SEND half links the vendored SDK straight in. The RECEIVE half does NOT link anything
# of mbxspout's - it takes the header for the types and loads the DLL at runtime, which is
# exactly what a partner integration does.
# WHERE THE VENDORED SDK IS, now that this sample lives in a different repo from it.
# mbxspout carries Spout2 under third_party; this sample sits in mbxhub. The default below
# reaches a SIBLING checkout - ..\..\..\mbxspout - which is the layout every machine here
# already has, and -DSPOUT_ROOT=<path> overrides it for one that does not.
#
# It is checked rather than assumed: a path that does not exist produces a wall of
# "cannot open source file" from the compiler, which says nothing about the real problem.
if(NOT DEFINED SPOUT_ROOT)
    set(SPOUT_ROOT ${CMAKE_CURRENT_SOURCE_DIR}/../../../mbxspout/third_party/Spout2/SPOUTSDK)
endif()
if(NOT EXISTS ${SPOUT_ROOT}/SpoutGL/SpoutUtils.cpp)
    message(FATAL_ERROR
        "Spout2 sources not found at ${SPOUT_ROOT}.
"
        "This sample builds against the SDK vendored in the mbxspout repo, so it needs both "
        "repos checked out side by side. Clone mbxspout beside mbxhub, or pass "
        "-DSPOUT_ROOT=<path-to>/Spout2/SPOUTSDK.")
endif()
set(SPOUT_SOURCES
    ${SPOUT_ROOT}/SpoutDirectX/SpoutDX/SpoutDX.cpp
    ${SPOUT_ROOT}/SpoutGL/SpoutCopy.cpp
    ${SPOUT_ROOT}/SpoutGL/SpoutDirectX.cpp
    ${SPOUT_ROOT}/SpoutGL/SpoutFrameCount.cpp
    ${SPOUT_ROOT}/SpoutGL/SpoutSenderNames.cpp
    ${SPOUT_ROOT}/SpoutGL/SpoutSharedMemory.cpp
    ${SPOUT_ROOT}/SpoutGL/SpoutUtils.cpp
)

add_executable(hello-spout hello-spout.cpp ${SPOUT_SOURCES})

# Static CRT, same reason as the DLL: a sample that needs a redistributable installed before
# it will start is a sample that teaches the wrong lesson on someone else's machine.
set_property(TARGET hello-spout PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreaded")

target_include_directories(hello-spout PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/../../include              # mbxspout.h - the contract
    ${SPOUT_ROOT}/SpoutDirectX/SpoutDX
    ${SPOUT_ROOT}/SpoutGL)
target_compile_definitions(hello-spout PRIVATE SPOUT_BUILD_STATIC _CRT_SECURE_NO_WARNINGS NOMINMAX)
target_compile_features(hello-spout PRIVATE cxx_std_17)
target_link_libraries(hello-spout PRIVATE d3d11 dxgi winhttp winmm shell32 advapi32 version comctl32 user32 gdi32)

# The charm reads its own manifest from beside the exe at --register time.
add_custom_command(TARGET hello-spout POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
            ${CMAKE_CURRENT_SOURCE_DIR}/manifest.json
            $<TARGET_FILE_DIR:hello-spout>/manifest.json)
