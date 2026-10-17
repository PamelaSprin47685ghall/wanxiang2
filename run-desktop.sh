#!/usr/bin/env bash
# 独立启动 wanxiang2 桌面入口（不依赖宿主仓库的 start.sh / tools/wanxiang2-feed.sh）。
#
#   ./run-desktop.sh                    # 产包 → 构建 → 启动桌面
#
# 环境变量：
#   WX_SKIP_PACK=1    跳过产包（artifacts/feed 已存在且与当前源码一致时）
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

# 产包：wanxiang2 交付包源（./x pack -> artifacts/feed）。桌面入口经 Wanxiang2.* 0.0.0-local 包消费。
if [ "${WX_SKIP_PACK:-0}" != "1" ] || [ ! -d artifacts/feed ]; then
  echo "产包…" >&2
  ./x pack
fi

feed_fingerprint() {
  find artifacts/feed -type f 2>/dev/null | sort | xargs sha256sum 2>/dev/null | sha256sum | cut -d' ' -f1
}
FEED=$(feed_fingerprint)

BIN_DIR="app/Desktop/bin/Debug"
BUILD_RECEIPT="$BIN_DIR/wanxiang2-feed.sha256"
if [ "${WX_SKIP_BUILD:-0}" != "1" ]; then
  rm -f "$BUILD_RECEIPT"
  echo "构建桌面入口…" >&2
  dotnet build "$PROJ" -c Debug --verbosity minimal
  AFTER=$(feed_fingerprint)
  if [ "$FEED" != "$AFTER" ]; then
    echo "包源在构建期间变化，请重新运行。" >&2
    exit 1
  fi
  printf '%s\n' "$FEED" > "$BUILD_RECEIPT"
elif [ "$(cat "$BUILD_RECEIPT" 2>/dev/null)" != "$FEED" ]; then
  echo "现有产物没有当前包源的构建回执，请不设置 WX_SKIP_BUILD 重新运行。" >&2
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
