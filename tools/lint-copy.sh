#!/usr/bin/env bash
# 界面文案门禁（copy lint）。
#
# 为什么它是脚本而不是测试：
#   「产品文案规则」没有运行时可观察——它约束的是**用户看到的字**，
#   不是任何对象的行为。所以它不该伪装成一条 xunit 用例（假装自己能被
#   `Assert` 证明），而应该是一条独立的、扫描源码的 lint 规则。
#
# 规则来源：docs/ux/a11y-audit.md §6 的术语表与 §3 的对话框约定。
# 三条规则：
#   C1 禁用同义词：设定 / 秘钥 / Api Key / api key / Apikey（统一写作 设置 / 密钥 / API Key）。
#   C2 取消类提示必须写明 Esc：字符串以「取消并关闭」开头时必须含 "Esc"。
#      （对话框约定：取消/关闭类控件的 tooltip 要告诉用户 Esc 可用。）
# C3 中文串里不得出现三个英文句点 "..."（统一用中文省略号 …）。
# C4 PWA 壳（交付给浏览器的静态资源）里不得出现已淘汰的同义词——
#    与界面用同一套术语（旧的 PWA 页面文案扫描，只覆盖这三个文件）。
#
# 扫描范围：libs/ + compose/ + app/ 下的生产 .fs（排除 tests/、tools/、bin/、obj/）。
#   —— 比原先放在 compose/Theme/tests/A11yRegression.fs 里的版本更宽：
#   那两条旧用例只扫 compose + app，看不到住在 libs/ 里的取消类控件，
#   于是「取消按钮的提示文案都写明 Esc」永远在空集合上运行（恒真）。
#
# 排除项（保持诚实，不要扩大）：
#   * 注释行（// 与 ///）不算文案。
#   * tests/ 与 tools/ 下的字符串是测试夹具与工具参数，不是用户文案。
#   * bin/ 与 obj/ 是生成物。
#   * C2 只认「取消并关闭」这个前缀——它是取消类控件提示的固定写法；
#     别的文案（「取消全选」「取消折行」等）不是对话框取消键，不该被这条规则管。
#
# 用法：tools/lint-copy.sh
# 退出码：0 通过；1 有违规（逐条打印 file:line）；2 前置不满足。
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"

if ! command -v python3 >/dev/null 2>&1; then
  echo "FAIL 前置：找不到 python3" >&2
  exit 2
fi

python3 - <<'PY'
import os
import re
import sys

ROOTS = ("libs", "compose", "app")
EXCLUDED_DIRS = {"tests", "tools", "bin", "obj"}

# 中文串字面量："…"，允许转义引号；内部至少含一个汉字。
LIT = re.compile(r'"(?:[^"\\\n]|\\.)*[一-鿿](?:[^"\\\n]|\\.)*"')

# C1：禁用同义词 → 统一写法（docs/ux/a11y-audit.md §6）。
BANNED = [
    ("设定", "统一用「设置」"),
    ("秘钥", "统一用「密钥」"),
    ("Api Key", "统一写作「API Key」"),
    ("api key", "统一写作「API Key」"),
    ("Apikey", "统一写作「API Key」"),
]

# C2：取消类提示的前缀（对话框约定，见 docs/ux/a11y-audit.md §3）。
CANCEL_PREFIX = "取消并关闭"

# C4：PWA 壳的用户可见文字与界面同一套术语。
PWA_ASSETS = [
    "app/Pwa/wwwroot/index.html",
    "app/Pwa/wwwroot/main.js",
    "app/Pwa/wwwroot/manifest.webmanifest",
]
PWA_BANNED = ["会话", "服务器", "服务端", "服务商", "提供商", "供应商", "提供方"]


def production_files():
    for root in ROOTS:
        for dirpath, dirnames, filenames in os.walk(root):
            parts = dirpath.split(os.sep)
            if any(p in EXCLUDED_DIRS for p in parts):
                continue
            for name in sorted(filenames):
                if name.endswith(".fs"):
                    yield os.path.join(dirpath, name)


def strip_comment(line):
    """去掉行注释。F# 的 /// 也在 // 之内。"""
    cut = line.find("//")
    return line if cut < 0 else line[:cut]


violations = []
scanned = 0
literals = 0

for path in production_files():
    scanned += 1
    try:
        lines = open(path, encoding="utf-8").read().split("\n")
    except OSError:
        continue
    for lineno, raw in enumerate(lines, 1):
        code = strip_comment(raw)
        if not code.strip():
            continue
        for m in LIT.finditer(code):
            s = m.group(0)
            literals += 1
            # C1
            for word, hint in BANNED:
                if word in s:
                    violations.append(
                        (path, lineno, f"含「{word}」（{hint}）：{s}")
                    )
            # C2
            if s.startswith(f'"{CANCEL_PREFIX}') and "Esc" not in s:
                violations.append(
                    (path, lineno, f"取消类提示缺少 Esc 说明：{s}")
                )
            # C3
            if "..." in s:
                violations.append(
                    (path, lineno, f"中文串里用了三个英文句点（应为「…」）：{s}")
                )

print(f"扫描 {scanned} 个生产 .fs 文件（libs/ + compose/ + app/，排除 tests/tools/bin/obj），"
      f"检查 {literals} 个中文串字面量")

# ---- C4：PWA 壳交付物的术语 ----
pwa_scanned = 0
for asset in PWA_ASSETS:
    if not os.path.exists(asset):
        continue
    pwa_scanned += 1
    for lineno, raw in enumerate(open(asset, encoding="utf-8").read().split("\n"), 1):
        for word in PWA_BANNED:
            if word in raw:
                violations.append(
                    (asset, lineno, f"PWA 壳含已淘汰术语「{word}」：{raw.strip()[:120]}")
                )
print(f"另扫 {pwa_scanned} 个 PWA 壳交付物（index.html / main.js / manifest.webmanifest）")

if violations:
    for path, lineno, message in violations:
        print(f"  FAIL {path}:{lineno}: {message}")
    print(f"结果：{len(violations)} 项文案违规")
    sys.exit(1)

print("结果：界面文案全部通过")
PY
