#!/usr/bin/env bash
# 决策 290（旧仓库）：PWA 首访 / 二次加载耗时 + 「真正到达主机的请求数与字节数」，迁自旧仓库 tools/perf_pwa_count.sh。
# 目标为 wanxiang2 PWA：./run-pwa.sh 构建后的静态服务（或任一可达 base）。
# 用法：tools/perf_pwa_count.sh <主机 base，如 http://127.0.0.1:8000> [proxyPort=18913]
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
TARGET="$1"; PORT="${2:-18913}"
LOG=$(mktemp "${TMPDIR:-/tmp}/wxperf-proxy.XXXXXX")
node "$ROOT/tools/throttle_proxy.js" --listen "$PORT" --target "$TARGET" --rtt 0 --mbps 100000 > "$LOG" 2>&1 &
PROXY=$!
sleep 1
NODE_PATH="${NODE_PATH:-/workspace/qa-node/node_modules}" node "$ROOT/tools/perf_pwa.js" --base "http://127.0.0.1:$PORT" --runs 1
kill "$PROXY" 2>/dev/null
awk '/__perf_mark_cached/{phase=1; next} /__perf_mark_end/{phase=2; next} /^REQ/{p=phase+0; n[p]++; b[p]+=$5} END{printf "host-requests cold=%d (%.1f MB)  cached=%d (%.1f MB)\n", n[0], b[0]/1048576, n[1], b[1]/1048576}' "$LOG"
rm -f "$LOG"
