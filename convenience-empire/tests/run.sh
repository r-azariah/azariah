#!/usr/bin/env bash
# Runs the Convenience Empire checks outside Roblox.
# Needs the Luau CLI tools (luau, luau-analyze) on PATH: https://github.com/luau-lang/luau/releases
set -euo pipefail
cd "$(dirname "$0")"

combined="$(mktemp --suffix=.luau)"
trap 'rm -f "$combined"' EXIT

echo "== Type-checking shared modules"
luau-analyze ../src/shared/Config.luau ../src/shared/ProductCatalog.luau

echo "== Shared modules"
luau shared_check.luau

echo "== Store builder layout"
cat roblox_mock.luau ../studio/BuildStoreTemplate.luau builder_check.luau > "$combined"
luau "$combined"
