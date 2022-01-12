# Dear ImGui-flavored Mochi

[![MIT Licensed](https://img.shields.io/github/license/mochilibraries/mochi.dearimgui?style=flat-square)](LICENSE.txt)
[![CI Status](https://img.shields.io/github/workflow/status/mochilibraries/mochi.dearimgui/Mochi.DearImGui/main?style=flat-square&label=CI)](https://github.com/MochiLibraries/Mochi.DearImGui/actions?query=workflow%3AMochi.DearImGui+branch%3Amain)
[![NuGet Version](https://img.shields.io/nuget/v/Mochi.DearImGui?style=flat-square)](https://www.nuget.org/packages/Mochi.DearImGui/)
[![Sponsor](https://img.shields.io/badge/sponsor-%E2%9D%A4-lightgrey?logo=github&style=flat-square)](https://github.com/sponsors/PathogenDavid)

This repo contains C# bindings for [Dear ImGui](https://github.com/ocornut/imgui/) as well as a [Biohazrd](https://github.com/MochiLibraries/Biohazrd)-powered generator for generating them.

We currently publish NuGet packages for .NET 6 on Windows x64 and Linux x64 (glibc >= 2.27). The `Mochi.DearImGui` package currently only provides Windows support, it will be a cross-platform meta package in the future. `Mochi.DearImGui.linux-x64` provides support for Linux.

A backend for OpenTK is published as `Mochi.DearImGui.OpenTK`, and is platform-independent. See [`Mochi.DearImGui.Sample`](Mochi.DearImGui.Sample/Program.cs) for example usage.

In contrast to other C# bindings for Dear ImGui, this one interacts with the C++ API directly and is lower-level. If you need high-level bindings consider using the excellent [ImGui.NET](https://github.com/mellinoe/ImGui.NET) instead.

## License

This project is licensed under the MIT License. [See the license file for details](LICENSE.txt).

Additionally, this project has some third-party dependencies. [See the third-party notice listing for details](THIRD-PARTY-NOTICES.md).

## Building

### Windows Prerequisites

Windows 10 21H2 x64 is recommended.

Tool | Tested Version
-----|--------------------
[Visual Studio](https://visualstudio.microsoft.com/vs/) | 2022 (17.1.0p2)
[.NET 6.0 SDK](http://dot.net/) | 6.0.101
[CMake](https://cmake.org/) | 3.22.0

Visual Studio must have the "Desktop development with C++" workload installed.

### Linux Prerequisites

Ubuntu 20.04 Focal x64 is recommended, but most distros are expected to work. (Mochi.DearImGui itself should also work on Linux ARM64, but the OpenTK backend doesn't since OpenTK's GLFW redistributable doesn't.)

Package | Tested Version
--------|--------------------
`build-essential` | 12.8
`cmake` | 3.16.3
`dotnet-sdk-6.0` | 6.0.100

### Building Dear ImGui and generating the bindings

1. Ensure Git submodules are up-to-date with `git submodule update --init --recursive`
2. Build and run `generate.cmd` (Windows) or `generate.sh` (Linux) from the repository root

### Building and running the sample

Without modification the sample will depend on the bindings being built locally as instructed above.

Simply build+run `Mochi.DearImGui.Sample` as you would any other .NET project. (IE: Using F5 in Visual Studio or `dotnet run --project Mochi.DearImGui.Sample`.)
