#!/usr/bin/env bash
# Cross-builds the portable Windows AZARIAH.exe from Linux/macOS.
# Usage: scripts/publish.sh [win-x64|win-arm64]
set -euo pipefail
export AVALONIA_TELEMETRY_OPTOUT=1 DOTNET_CLI_TELEMETRY_OPTOUT=1
runtime="${1:-win-x64}"
repo="$(cd "$(dirname "$0")/.." && pwd)"
dotnet publish "$repo/src/Azariah.App" -c Release -r "$runtime" -o "$repo/publish/$runtime"
echo "Built $repo/publish/$runtime/Azariah.exe"
