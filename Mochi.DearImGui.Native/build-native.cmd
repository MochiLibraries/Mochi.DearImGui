@echo off
setlocal enabledelayedexpansion

:: Start in the directory containing this script
cd %~dp0

:: Determine platform RID and build folder
call ..\tooling\determine-rid.cmd || exit /B !ERRORLEVEL!
set BUILD_FOLDER=..\obj\Mochi.DearImGui.Native\cmake\%PLATFORM_RID%

:: Ensure build folder is protected from Directory.Build.* influences
if not exist %BUILD_FOLDER% (
    mkdir %BUILD_FOLDER%
    echo ^<Project^>^</Project^> > %BUILD_FOLDER%/Directory.Build.props
    echo ^<Project^>^</Project^> > %BUILD_FOLDER%/Directory.Build.targets
    echo # > %BUILD_FOLDER%/Directory.Build.rsp
)

:: (Re)generate the Visual Studio solution and build in all configurations
cmake -G "Visual Studio 16 2019" -S . -B %BUILD_FOLDER% || exit /B 1
echo ==============================================================================
echo Building Mochi.DearImGui.Native %PLATFORM_RID% debug build...
echo ==============================================================================
cmake --build %BUILD_FOLDER% --config debug || exit /B 1
echo ==============================================================================
echo Building Mochi.DearImGui.Native %PLATFORM_RID% release build...
echo ==============================================================================
cmake --build %BUILD_FOLDER% --config release || exit /B 1
