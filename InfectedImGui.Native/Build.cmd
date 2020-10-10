@echo off
cmake -G "Visual Studio 16 2019" -S . -B build
cmake --build build --config Debug
cmake --build build --config Release
