#!/usr/bin/env bash
# 桌面产品入口冒烟：证明 app/Desktop 的桌面入口真的能启动、打出启动里程碑（决策 290）、
# 并且干净退出——不是「能编译」而已。与 run-desktop.sh（构建/启动）、tools/perf_desktop.sh（采样打点）
# 同一套机制：app/Desktop/bin/Debug 下的 apphost + StartupTrace 的 stderr 打点。
#
# 做什么：
#   1. 前置：artifacts/feed 存在（缺则提示 ./x pack 并退 2）；存在用户级 SDK 时优先用它，
#      以 Debug 构建 app/Desktop/Wanxiang.App.fsproj，RestorePackagesPath 指向 artifacts/packages。
#   2. 用全新的临时 home + data 目录跑一次 apphost（显式空闲回环端口，
#      --config / --data / --server --client --pwa=false，WANXIANG_STARTUP_TRACE=exit，WANXIANG_NO_URL_SCHEME=1），
#      整段由 timeout 兜底：超时被杀就是失败。
#   3. 断言并打印：进程退出码 0；stderr 有 startup.firstFrame 与 startup.listReady；
#      没有未处理异常 / 异常栈行；数据目录留下预期的存储文件且写者已释放目录；成功时临时目录被删掉。
#   4. 任一断言失败：打印可读的 ok/FAIL 清单与捕获的 stderr 尾部，保留现场，非零退出。
#
# 用法：tools/smoke_desktop.sh（无参数）
#
# 环境变量：
#   WX_SMOKE_TIMEOUT  单次运行的超时秒数（默认 180；到期被杀即失败）
#   WX_SMOKE_PORT     指定回环端口（默认自动挑一个空闲端口）
#   WX_SKIP_BUILD=1   跳过构建，直接用现有 app/Desktop/bin/Debug 产物（迭代用；默认总是构建）
#
# 退出码：0 全部断言通过；1 运行或断言失败；2 前置不满足（feed 缺失 / 无 SDK / 构建失败）
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"

# 系统 SDK 可能缺运行时或 workload；存在用户级 SDK 时优先用它（与 run-desktop.sh 一致）。
if [ -x "$HOME/.dotnet/dotnet" ]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
fi

TIMEOUT_SECS="${WX_SMOKE_TIMEOUT:-180}"
PROJ="app/Desktop/Wanxiang.App.fsproj"
BIN_DIR="app/Desktop/bin/Debug"
FEED="artifacts/feed"

[ -d "$FEED" ] || { echo "FAIL 前置：交付包源 $FEED 不存在；先运行 ./x pack" >&2; exit 2; }
command -v dotnet >/dev/null 2>&1 || { echo "FAIL 前置：找不到 dotnet（$HOME/.dotnet 下也没有）" >&2; exit 2; }

feed_fingerprint() {
  find "$FEED" -type f 2>/dev/null | sort | xargs sha256sum 2>/dev/null | sha256sum | cut -d' ' -f1
}

if [ "${WX_SKIP_BUILD:-0}" != "1" ]; then
  BEFORE=$(feed_fingerprint)
  echo "构建桌面入口：$PROJ（Debug，包源 $FEED）" >&2
  if ! dotnet build "$PROJ" -c Debug --verbosity minimal -p:RestorePackagesPath="$PWD/artifacts/packages"; then
    echo "FAIL 前置：桌面入口构建失败（feed 缺包或包过期时先 ./x pack 再重跑）" >&2
    exit 2
  fi
  AFTER=$(feed_fingerprint)
  [ "$BEFORE" = "$AFTER" ] || { echo "FAIL 前置：构建期间包源发生变化（$FEED），先 ./x pack 再重跑" >&2; exit 2; }
fi

APP=$(find "$BIN_DIR" -maxdepth 3 -type f -perm -111 ! -name '*.dll' ! -name '*.pdb' ! -name '*.so' ! -name '*.dylib' ! -name '*.a' 2>/dev/null | head -1)
[ -n "$APP" ] && [ -x "$APP" ] || { echo "FAIL 前置：$BIN_DIR 下没有桌面 apphost（不设 WX_SKIP_BUILD 构建一次）" >&2; exit 2; }

# 空闲端口：连得上就说明被占用（纯 bash，不依赖 ss/netstat/python）。
port_in_use() { (exec 3<>"/dev/tcp/127.0.0.1/$1") 2>/dev/null; }
pick_port() {
  local i p
  for ((i = 0; i < 100; i++)); do
    p=$((20000 + RANDOM % 40000))
    if ! port_in_use "$p"; then printf '%s\n' "$p"; return 0; fi
  done
  return 1
}

PORT="${WX_SMOKE_PORT:-$(pick_port)}" || { echo "FAIL：挑不到空闲回环端口（可用 WX_SMOKE_PORT 指定）" >&2; exit 1; }
export DISPLAY="${DISPLAY:-:0}"

T=$(mktemp -d "${TMPDIR:-/tmp}/wanxiang2-smoke-desk-XXXXXX")
HOME_DIR="$T/home"
DATA_DIR="$T/data"
CONFIG="$HOME_DIR/config.toml"
ERR="$T/stderr.log"
OUT="$T/stdout.log"
mkdir -p "$HOME_DIR" "$DATA_DIR"
printf 'configVersion = 2\n[network]\nlisten = "127.0.0.1:%s"\n' "$PORT" > "$CONFIG"

echo "运行：$APP --config $CONFIG --data $DATA_DIR --server --client --pwa=false（端口 $PORT，超时 ${TIMEOUT_SECS}s）"
rc=0
WANXIANG_HOME="$HOME_DIR" WANXIANG_STARTUP_TRACE=exit WANXIANG_NO_URL_SCHEME=1 \
  timeout -k 10 "$TIMEOUT_SECS" "$APP" \
    --config "$CONFIG" --data "$DATA_DIR" --server --client --pwa=false \
    >"$OUT" 2>"$ERR" || rc=$?

FAILED=0
pass() { printf '  ok   %s\n' "$1"; }
fail() { printf '  FAIL %s\n' "$1"; FAILED=$((FAILED + 1)); }

MARKS=$(grep -o 'startup\.[a-zA-Z]* uptime_ms=[0-9]*' "$ERR" 2>/dev/null || true)
echo
echo "断言（stderr：$ERR）："
printf '  启动打点：%s\n' "$(printf '%s' "$MARKS" | tr '\n' ' ')"

# 1) 退出码：timeout 到期（124/137）算失败，不是「反正退出了」。
if [ "$rc" -eq 0 ]; then
  pass "进程退出码 0"
elif [ "$rc" -eq 124 ] || [ "$rc" -eq 137 ]; then
  fail "进程退出码 $rc：timeout ${TIMEOUT_SECS}s 到期被杀（启动或退出挂住）"
else
  fail "进程退出码 $rc（期望 0）"
fi

# 2) 启动里程碑：firstFrame 证明窗口真的画出来了；同进程主机在跑，listReady 必须到。
for mark in firstFrame listReady; do
  if printf '%s\n' "$MARKS" | grep -q "^startup\.$mark "; then
    pass "stderr 有 startup.$mark"
  else
    fail "stderr 缺 startup.$mark"
  fi
done

# 3) 未处理异常 / 异常栈行：命中就打印前几行（可读的 diff）。
FATAL_RE='Unhandled exception|^wanxiang: |Exception:|(^|\\n)[[:space:]]*at [A-Za-z_][A-Za-z0-9_.]*\('
FATAL=$(grep -nE "$FATAL_RE" "$ERR" 2>/dev/null | head -5 || true)
if [ -z "$FATAL" ]; then
  pass "stderr 无未处理异常 / 异常栈行"
else
  fail "stderr 出现异常行："
  printf '%s\n' "$FATAL" | while IFS= read -r line; do printf '       line %s: %s\n' "${line%%:*}" "${line#*:}"; done
fi

# 4) 存储文件：干净打开过存储（log.jsonseq 在）、锁文件在，且写者已释放目录。
MISSING=""
for f in "ledger/log.jsonseq" "ledger/.lock" "ledger/.lock.pid"; do
  [ -f "$DATA_DIR/$f" ] || MISSING="$MISSING $f"
done
if [ -z "$MISSING" ]; then
  pass "数据目录有存储文件 ledger/log.jsonseq ledger/.lock ledger/.lock.pid"
else
  fail "数据目录缺存储文件：${MISSING# }"
fi
if [ -d "$DATA_DIR/attachments" ]; then
  pass "数据目录有 attachments/"
else
  fail "数据目录缺 attachments/"
fi
if [ -f "$DATA_DIR/ledger/.lock.pid" ]; then
  LOCK_PID=$(cat "$DATA_DIR/ledger/.lock.pid" 2>/dev/null || true)
  if [ -n "$LOCK_PID" ] && kill -0 "$LOCK_PID" 2>/dev/null; then
    fail "数据目录锁未释放（.lock.pid $LOCK_PID 仍是活进程）"
  else
    pass "数据目录锁已释放（.lock.pid ${LOCK_PID:-?} 不是活进程）"
  fi
fi

# 5) 成功才清理临时目录；清理本身就是断言。
if [ "$FAILED" -eq 0 ]; then
  rm -rf "$T"
  if [ -d "$T" ]; then fail "临时目录未删除：$T"; else pass "临时目录已删除（$T）"; fi
fi

echo
if [ "$FAILED" -ne 0 ]; then
  echo "结果：$FAILED 项断言失败"
  echo "--- stderr 尾部（最多 40 行：$ERR）"
  tail -n 40 "$ERR" 2>/dev/null || true
  if [ -s "$OUT" ]; then
    echo "--- stdout 尾部（最多 20 行：$OUT）"
    tail -n 20 "$OUT" || true
  fi
  echo "临时目录保留：$T（config $CONFIG，data $DATA_DIR）"
  exit 1
fi
echo "结果：全部断言通过（$(printf '%s' "$MARKS" | tr '\n' ' ')）"
exit 0
