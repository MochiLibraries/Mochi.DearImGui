# Infected Dear ImGui

[![MIT Licensed](https://img.shields.io/github/license/infectedlibraries/clangsharp.pathogen?style=flat-square)](LICENSE.txt)
[![Sponsor](https://img.shields.io/badge/sponsor-%E2%9D%A4-lightgrey?logo=github&style=flat-square)](https://github.com/sponsors/PathogenDavid)

This repo contains C# bindings for [Dear ImGui](https://github.com/ocornut/imgui/) as well as a [Biohazrd](https://github.com/InfectedLibraries/Biohazrd)-powered generator for generating them.

This project is not ready to be used, if you're looking for an ImGui binding for C# I'd suggest watching releases on this repository and using the excellent [ImGui.NET](https://github.com/mellinoe/ImGui.NET) for the time being.

This repository primarily exists to serve as an example what using Biohazrd looks like today with a C++ library that has a relatively simple API. For the sake of demonstration, the output of the generator for Windows x64 is comitted under [InfectedImGui/#Generated](InfectedImGui/#Generated).

## License

This project is licensed under the MIT License. [See the license file for details](LICENSE.txt).

Additionally, this project has some third-party dependencies. [See the third-party notice listing for details](THIRD-PARTY-NOTICES.md).

## Generating the bindings

1. Ensure Git submodules are up-to-date with `git submodule update --init --recursive`
2. Build and run `InfectedImGui.Generator`
