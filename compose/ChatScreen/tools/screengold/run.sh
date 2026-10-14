#!/usr/bin/env bash
# 用旧代码重新生成 ../../tests/golden/screen.txt。
#   用法：tools/screengold/run.sh <旧仓库检出目录>（extract-ledger 533b85b）
set -euo pipefail
old="$(realpath "$1")"; here="$(cd "$(dirname "$0")" && pwd)"
bin="$old/tests/Wanxiang.Tests/bin/Debug/net10.0"
[[ -f "$bin/Wanxiang.UI.dll" ]] || dotnet build "$old/tests/Wanxiang.Tests/Wanxiang.Tests.fsproj" -c Debug
dotnet run -c Release --project "$here/screengold.fsproj" -p:OldBin="$bin" -- "$here/../../tests/golden/screen.txt"
