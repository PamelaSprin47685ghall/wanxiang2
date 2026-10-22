# 跨平台超时执行（被 tools/smoke_desktop.sh 与 tools/perf_desktop.sh 共用）。
#
# run_with_timeout <秒> <命令...>
#   正常退出：透传被测命令的退出码。
#   超时    ：统一返回 124（与 GNU timeout 的退出码约定一致），并收掉被测命令的整个进程组。
#
# 为什么要自己实现：macOS 自带 bash 3.2 不带 GNU coreutils 的 timeout（未装 coreutils 时
# 连 gtimeout 也没有）；而桌面入口会派生子进程，只杀直接子进程会留下后台进程占端口。
#
# 三条实现路径，按可用性依次回退：
#   1. GNU timeout（Linux、装了 coreutils 的 macOS）
#   2. gtimeout（Homebrew coreutils 的别名）
#   3. python3 subprocess（两平台自带）：开新进程组，超时后先礼后兵地收整组。
run_with_timeout() {
  local secs="$1"; shift
  if command -v timeout >/dev/null 2>&1; then
    timeout -k 10 "$secs" "$@"
  elif command -v gtimeout >/dev/null 2>&1; then
    gtimeout -k 10 "$secs" "$@"
  else
    python3 - "$secs" "$@" <<'PY'
import os
import signal
import subprocess
import sys
import time

secs = float(sys.argv[1])
cmd = sys.argv[2:]

# start_new_session：让被测命令成为新进程组的组长，超时后能整组收掉，
# 不给它的子进程留下继续占端口的余留。
proc = subprocess.Popen(cmd, start_new_session=True)


def kill_group(sig):
    try:
        os.killpg(proc.pid, sig)
    except (ProcessLookupError, PermissionError):
        pass


try:
    sys.exit(proc.wait(timeout=secs))
except subprocess.TimeoutExpired:
    kill_group(signal.SIGTERM)
    # 父进程退出不代表整组退出；组号在 start_new_session 时就是 proc.pid，
    # 不能等父进程退出后再用 getpgid 查询。宽限期内轮询整组而不是只等父进程。
    deadline = time.monotonic() + 10
    while time.monotonic() < deadline:
        proc.poll()
        try:
            os.killpg(proc.pid, 0)
        except ProcessLookupError:
            break
        time.sleep(0.05)
    kill_group(signal.SIGKILL)
    proc.wait()
    sys.exit(124)
PY
  fi
}
