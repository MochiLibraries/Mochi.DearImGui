@echo off
setlocal

:: Start in the directory containing this script
cd %~dp0

:: Ensure Dear ImGui has been cloned
if not exist external\imgui\ (
    echo Dear ImGui source not found, did you forget to clone recursively? 1>&2
    exit /B 1
)

:: Run generator (will also build Mochi.DearImGui.Native)
dotnet run --configuration Release --project Mochi.DearImGui.Generator -- "external/imgui/" "Mochi.DearImGui.Native/" "Mochi.DearImGui/#Generated/"
