#!/usr/bin/env bash
# 独立启动 wanxiang2 桌面入口（不依赖宿主仓库的 start.sh / tools/wanxiang2-feed.sh）。
#
#   ./run-desktop.sh                    # 产包 → 构建 → 启动桌面（与上次一致的阶段自动跳过）
#
# 自动跳过（全量 ./x pack 实测 15 分钟、入口构建实测 2 分半，重复启动不该每次重付）：
#   产包 —— 对打包输入取指纹：libs/、compose/ 下组件目录的源与工程文件（排除 bin/obj、
#            tests/、tools/、*.md——它们不进包），加构建驱动器 x 与全仓构建配置四个文件。与
#            artifacts/pack.stamp 一致且 artifacts/feed 存在 → 跳过；否则 ./x pack 后写回。
#   构建 —— 对 artifacts/feed 包字节与 app/Desktop 入口源码取指纹，与构建回执一致 → 跳过。
#
# 环境变量：
#   WX_SKIP_PACK=1    强制跳过产包（连指纹比对一起跳过；自负 feed 与源码一致的责任）
#   WX_SKIP_BUILD=1   跳过构建，只运行仍匹配当前包源的已有产物
#   WX_ARGS="..."     追加参数，例如 --config /path/to/config.toml --data /path/to/data
set -euo pipefail
cd "$(dirname "$0")"

# 系统 SDK 可能缺运行时或 workload；存在用户级 SDK 时优先用它。
if [ -x "$HOME/.dotnet/dotnet" ]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
fi

ENTRY="app/Desktop"
PROJ=$(find "$ENTRY" -maxdepth 2 \( -name '*.fsproj' -o -name '*.csproj' \) -not -path '*/obj/*' -not -path '*/bin/*' 2>/dev/null | head -1)
if [ -z "$PROJ" ]; then
  echo "未找到桌面入口项目：$ENTRY 下没有 .fsproj/.csproj（入口由 wx2-desktop 提供）" >&2
  exit 1
fi

# 指纹公共输入：构建驱动器 ./x（pack 的实现本身；它变了，产出就可能是新的）+ 全仓构建配置。
# 改这些会同时影响所有包的产出与入口的还原/编译。
build_config_paths() {
  local f
  for f in x Directory.Build.props Directory.Packages.props global.json NuGet.config; do
    if [ -f "$f" ]; then printf '%s\0' "$f"; fi
  done
}

# 打包输入指纹（决定 ./x pack 是否过时）。路径进 sha256sum 输出，改名也算变更。
src_fingerprint() {
  { find libs compose -type f \
      -not -path '*/bin/*' -not -path '*/obj/*' \
      -not -path '*/tests/*' -not -path '*/tools/*' \
      -not -name '*.md' -print0
    build_config_paths
  } | LC_ALL=C sort -z | xargs -0 -r sha256sum | sha256sum | cut -d' ' -f1
}

feed_fingerprint() {
  find artifacts/feed -type f 2>/dev/null | LC_ALL=C sort | xargs -r sha256sum 2>/dev/null | sha256sum | cut -d' ' -f1
}

# 入口构建指纹：桌面工程与入口源文件（构建产物目录除外）+ 构建配置。
app_fingerprint() {
  { find "$ENTRY" -type f -not -path '*/bin/*' -not -path '*/obj/*' -print0
    build_config_paths
  } | LC_ALL=C sort -z | xargs -0 -r sha256sum | sha256sum | cut -d' ' -f1
}

# —— 产包 ——
SRC=$(src_fingerprint)
PACK_STAMP="artifacts/pack.stamp"
NEED_PACK=1
# feed 不存在时即便 WX_SKIP_PACK=1 也照常产包（与老脚本一致）：入口没有包源只能 restore 失败。
if [ ! -d artifacts/feed ]; then
  :
elif [ "${WX_SKIP_PACK:-0}" = "1" ]; then
  NEED_PACK=0
elif [ "$(cat "$PACK_STAMP" 2>/dev/null)" = "$SRC" ]; then
  NEED_PACK=0
  echo "跳过产包（打包输入与上次产包一致；改动后自动重打）…" >&2
fi
if [ "$NEED_PACK" = 1 ]; then
  echo "产包…" >&2
  # 先摘掉回执再打：./x pack 起手就清 artifacts/feed，中途失败（如依赖环）时不能留旧回执
  # 让下一次把残缺 feed 当成「与源码一致」跳过。成功后才写回。
  rm -f "$PACK_STAMP"
  ./x pack
  mkdir -p artifacts
  printf '%s\n' "$SRC" > "$PACK_STAMP"
fi

# —— 构建 ——
FEED=$(feed_fingerprint)
BIN_DIR="$ENTRY/bin/Debug"
BUILD_RECEIPT="$BIN_DIR/wanxiang2-feed.sha256"
BUILD_STATE="$FEED $(app_fingerprint)"
if [ "${WX_SKIP_BUILD:-0}" != "1" ]; then
  if [ "$(cat "$BUILD_RECEIPT" 2>/dev/null)" = "$BUILD_STATE" ]; then
    echo "跳过构建（feed 包字节与入口源码均与上次构建一致）…" >&2
  else
    rm -f "$BUILD_RECEIPT"
    # 干净消费（与 ./x app、tools/smoke_desktop.sh 同一约定）：包必须解到仓库内缓存、
    # 从 artifacts/feed 取。不传 RestorePackagesPath 会走 ~/.nuget/packages——同版本号
    # （0.0.0-local）的旧解包命中缓存后 NuGet 不再重解，入口会对着过期包编译
    # （2026-10-18 实测：旧 AppShell 解包致 37 个 FS0039/FS1129）。
    rm -rf artifacts/packages/wanxiang2.*
    echo "构建桌面入口…" >&2
    dotnet build "$PROJ" -c Debug --verbosity minimal -p:RestorePackagesPath="$PWD/artifacts/packages"
    AFTER=$(feed_fingerprint)
    if [ "$FEED" != "$AFTER" ]; then
      echo "包源在构建期间变化，请重新运行。" >&2
      exit 1
    fi
    printf '%s\n' "$BUILD_STATE" > "$BUILD_RECEIPT"
  fi
elif [ "$(cat "$BUILD_RECEIPT" 2>/dev/null)" != "$BUILD_STATE" ]; then
  echo "现有产物没有当前包源/入口源码的构建回执，请不设置 WX_SKIP_BUILD 重新运行。" >&2
  exit 1
fi

BIN=$(find "$BIN_DIR" -maxdepth 3 -type f -perm -111 ! -name '*.dll' ! -name '*.pdb' ! -name '*.so' ! -name '*.dylib' ! -name '*.a' 2>/dev/null | head -1)
if [ -z "$BIN" ] || [ ! -x "$BIN" ]; then
  echo "未找到桌面可执行文件（$BIN_DIR 下无 apphost；先不带 WX_SKIP_BUILD 构建一次）" >&2
  exit 1
fi

echo "启动 wanxiang2 桌面：$BIN" >&2
# shellcheck disable=SC2086
exec "$BIN" ${WX_ARGS:-}
