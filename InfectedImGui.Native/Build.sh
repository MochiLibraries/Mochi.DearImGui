#!/usr/bin/env bash
cmake -G "Unix Makefiles" -S . -B build-linux
cmake --build build-linux --config Debug
#cmake --build build-linux --config Release
