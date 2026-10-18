#!/usr/bin/env bash
# 旧仓库决策 411：发布产物冒烟，迁自旧仓库 tools/smoke_published.sh，改指 wanxiang2 发布产物。
# 启动「发布目录」里的主机（--pwa，无桌面 UI、显式空闲端口、临时配置/数据目录），
# 跑 tools/smoke_published_pwa.js，结束时一定杀掉主机。
# 发布目录传 ./publish.sh 的展开目录 artifacts/publish/<rid>（dist/ 里是压缩包，需先解压）。
# 需要 Node 依赖：仓库根先 `npm ci`（装 puppeteer-core；Chrome 默认 /usr/bin/google-chrome，可用 CHROME_PATH 覆盖）。
# 用法：tools/smoke_published.sh <publish 目录> [port]
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
PUB=$(cd "$1" && pwd)
PORT="${2:-$(python3 -c 'import socket;s=socket.socket();s.bind(("127.0.0.1",0));print(s.getsockname()[1])')}"
WORK=$(mktemp -d /tmp/wanxiang2-smoke-XXXX)
mkdir -p "$WORK/data"
TOKEN="smoke-$(python3 -c 'import secrets;print(secrets.token_hex(16))')"
HASH=$(printf '%s' "$TOKEN" | { sha256sum 2>/dev/null || shasum -a 256; } | cut -d' ' -f1)
INSTANCE=$(python3 -c 'import uuid;print(uuid.uuid4())')
cat > "$WORK/config.toml" <<TOML
configVersion = 2
instanceId = "$INSTANCE"
[runtime]
server = true
client = false
pwa = true
fix = false
[network]
listen = "127.0.0.1:$PORT"

[[auth.clients]]
tokenHash = "$HASH"
name = "PWA"
createdAt = "2026-01-01T00:00:00+00:00"
revoked = false
TOML
BIN=$(find "$PUB" -maxdepth 1 -type f -perm -111 ! -name '*.dll' ! -name '*.pdb' ! -name '*.so' ! -name '*.dylib' ! -name '*.a' 2>/dev/null | head -1)
if [ -z "$BIN" ] || [ ! -x "$BIN" ]; then
    echo "发布目录里没有可执行文件：$PUB（应传 artifacts/publish/<rid>；dist/ 内是压缩包需先解压）" >&2
    exit 1
fi
WANXIANG_NO_URL_SCHEME=1 "$BIN" --config "$WORK/config.toml" --data "$WORK/data" --client=false --pwa > "$WORK/host.log" 2>&1 &
HOST=$!
trap 'kill $HOST 2>/dev/null || true; wait $HOST 2>/dev/null || true' EXIT
for _ in $(seq 1 120); do
    curl -fsS "http://127.0.0.1:$PORT/" -o /dev/null 2>/dev/null && break
    kill -0 $HOST 2>/dev/null || { echo "host exited early:"; cat "$WORK/host.log"; exit 1; }
    sleep 0.5
done
# wx2 发布目录可能内嵌 pwa/ 子目录（publish.sh 的 PublishPwaBundle 等价物），两种布局都认。
if ! curl -fsS -o /dev/null "http://127.0.0.1:$PORT/_framework/avalonia.js" && ! curl -fsS -o /dev/null "http://127.0.0.1:$PORT/pwa/_framework/avalonia.js"; then
    echo "FAIL avalonia.js not served"; cat "$WORK/host.log"; exit 1
fi
echo "host up on $PORT (pid $HOST)"
rc=0
# 预置一条客户端凭据（上面配置里的哈希）交给 PWA：脚本用它把浏览器接进已认证状态，
# 这样草稿往返这类「登录后才有的界面」也能真跑（见 smoke_published_pwa.js 的第二段）。
WX_SMOKE_TOKEN="$TOKEN" WX_SMOKE_INSTANCE="$INSTANCE" \
    node "$ROOT/tools/smoke_published_pwa.js" --base "http://127.0.0.1:$PORT" --out "${SMOKE_OUT:-$WORK/shots}" || rc=$?
[ $rc -eq 0 ] || { echo "--- host.log (tail)"; tail -40 "$WORK/host.log"; }
exit $rc
