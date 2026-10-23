#!/usr/bin/env bash
# 画廊深浅主题 PNG 像素基线核查与生成门禁（展望.md §3 G3 自动化）
#
# 契约：
#   1. 基线必须由 DevOps 在目标环境下真实生成，不能伪造，不能静默放行；
#   2. 缺少基线文件时必须以非零退出码（1）失败并标红；
#   3. 出现像素差异或尺寸不符时必须以非零退出码（1）失败并标红，同时输出 diff 高亮图；
#   4. 仅当全部条目的深色与浅色模式均 100% 逐像素匹配时退出码为 0。
#
# 用法：
#   bash tools/gallery-baseline.sh                               # 核查默认目录 compose/Gallery/baselines
#   bash tools/gallery-baseline.sh <基线目录>                     # 核查指定目录
#   bash tools/gallery-baseline.sh --generate <输出目录>          # 生成基线（DevOps 用）
set -euo pipefail

cd "$(dirname "$0")/.."

if [ -x "$HOME/.dotnet/dotnet" ]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
fi

export RestorePackagesPath="${RestorePackagesPath:-$PWD/artifacts/packages}"

if [[ $# -ge 1 && "$1" == "--generate" ]]; then
  OUT_DIR="${2:-compose/Gallery/baselines}"
  echo "== 触发画廊像素基线生成（DevOps 命令）: $OUT_DIR =="
  mkdir -p "$OUT_DIR"
  exec dotnet run --project compose/Gallery/Gallery.fsproj -- --generate-baselines "$OUT_DIR" --theme both
fi

BASELINE_DIR="${1:-compose/Gallery/baselines}"
echo "== 执行画廊深浅双主题 PNG 像素基线核查（门禁 G3）: $BASELINE_DIR =="

if [[ ! -d "$BASELINE_DIR" ]]; then
  echo "[FAIL MISSING] 基线目录不存在: $BASELINE_DIR" >&2
  echo "门禁失败：基线必须由 DevOps 在实机生成（bash tools/gallery-baseline.sh --generate $BASELINE_DIR），缺基线必须红！" >&2
  exit 1
fi

exec dotnet run --project compose/Gallery/Gallery.fsproj -- --check-baselines "$BASELINE_DIR" --theme both
