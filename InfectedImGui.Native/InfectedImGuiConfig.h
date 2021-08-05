#pragma once
#ifdef _WIN32
#define IMGUI_API __declspec(dllexport)
#define IMGUI_IMPL_API extern "C" __declspec(dllexport)
#else
#define IMGUI_API
#define IMGUI_IMPL_API extern "C"
#endif
#define IMGUI_DISABLE_OBSOLETE_FUNCTIONS
