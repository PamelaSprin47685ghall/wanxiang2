#!/usr/bin/env bash
# 组件画廊（component gallery，G2）的命令行外壳。
#
# 画廊是一个组合者（compose/Gallery）：它把每个控件库的状态矩阵显式列出来，
# 每种状态渲染成一个可视条目。状态全部由 Workspace.init() + Workspace.update 的消息驱动，
# 再经 Workspace.project 投影，不手搓视图状态记录（见 compose/Gallery/AGENTS.md《状态来源》）。
#
# 用法：
#   bash tools/gallery.sh --list                                                      列出全部条目（名字 / 控件库 / 说明）
#   bash tools/gallery.sh --render <条目名> <输出.png> [--theme <light|dark>]          把一个状态渲染成 PNG（支持深浅主题）
#   bash tools/gallery.sh --check-baselines <基线目录> [--theme <light|dark|both>]     核对深浅主题像素基线门禁（G3）
#   bash tools/gallery.sh --generate-baselines <输出目录> [--theme <light|dark|both>]  生成深浅主题像素基线（DevOps 用）
#   bash tools/gallery.sh --a11y-audit                                                全画廊真实控件树无障碍遍历审查（G5）
#   bash tools/gallery.sh --window                                                    开交互窗口（开发用法）
#
# 渲染在 Avalonia headless（Skia）下进行，不连主机、不读本机文件、不写仓库里的任何东西。
# 未知条目名或基线核对失败时以非零码退出。
set -euo pipefail

cd "$(dirname "$0")/.."

# 全仓统一 SDK（与 ./x 同口径）：系统 SDK 缺共享框架与 wasm-tools。
if [ -x "$HOME/.dotnet/dotnet" ]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
fi

if [[ $# -eq 0 ]]; then
  echo "用法：" >&2
  echo "  bash tools/gallery.sh --list" >&2
  echo "  bash tools/gallery.sh --render <条目名> <输出.png> [--theme <light|dark>]" >&2
  echo "  bash tools/gallery.sh --check-baselines <基线目录> [--theme <light|dark|both>]" >&2
  echo "  bash tools/gallery.sh --generate-baselines <输出目录> [--theme <light|dark|both>]" >&2
  echo "  bash tools/gallery.sh --a11y-audit" >&2
  echo "  bash tools/gallery.sh --window" >&2
  exit 2
fi

# 隔离的包缓存：画廊迭代不碰 artifacts/packages（那是 ./x pack / ./x compose 的缓存，
# 同一时刻只允许一个写者）。
export RestorePackagesPath="$PWD/artifacts/gallery-cache"

exec dotnet run --project compose/Gallery/Gallery.fsproj -- "$@"
