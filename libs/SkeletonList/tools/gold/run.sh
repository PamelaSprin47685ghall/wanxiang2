#!/usr/bin/env bash
# 用旧代码重新生成 ../../tests/golden/skeleton.txt。用法：tools/gold/run.sh <旧仓库检出目录>
set -euo pipefail
old="$(realpath "$1")"; here="$(cd "$(dirname "$0")" && pwd)"
bin="$old/tests/Wanxiang.Tests/bin/Debug/net10.0"
[[ -f "$bin/Wanxiang.UI.dll" ]] || dotnet build "$old/tests/Wanxiang.Tests/Wanxiang.Tests.fsproj" -c Debug
dotnet run -c Release --project "$here/gold.fsproj" -p:OldBin="$bin" -- "$here/../../tests/golden/skeleton.txt"
