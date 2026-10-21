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
# 预置一条对话（一条用户消息），让冒烟能真的打开对话、右键消息卡、触发弹出层。
# 账本格式与宿主读的一致：RFC 7464 分帧（RS + 正文 + LF），正文是 JsonSeq 的提交外壳
# {"v":1,"id":"<str>","at":"<ISO8601>","events":[…]}，事件是 EventJson 的形状
# {"type":…,"version":1,"data":{…}}（见 libs/JsonSeq/Envelope.fs、libs/Conversations/EventJson.fs）。
mkdir -p "$WORK/data/ledger"
python3 - "$WORK/data/ledger/log.jsonseq" <<'PY'
import json, sys

path = sys.argv[1]
conv = "11111111-2222-3333-4444-555555555555"

def envelope(commit_id, at, events):
    return json.dumps(
        {"v": 1, "id": str(commit_id), "at": at, "events": events},
        ensure_ascii=False, separators=(",", ":"))

created = {
    "type": "conversation.created",
    "version": 1,
    "data": {
        "conversationId": conv,
        "title": "中文冒烟",
        "config": {"provider": "demo", "model": "test-model"},
    },
}
message = {
    "type": "agent-message-recorded",
    "version": 1,
    "data": {
        "conversationId": conv,
        "payload": {"role": "user", "contents": [{"type": "text", "text": "你好，世界"}]},
    },
}

frames = b""
for body in (envelope(1, "2026-10-21T00:00:00.0000000+00:00", [created]),
             envelope(2, "2026-10-21T00:00:01.0000000+00:00", [message])):
    frames += b"\x1e" + body.encode("utf-8") + b"\x0a"

with open(path, "wb") as f:
    f.write(frames)
PY
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
# 一个不联网的假 provider：只为让预置的对话能出现在侧栏（目录按 provider 过滤）。
[providers.demo]
kind = "openai"
baseUrl = "http://127.0.0.1:1/v1"
models = ["test-model"]
defaultModel = "test-model"

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
