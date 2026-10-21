#!/usr/bin/env python3
"""库清单审计：每个 libs/<Name> 包的规模、自身依赖、生产消费者。

用途：一次性清点全库清单（每包的生产文件数、生产 LOC、外部依赖、Wanxiang2 依赖、
生产消费者、AGENTS.md/tests/UNWIRED.md 存在性），供 docs/W7-CLEANBREAK-AUDIT.md 的
§1 表格引用；`tools/audit_inventory.json` 是本文表格的原始数据。

触发时机：**只在结构性变更后手工重算一次**，即
  - 新增/删除/合并 libs/<Name> 包，或改动包的 `<Compile Include>` 成员文件；
  - 改动某包的 `PackageReference`（外部依赖或 Wanxiang2.* 依赖）；
  - 新增/删除包的生产消费者（compose/ 或 app/ 下工程引用 `Wanxiang2.<Name>`）。
日常改代码、改测试、跑构建**不需要**跑它；它不是门禁，不进 `./x check` / `./x all`，
CI 也不执行（门禁由 ./x check、tools/assert-hygiene.sh、tools/lint-copy.sh 承担）。

用法：仓库根执行 `python3 tools/audit_inventory.py`。
它会打印表格并**覆写** tools/audit_inventory.json；重算后请连同本文件一起提交，
免得 JSON 与源码脱节（下次重算时 diff 应为空或只反映真实变更）。
"""
import json, os, re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LIBS = os.path.join(ROOT, "libs")
SKIP_DIRS = ("bin", "obj", "tests")


def fs_files(d):
    out = []
    for dp, dn, fn in os.walk(d):
        dn[:] = [x for x in dn if x not in SKIP_DIRS]
        for f in fn:
            if f.endswith((".fs", ".cs")):
                out.append(os.path.join(dp, f))
    return sorted(out)


def loc(p):
    n = 0
    with open(p, encoding="utf-8") as fh:
        for line in fh:
            s = line.strip()
            if s and not s.startswith("//"):
                n += 1
    return n


def members_of(d, proj):
    """工程的实际生产源文件：F# 工程按 <Compile Include>；C# 工程按默认 glob（*.cs）。"""
    txt = open(proj, encoding="utf-8").read()
    if proj.endswith(".csproj"):
        files = []
        for dp, dn, fn in os.walk(d):
            dn[:] = [x for x in dn if x not in SKIP_DIRS]
            for f in fn:
                if f.endswith(".cs"):
                    files.append(os.path.join(dp, f))
        return sorted(files)
    inc = re.findall(r'<Compile Include="([^"]+)"', txt)
    out = []
    for f in inc:
        p = os.path.join(d, f)
        if os.path.exists(p):
            out.append(p)
    return sorted(out)


def all_projects():
    out = []
    for top in ("libs", "compose", "app"):
        base = os.path.join(ROOT, top)
        if not os.path.isdir(base):
            continue
        for dp, dn, fn in os.walk(base):
            dn[:] = [x for x in dn if x not in ("bin", "obj")]
            for f in fn:
                if f.endswith((".fsproj", ".csproj")):
                    out.append(os.path.join(dp, f))
    return out


def refs_of(p):
    txt = open(p, encoding="utf-8").read()
    return re.findall(r'<PackageReference\s+Include="([^"]+)"', txt)


def proj_name(p):
    rel = os.path.relpath(p, ROOT)
    return rel


def main():
    projs = all_projects()
    refs = {p: refs_of(p) for p in projs}
    rows = []
    for name in sorted(os.listdir(LIBS)):
        d = os.path.join(LIBS, name)
        if not os.path.isdir(d):
            continue
        proj = None
        for ext in (".fsproj", ".csproj"):
            c = os.path.join(d, name + ext)
            if os.path.exists(c):
                proj = c
        if proj is None:
            cands = sorted(f for f in os.listdir(d) if f.endswith((".fsproj", ".csproj")))
            rows.append({
                "name": name, "stale": True,
                "has_agents": os.path.exists(os.path.join(d, "AGENTS.md")),
                "has_tests": os.path.isdir(os.path.join(d, "tests")),
                "files": 0, "loc": 0, "consumers": [], "external": [],
                "wanxiang": [], "unwired": os.path.exists(os.path.join(d, "UNWIRED.md")),
            })
            continue
        pkg = "Wanxiang2." + name
        own = refs[proj]
        external = sorted(r for r in own if not r.startswith("Wanxiang2."))
        wan = sorted(r for r in own if r.startswith("Wanxiang2."))
        consumers = []
        for p in projs:
            if p == proj:
                continue
            rel = os.path.relpath(p, ROOT)
            if rel.split(os.sep)[-2:-1] and "/tests/" in "/" + rel.replace(os.sep, "/"):
                continue
            if pkg in refs[p]:
                consumers.append(rel)
        files = members_of(d, proj)
        rows.append({
            "name": name, "stale": False,
            "has_agents": os.path.exists(os.path.join(d, "AGENTS.md")),
            "has_tests": os.path.isdir(os.path.join(d, "tests")),
            "unwired": os.path.exists(os.path.join(d, "UNWIRED.md")),
            "files": len(files), "loc": sum(loc(f) for f in files),
            "members": [os.path.basename(f) for f in files],
            "consumers": sorted(consumers), "external": external, "wanxiang": wan,
        })
    json.dump(rows, open(os.path.join(ROOT, "tools", "audit_inventory.json"), "w"),
              ensure_ascii=False, indent=1)
    for r in rows:
        if r["stale"]:
            print(f"STALE {r['name']} agents={r['has_agents']} tests={r['has_tests']}")
            continue
        print(f"{r['name']:20} f={r['files']:2} loc={r['loc']:5} c={len(r['consumers']):2} "
              f"ext={','.join(r['external']) or '-'} wx={','.join(r['wanxiang']) or '-'} "
              f"| {', '.join(r['members'])}")


main()
