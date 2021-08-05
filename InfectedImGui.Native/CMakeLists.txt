cmake_minimum_required(VERSION 3.16)

set(IMGUI_SOURCE_DIRECTORY "../external/imgui/")
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}/bin")

project(InfectedImGui.Native)
file(GLOB IMGUI_SOURCES "${IMGUI_SOURCE_DIRECTORY}*.cpp")
add_library(InfectedImGui.Native SHARED ${IMGUI_SOURCES} ${IMGUI_SOURCE_DIRECTORY}backends/imgui_impl_opengl3.cpp ${IMGUI_SOURCE_DIRECTORY}backends/imgui_impl_glfw.cpp InfectedImGui.cpp ${IMGUI_SOURCE_DIRECTORY}examples/libs/gl3w/GL/gl3w.c)
include_directories("${IMGUI_SOURCE_DIRECTORY}" ".")
add_compile_definitions(IMGUI_USER_CONFIG="InfectedImGuiConfig.h")

add_compile_definitions(IMGUI_IMPL_OPENGL_LOADER_GL3W)
include_directories("${IMGUI_SOURCE_DIRECTORY}examples/libs/gl3w/")
