#!/usr/bin/env bash
# 用旧代码重新生成 ../../tests/golden/math.txt。
#   用法：tools/mathgold/run.sh <旧仓库检出目录>
#   旧仓库取 extract-ledger 的 68e10a5^（之后 RichJint/KatexHtml 已迁走），例如：
#     git -C wanxiang archive 68e10a5^ src tests | tar -x -C /tmp/oldsnap
set -euo pipefail
old="$(realpath "$1")"; here="$(cd "$(dirname "$0")" && pwd)"
dotnet run -c Release --project "$here/mathgold.fsproj" -p:OldRepo="$old" -- "$here/../../tests/golden/math.txt" "$old/tests/Wanxiang.Tests/fixtures"
