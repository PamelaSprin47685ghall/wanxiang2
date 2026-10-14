#!/usr/bin/env bash
# 用旧代码重新生成 ../../tests/golden/figure.txt。
#   用法：tools/figuregold/run.sh <旧仓库检出目录>（extract-ledger 0c0b2a6）
#   先 ./x pack（或至少打 MathTypeset、MathBaseline、RichJint 及其依赖到 artifacts/feed）。
set -euo pipefail
old="$(realpath "$1")"; here="$(cd "$(dirname "$0")" && pwd)"
dotnet run -c Release --project "$here/figuregold.fsproj" -p:OldRepo="$old" -p:RestorePackagesPath="$here/../../../../artifacts/packages" -- "$here/../../tests/golden/figure.txt"
