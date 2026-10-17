#!/usr/bin/env bash
# 决策 290（旧仓库）：桌面冷启动基准，迁自旧仓库 tools/perf_desktop.sh，改指 wanxiang2 桌面产物。
# 把种子数据目录复制到临时目录，启动 主机+桌面客户端，读取 StartupTrace 打点（firstFrame / listReady），
# 跑 N 次取中位数。可执行文件与 run-desktop.sh 同源：app/Desktop/bin/Debug 下的 apphost。
# 用法：tools/perf_desktop.sh <seedDataDir> [runs=3] [port=18914]；先 ./run-desktop.sh 构建一次。
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
SEED="$1"; RUNS="${2:-3}"; PORT="${3:-18914}"
BIN=$(find "$ROOT/app/Desktop/bin/Debug" -maxdepth 3 -type f -perm -111 ! -name '*.dll' ! -name '*.pdb' ! -name '*.so' ! -name '*.dylib' ! -name '*.a' 2>/dev/null | head -1)
[ -n "$BIN" ] && [ -x "$BIN" ] || { echo "未找到桌面可执行文件（app/Desktop/bin/Debug 下无 apphost；先 ./run-desktop.sh 构建一次）" >&2; exit 1; }
export DISPLAY="${DISPLAY:-:2}"
for i in $(seq 1 "$RUNS"); do
  T=$(mktemp -d /tmp/wxperf-desk-XXXX)
  cp -r "$SEED" "$T/data"
  printf 'configVersion = 2\n[network]\nlisten = "127.0.0.1:%s"\n' "$PORT" > "$T/config.toml"
  WANXIANG_HOME="$T" WANXIANG_STARTUP_TRACE=exit timeout 90 "$BIN" --config "$T/config.toml" --data "$T/data" --server --client --pwa=false 2>"$T/err.log" >/dev/null
  echo "run $i: $(grep -o 'startup\.[a-zA-Z]* uptime_ms=[0-9]*' "$T/err.log" | tr '\n' ' ')"
  rm -rf "$T"
done
