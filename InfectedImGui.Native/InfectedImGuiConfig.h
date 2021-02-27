#pragma once
#define IMGUI_API __declspec(dllexport)
#define IMGUI_IMPL_API extern "C" __declspec(dllexport)
#define IMGUI_DISABLE_OBSOLETE_FUNCTIONS

// Workaround for missing IMGUI_API on a few methods
// Will be fixed by https://github.com/ocornut/imgui/pull/3850
//#pragma comment(linker, "/export:??0ImGuiTabBar@@QEAA@XZ")
//#pragma comment(linker, "/export:??1ImGuiDockNode@@QEAA@XZ")
//#pragma comment(linker, "/export:??0ImGuiDockNode@@QEAA@I@Z")
struct IMGUI_API ImGuiTabBar;
struct IMGUI_API ImGuiDockNode;
