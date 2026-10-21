#!/usr/bin/env bash
# 断言卫生门禁：把「测试恒真 / 测试空转」变成机器可查的红灯。
#
# 为什么有它：2026-10-21 的审计发现，本仓库有过一批「无论行为如何都通过」的断言
# （`Assert.True(true)`、跨行的 `| _ -> false || true`、只有兜底分支的旅程断言、
# 没有任何断言的 `[<Fact>]`）。它们让「测试全绿」失去意义。本脚本把这类形状
# 变成 CI 里的硬失败。
#
# 三条规则（只扫 `libs/*/tests` 与 `compose/*/tests` 的 .fs 文件）：
#   R1 恒真断言：`Assert.True(true)` / `Assert.False(false)` /
#      `Assert.Equal(true, true)` / `Assert.True()`。
#   R2 恒真谓词：任一 `Assert.*` 的参数里出现 `|| true` 或 `true ||`
#      （谓词被短路成恒真——原始缺陷就是这个形状）。
#   R3 空转的事实：某个 `[<Fact>]` 的函数体里既没有断言调用，也没有 `failwith`，
#      也没有调用本文件里「会断言的辅助函数」（可传递地）。
#
# 实现方式：先用词法扫描把**注释与字符串**剥掉（保留行列），
# 再在代码骨架上做括号配对解析。
#   * 剥注释支持 F# 的 `(* ... *)` 块注释，**含嵌套**。
#   * 剥字符串支持 `"..."`、`$"..."`、`"""..."""`、`@"..."` 与字符字面量。
#   * 因此注释里写 `// Assert.True(true)` 不会被误报，字符串里的 `|| true` 也不会。
#   * R1/R2 用**平衡括号**取参数，所以跨行的
#     `Assert.Contains(xs, fun e -> match e with | _ -> false || true)` 认得。
#   * R3 的事实体到**下一个顶层声明**为止（不是「下一个 [<Fact>]」），
#     辅助函数按本文件的顶层 `let` 界定，并做传递闭包；没有 60 行之类的截断。
#
# 没有 F# 解析框架可用，所以这是词法层的近似：它能可靠识别上面三类形状，
# 但**不能**判定一段代码在语义上是否「有意义」——那仍然靠评审与行为测试。
#
# 用法：tools/assert-hygiene.sh
# 退出码：0 全部通过；1 有违规（逐条打印 file:line）；2 前置不满足。
set -uo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"

if ! command -v python3 >/dev/null 2>&1; then
  echo "FAIL 前置：找不到 python3" >&2
  exit 2
fi

python3 - "$ROOT" <<'PY'
import os
import re
import sys

ROOT = sys.argv[1]
os.chdir(ROOT)

# ---- 词法扫描与断言解析（内联，避免额外的文件依赖） ----

def strip_code(text):
    """剥掉注释与字符串，保留行列（返回与 text 等长的代码骨架）。"""
    chars = list(text)
    errors = []
    i, n = 0, len(chars)
    line_starts = [0]
    for idx, c in enumerate(chars):
        if c == "\n":
            line_starts.append(idx + 1)

    def line_of(pos):
        lo, hi = 0, len(line_starts) - 1
        while lo < hi:
            mid = (lo + hi + 1) // 2
            if line_starts[mid] <= pos:
                lo = mid
            else:
                hi = mid - 1
        return lo + 1

    def blank(a, b):
        for k in range(a, b):
            if chars[k] != "\n":
                chars[k] = " "

    while i < n:
        c = chars[i]
        if c == "/" and i + 1 < n and chars[i + 1] == "/":
            j = i
            while j < n and chars[j] != "\n":
                j += 1
            blank(i, j); i = j; continue
        if c == "(" and i + 1 < n and chars[i + 1] == "*":
            depth, j = 0, i
            while j < n:
                if chars[j] == "(" and j + 1 < n and chars[j + 1] == "*":
                    depth += 1; j += 2; continue
                if chars[j] == "*" and j + 1 < n and chars[j + 1] == ")":
                    depth -= 1; j += 2
                    if depth == 0:
                        break
                    continue
                j += 1
            if depth != 0:
                errors.append((line_of(i), "块注释未闭合"))
            blank(i, j); i = j; continue
        if c == '"' and text[i:i + 3] == '"""':
            j = i + 3
            while j < n:
                if text[j:j + 3] == '"""':
                    j += 3; break
                j += 1
            else:
                errors.append((line_of(i), "三引号字符串未闭合"))
            blank(i, j); i = j; continue
        if c == "@" and i + 1 < n and chars[i + 1] == '"':
            j = i + 2
            while j < n:
                if chars[j] == '"':
                    if j + 1 < n and chars[j + 1] == '"':
                        j += 2; continue
                    j += 1; break
                j += 1
            else:
                errors.append((line_of(i), "逐字字符串未闭合"))
            blank(i, j); i = j; continue
        if c == '"':
            # F# 的普通字符串**允许跨行**（与 C# 不同），所以只有到文件尾才算未闭合。
            j = i + 1
            while j < n:
                if chars[j] == "\\":
                    j += 2; continue
                if chars[j] == '"':
                    j += 1; break
                j += 1
            else:
                errors.append((line_of(i), "字符串未闭合"))
            blank(i, j); i = j; continue
        if c == "'":
            if i + 2 < n and chars[i + 1] != "\\" and chars[i + 2] == "'":
                blank(i, i + 3); i += 3; continue
            if i + 1 < n and chars[i + 1] == "\\" and i + 2 < n and chars[i + 2] == "'":
                blank(i, i + 3); i += 3; continue
        i += 1
    return "".join(chars), errors


ASSERT_CALL = re.compile(r"\bAssert\s*\.\s*([A-Za-z][A-Za-z0-9_]*)")
CONST_TRUE = re.compile(r"^\s*true\s*$")
CONST_FALSE = re.compile(r"^\s*false\s*$")
SELF_EQ = re.compile(r"^\s*(true|false)\s*,\s*(true|false)\s*$")


def match_paren(code, open_idx):
    depth, i, n = 0, open_idx, len(code)
    while i < n:
        if code[i] == "(":
            depth += 1
        elif code[i] == ")":
            depth -= 1
            if depth == 0:
                return i
        i += 1
    return -1


def find_assert_calls(code):
    out = []
    for m in ASSERT_CALL.finditer(code):
        name = m.group(1)
        j = m.end()
        while j < len(code) and code[j] in " \t":
            j += 1
        if j >= len(code) or code[j] != "(":
            continue
        close = match_paren(code, j)
        if close < 0:
            continue
        out.append((name, code[j + 1:close], m.start()))
    return out


def check_r1_r2(code):
    hits = []
    for name, args, start in find_assert_calls(code):
        line = code.count("\n", 0, start) + 1
        if name == "True" and CONST_TRUE.match(args):
            hits.append((line, "R1", f"Assert.True(true)：结论与被测行为无关"))
        if name == "False" and CONST_FALSE.match(args):
            hits.append((line, "R1", f"Assert.False(false)：结论与被测行为无关"))
        if name in ("Equal", "StrictEqual") and SELF_EQ.match(args):
            hits.append((line, "R1", f"Assert.{name}({args.strip()})：比较的是同一个常量"))
        if not args.strip() and name in ("True", "False"):
            hits.append((line, "R1", f"Assert.{name}()：没有断言任何东西"))
        for m in re.finditer(r"\|\|\s*true\b|\btrue\s*\|\|", args):
            hits.append((line, "R2",
                         f"Assert.{name} 的谓词里出现「{m.group(0)}」：谓词被短路成恒真"))
    return hits


FACT_ATTR = re.compile(r"^\s*\[\s*<\s*Fact\s*>\s*\]")
TOP_LEVEL_DECL = re.compile(
    r"^\s*(?:\[<[^>]*>\]\s*|type\b|module\b|namespace\b|let\b|open\b)")


def indent_of(line):
    return len(line) - len(line.lstrip())


def find_facts(code_lines):
    facts = []
    i, n = 0, len(code_lines)
    while i < n:
        if FACT_ATTR.match(code_lines[i][1]):
            start = i
            name = ""
            for j in range(start + 1, min(n, start + 5)):
                m = re.search(r"``(.+?)``", code_lines[j][1])
                if m:
                    name = m.group(1); break
                s = code_lines[j][1].strip()
                if s and not s.startswith("//"):
                    break
            # 事实体 = **它自己的 `let` 那一行起**，到下一个顶层声明为止。
            #   * 必须包含 let 那一行：单行事实（`let ``x`` () = Assert.Equal(...)`）
            #     的函数体就在那一行上。
            #   * 终点不能是 `[<Fact>]` 后的第一个顶层声明——那正是事实自己的 let，
            #     所以终点扫描从 let 之后开始。
            let_line = start + 1
            for j in range(start + 1, min(n, start + 6)):
                if re.match(r"^let\s", code_lines[j][1]):
                    let_line = j
                    break
            end = n
            for j in range(let_line + 1, n):
                line = code_lines[j][1]
                if not line.strip():
                    continue
                if indent_of(line) == 0 and TOP_LEVEL_DECL.match(line):
                    end = j; break
            facts.append((let_line, end, name))
            i = end
            continue
        i += 1
    return facts


def top_level_lets(code_lines):
    starts = []
    for i, (_, line) in enumerate(code_lines):
        m = re.match(r"^let\s+(?:(?:private|inline|public|static)\s+)*(?:rec\s+)?([A-Za-z][A-Za-z0-9_']*)\b", line)
        if m:
            starts.append((i, m.group(1)))
    return starts


def asserting_helpers(code_lines):
    n = len(code_lines)
    starts = top_level_lets(code_lines)
    bodies = {}
    for idx, (i, name) in enumerate(starts):
        end = starts[idx + 1][0] if idx + 1 < len(starts) else n
        bodies[name] = "\n".join(code_lines[j][1] for j in range(i, end))
    asserting = {n2 for n2, b in bodies.items()
                 if "Assert." in b or "failwith" in b}
    changed = True
    while changed:
        changed = False
        for name, body in bodies.items():
            if name in asserting:
                continue
            for other in asserting:
                if re.search(r"\b" + re.escape(other) + r"\b", body):
                    asserting.add(name); changed = True; break
    return asserting, bodies


# ---- 扫描 ----

def test_files():
    out = []
    for root in ("libs", "compose"):
        for dirpath, dirnames, filenames in os.walk(root):
            parts = dirpath.split(os.sep)
            if "tests" not in parts:
                continue
            if any(p in ("bin", "obj") for p in parts):
                continue
            for name in sorted(filenames):
                if name.endswith(".fs"):
                    out.append(os.path.join(dirpath, name))
    return sorted(out)


files = test_files()
if not files:
    print("FAIL 前置：找不到任何测试文件（libs/*/tests 或 compose/*/tests）")
    sys.exit(2)

print(f"扫描 {len(files)} 个测试文件（libs/*/tests、compose/*/tests）")

r1_hits, r2_hits, r3_hits, lex_errors = [], [], [], []

for path in files:
    try:
        text = open(path, encoding="utf-8").read()
    except OSError:
        continue
    code, errs = strip_code(text)
    for ln, msg in errs:
        lex_errors.append((path, ln, msg))

    for ln, rule, msg in check_r1_r2(code):
        (r1_hits if rule == "R1" else r2_hits).append((path, ln, msg))

    code_lines = list(enumerate(code.split("\n"), 1))
    helpers, bodies = asserting_helpers(code_lines)
    for body_start, end, name in find_facts(code_lines):
        # body_start = 事实自己的 let 那一行（单行事实的函数体就在那行上）。
        body = "\n".join(code_lines[j][1] for j in range(body_start, end))
        if "Assert." in body or "failwith" in body:
            continue
        if any(re.search(r"\b" + re.escape(h) + r"\b", body) for h in helpers):
            continue
        r3_hits.append((path, code_lines[body_start][0], name or "(无名事实)"))

total = 0

print()
print("R1 恒真断言（Assert.True(true) / Assert.False(false) / Assert.True() / 自反比较）:")
if r1_hits:
    for path, ln, msg in r1_hits:
        print(f"  FAIL {path}:{ln}: {msg}")
    total += len(r1_hits)
    print(f"  → {len(r1_hits)} 处")
else:
    print("  ok   无恒真断言")

print()
print("R2 恒真谓词（Assert.* 参数里的 '|| true' / 'true ||'）:")
if r2_hits:
    for path, ln, msg in r2_hits:
        print(f"  FAIL {path}:{ln}: {msg}")
    total += len(r2_hits)
    print(f"  → {len(r2_hits)} 处")
else:
    print("  ok   无恒真谓词")

print()
print("R3 空转的事实（[<Fact>] 函数体里没有断言、failwith 或会断言的本文件辅助函数）:")
if r3_hits:
    for path, ln, name in r3_hits:
        print(f"  FAIL {path}:{ln}: {name}")
    total += len(r3_hits)
    print(f"  → {len(r3_hits)} 处")
else:
    print("  ok   无空转事实")

if lex_errors:
    print()
    print("词法告警（字符串/注释未闭合，扫描结果可能不完整）:")
    for path, ln, msg in lex_errors:
        print(f"  WARN {path}:{ln}: {msg}")

print()
if total:
    print(f"结果：{total} 项断言卫生违规")
    sys.exit(1)
print("结果：断言卫生全部通过")
PY
exit $?
