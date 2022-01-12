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
:: We don't specify a generator specifically so that CMake will default to the latest installed verison of Visual Studio
:: https://github.com/Kitware/CMake/blob/0c038689be424ca71a6699a993adde3bcaa15b6c/Source/cmake.cxx#L2213-L2214
cmake -S . -B %BUILD_FOLDER% || exit /B 1
echo ==============================================================================
echo Building Mochi.DearImGui.Native %PLATFORM_RID% debug build...
echo ==============================================================================
cmake --build %BUILD_FOLDER% --config Debug || exit /B 1
echo ==============================================================================
echo Building Mochi.DearImGui.Native %PLATFORM_RID% release build...
echo ==============================================================================
cmake --build %BUILD_FOLDER% --config Release || exit /B 1
