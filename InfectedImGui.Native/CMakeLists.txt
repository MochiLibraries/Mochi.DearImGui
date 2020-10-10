cmake_minimum_required(VERSION 3.18)

set(IMGUI_SOURCE_DIRECTORY "../external/imgui/")
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/bin")

project(InfectedImGui.Native)
file(GLOB IMGUI_SOURCES "${IMGUI_SOURCE_DIRECTORY}*.cpp")
add_library(InfectedImGui.Native SHARED ${IMGUI_SOURCES})
include_directories("${IMGUI_SOURCE_DIRECTORY}" ".")
add_compile_definitions(IMGUI_USER_CONFIG="InfectedImGuiConfig.h")
