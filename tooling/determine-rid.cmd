@echo off

if not "%FORCE_RID%" == "" (
    set PLATFORM_RID=%FORCE_RID%
) else if /i "%PROCESSOR_ARCHITECTURE%" == "AMD64" (
    set PLATFORM_RID=win-x64
) else if /i "%PROCESSOR_ARCHITECTURE%" == "ARM64" (
    set PLATFORM_RID=win-arm64
) else if /i "%PROCESSOR_ARCHITECTURE%" == "X86" (
    set PLATFORM_RID=win-x86
) else (
    echo Unrecognized processor architecture '%PROCESSOR_ARCHITECTURE%'. Not sure what to use for RID. 1>&2
    echo You can force a specific RID by setting FORCE_RID. 1>&2
    exit /B 1
)
