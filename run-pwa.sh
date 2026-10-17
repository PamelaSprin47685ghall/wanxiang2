#!/usr/bin/env bash
# 独立构建并启动 wanxiang2 PWA 入口（不依赖宿主仓库的 start.sh）。
#
#   ./run-pwa.sh                        # 产包 → 构建 PWA → 静态托管 AppBundle
#
# 环境变量：
#   WX_SKIP_PACK=1    跳过产包（artifacts/feed 已存在且与当前源码一致时）
#   WX_SKIP_BUILD=1   跳过构建，只托管已有 AppBundle
#   WX_PORT=8000      静态服务器端口（默认 8000）
set -euo pipefail
cd "$(dirname "$0")"

if [ -x "$HOME/.dotnet/dotnet" ]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
fi

ENTRY="app/Pwa"
PROJ=$(find "$ENTRY" -maxdepth 2 \( -name '*.fsproj' -o -name '*.csproj' \) -not -path '*/obj/*' -not -path '*/bin/*' 2>/dev/null | head -1)
if [ -z "$PROJ" ]; then
  echo "未找到 PWA 入口项目：$ENTRY 下没有 .fsproj/.csproj（入口由 wx2-pwa 提供）" >&2
  exit 1
fi

# 产包：wanxiang2 交付包源（./x pack -> artifacts/feed）。PWA 入口经 Wanxiang2.* 0.0.0-local 包消费。
if [ "${WX_SKIP_PACK:-0}" != "1" ] || [ ! -d artifacts/feed ]; then
  echo "产包…" >&2
  ./x pack
fi

feed_fingerprint() {
  find artifacts/feed -type f 2>/dev/null | sort | xargs sha256sum 2>/dev/null | sha256sum | cut -d' ' -f1
}
FEED=$(feed_fingerprint)

BIN_DIR="app/Pwa/bin/Debug"
BUILD_RECEIPT="$BIN_DIR/wanxiang2-feed.sha256"
if [ "${WX_SKIP_BUILD:-0}" != "1" ]; then
  rm -f "$BUILD_RECEIPT"
  echo "构建 PWA 入口…（需要 wasm-tools workload：dotnet workload install wasm-tools）" >&2
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

BUNDLE=$(find "$BIN_DIR" -type d -name AppBundle 2>/dev/null | head -1)
if [ -z "$BUNDLE" ]; then
  echo "未找到 AppBundle（PWA 未构建成功，或缺少 wasm-tools workload）" >&2
  exit 1
fi

PORT="${WX_PORT:-8000}"
echo "PWA 已构建：$BUNDLE" >&2
echo "访问 http://localhost:$PORT （Ctrl-C 退出）" >&2
exec python3 -m http.server "$PORT" --directory "$BUNDLE"
