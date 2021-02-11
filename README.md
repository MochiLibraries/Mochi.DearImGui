# Infected Dear ImGui

[![MIT Licensed](https://img.shields.io/github/license/infectedlibraries/infectedimgui?style=flat-square)](LICENSE.txt)
[![Sponsor](https://img.shields.io/badge/sponsor-%E2%9D%A4-lightgrey?logo=github&style=flat-square)](https://github.com/sponsors/PathogenDavid)

This repo contains C# bindings for [Dear ImGui](https://github.com/ocornut/imgui/) as well as a [Biohazrd](https://github.com/InfectedLibraries/Biohazrd)-powered generator for generating them.

This project is not ready to be used, if you're looking for an ImGui binding for C# I'd suggest watching releases on this repository and using the excellent [ImGui.NET](https://github.com/mellinoe/ImGui.NET) for the time being. (These bindings work, but they have some very rough edges and not everything works.)

This repository primarily exists to serve as an example what using Biohazrd looks like today with a C++ library that has a relatively simple API. For the sake of demonstration, the output of the generator for Windows x64 is committed under [InfectedImGui/#Generated](InfectedImGui/#Generated).

## License

This project is licensed under the MIT License. [See the license file for details](LICENSE.txt).

Additionally, this project has some third-party dependencies. [See the third-party notice listing for details](THIRD-PARTY-NOTICES.md).

## Building

Building and running is currently only supported on Windows x64 with Visual Studio 2019.

### Prerequisites

Tool | Recommended Version
-----|--------------------
[CMake](https://cmake.org/) | 3.18.4
[Visual Studio 2019](https://visualstudio.microsoft.com/vs/) | 16.8.4
[.NET Core SDK](http://dot.net/) | 5.0

Visual Studio requires the "Desktop development with C++" and  ".NET desktop development" workloads to be installed.

(Note: I am unsure how whether CMake prefers preview or non-preview Visual Studio. You might need non-preview 2019 installed too.)

### Generating the bindings

1. Ensure Git submodules are up-to-date with `git submodule update --init --recursive`
2. Run `InfectedImGui.Native/Build.cmd`
3. Build and run `InfectedImGui.Generator`

Note: The generator will complain about missing exports due to inline methods, which are currently not properly handled by this generator.

### Building the sample

1. Ensure Git submodules are up-to-date with `git submodule update --init --recursive`
2. Run `InfectedImGui.Native/Build.cmd`
3. Open `InfectedImGui.sln` and build/run `InfectedImGui.Sample`

If you make any changes to the ImGui source code or change the branch it uses, you must re-generate the bindings using the instructions above.
