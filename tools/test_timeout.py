"""验证超时工具的退出码与进程组回收，不依赖本机 coreutils。"""
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time
import unittest

ROOT = Path(__file__).resolve().parent.parent


class TimeoutTests(unittest.TestCase):
    def run_fallback(self, code, seconds="0.3"):
        with tempfile.TemporaryDirectory(prefix="wx-timeout-") as folder:
            Path(folder, "python3").symlink_to(sys.executable)
            return subprocess.run(
                ["/bin/bash", "-c",
                 '. tools/timeout.sh; run_with_timeout "$1" "$2" -c "$3"',
                 "probe", seconds, sys.executable, code],
                cwd=ROOT, env={**os.environ, "PATH": folder},
                capture_output=True, text=True, timeout=20,
            )

    def test_exit_code(self):
        self.assertEqual(self.run_fallback("import sys; sys.exit(7)", "2").returncode, 7)

    def test_timeout_reaps_term_resistant_child_after_parent_exits(self):
        child = "import signal,time; signal.signal(signal.SIGTERM,signal.SIG_IGN); print('ready',flush=True); time.sleep(60)"
        parent = (
            "import subprocess,sys,time; "
            f"p=subprocess.Popen([sys.executable,'-c',{child!r}],stdout=subprocess.PIPE,stderr=subprocess.DEVNULL,text=True); "
            "p.stdout.readline(); print(p.pid,flush=True); time.sleep(60)"
        )
        result = self.run_fallback(parent)
        pid = int(result.stdout.strip())
        try:
            self.assertEqual(result.returncode, 124, result.stderr)
            # Linux 上孤儿可能暂留僵尸；僵尸已经不再运行，不占端口。
            for _ in range(100):
                state = subprocess.run(["ps", "-o", "stat=", "-p", str(pid)],
                                       capture_output=True, text=True).stdout.strip()
                if not state or state.startswith("Z"):
                    break
                time.sleep(0.02)
            self.assertTrue(not state or state.startswith("Z"), f"子进程 {pid} 仍运行: {state}")
        finally:
            try:
                os.kill(pid, signal.SIGKILL)
            except ProcessLookupError:
                pass


if __name__ == "__main__":
    unittest.main()
