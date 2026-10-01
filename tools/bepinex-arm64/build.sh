#!/bin/bash
# Rebuilds the BepInEx.dll / BepInEx.Preloader.dll that patches/core~*.bsdiff produce.
#
# Source: BepInEx v5-lts at f4c1b11 (LGPL-2.1), which includes PR #1402 "Fix plugin loading on native arm64
# macOS" by cdobbyn. That fix isn't in a BepInEx release yet (5.4.23.5 is the latest). Once a release ships it,
# these patches go away and the installer downloads that release instead.
#
# Needs the .NET 8 SDK (built with 8.0.425). The build is deterministic: the output hashes match
# manifest/patches-native.tsv.
set -euo pipefail
COMMIT=f4c1b11
OUT="${1:-$PWD/bepinex-arm64-build}"
rm -rf "$OUT" && git clone -q https://github.com/BepInEx/BepInEx.git "$OUT"
cd "$OUT" && git checkout -q "$COMMIT" && git submodule update --init --quiet
dotnet build BepInEx.Preloader/BepInEx.Preloader.csproj -c Release -nologo -v q \
  -p:BepInExVersionSuffix=macarm64.$COMMIT "-p:PathMap=$PWD=/_/" -p:ContinuousIntegrationBuild=true
shasum -a 256 BepInEx.Preloader/bin/Release/net35/BepInEx.dll BepInEx.Preloader/bin/Release/net35/BepInEx.Preloader.dll
