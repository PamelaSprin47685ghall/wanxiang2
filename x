#!/usr/bin/env bash
# wanxiang2 构建入口。
#   ./x test  <lib>   单独构建并测试 libs/<lib>（只凭本库与声明的外部包）
#   ./x check         依赖检查：库不引用本库以外的源码；组合者只经交付包使用库
#   ./x hygiene       断言卫生：恒真断言 / 恒真谓词 / 空转的事实一律红灯（秒级，只需源码）
#   ./x format        把全仓 F# 排成 Fantomas 格式（规则见 .editorconfig，工具见 .config/dotnet-tools.json）
#   ./x lint          界面文案：禁用同义词 / 取消类提示须写明 Esc / 中文省略号（秒级，只需源码）
#   ./x pack          把每个库和组合者打成包，放进 artifacts/feed
#   ./x compose       用干净的包缓存消费 artifacts/feed，测试 compose/ 下的组合者
#   ./x consume       在仓库外的临时项目里只从 artifacts/feed 取包，构建并真实运行
#   ./x app           从 artifacts/feed 构建 app/ 下的入口（Pwa 需 wasm-tools workload）
#   ./x all           check + hygiene + lint + pack + 逐库 test + compose + consume（测试并发：XJOBS；默认按内存与核数取，见 test_jobs）
#   ./x sln           由 libs/ 重新生成 wanxiang2.slnx
#   ./x new-widget <Name>  由 tools/widget-template 生成控件库 libs/<Name>（目录已存在则拒绝）
set -euo pipefail
export MSBUILDDISABLENODEREUSE=0
cd "$(dirname "$0")"

# 全仓统一 SDK：系统 SDK（/usr/share/dotnet）没有 Microsoft.AspNetCore.App 共享框架
# （compose/ServerApp 需要，NETSDK1226），也没有 wasm-tools（app/Pwa 需要）。
# 用户级 SDK（~/.dotnet）两者都有；global.json 的 rollForward=latestFeature 允许它。
# 凡 ~/.dotnet 存在，全部 dotnet 调用都走它；系统环境没有用户级 SDK 时照常走系统 dotnet。
if [ -x "$HOME/.dotnet/dotnet" ]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
fi

libs()     { for d in libs/*/;    do basename "$d"; done; }
composers(){ for d in compose/*/; do [[ -d "$d" ]] && basename "$d"; done; }

# 库的主项目：F# 为 <lib>.fsproj；需要 C# source generator（JSImport）的库为 <lib>.csproj
libproj()  { if [[ -f "libs/$1/$1.csproj" ]]; then echo "libs/$1/$1.csproj"; else echo "libs/$1/$1.fsproj"; fi; }

need_lib() {
  [[ -n "${1:-}" && -d "libs/$1" ]] || { echo "unknown lib: ${1:-}" >&2; echo "libs: $(libs | tr '\n' ' ')" >&2; exit 2; }
}



# 项目文件中的相对引用路径（ProjectReference / Import / Compile）
refs() { grep -oE '(ProjectReference|Import|Compile)[^>]*(Include|Project)="[^"]*"' "$1" | sed -E 's/.*"([^"]*)"/\1/' || true; }

# 规范化绝对路径，等价 GNU `realpath -m`：即使路径不存在也解析，不要求存在、不解析符号链接。
# 本机 macOS 的 BSD realpath 没有 -m，也没有 grealpath；python3 在 macOS 与 Linux 都自带，
# os.path.abspath(os.path.normpath(...)) 与 realpath -m 语义一致。
abspath() { python3 -c 'import os,sys; print(os.path.abspath(os.path.normpath(sys.argv[1])))' "$1"; }

# 排序一律走字节序：`sort` 的默认排序随 locale 变，而 gen_sln 的输出要与仓库里的
# wanxiang2.slnx 逐字节比对（2026-10-18 首轮 CI 就因为本机 en_US 与 runner 的 C 排序不同而失败）。
sort_c() { LC_ALL=C sort "$@"; }

check() {
  local bad=0
  for lib in $(libs); do
    local root; root="$(realpath "libs/$lib")"
    for f in AGENTS.md "tests/$lib.Tests.fsproj"; do
      [[ -f "libs/$lib/$f" ]] || { echo "FAIL $lib: missing $f"; bad=1; }
    done
    [[ -f "$(libproj "$lib")" ]] || { echo "FAIL $lib: missing $lib.fsproj or $lib.csproj"; bad=1; }
    while IFS= read -r proj; do
      local dir; dir="$(dirname "$proj")"
      while IFS= read -r ref; do
        [[ -z "$ref" ]] && continue
        local abs; abs="$(abspath "$dir/$ref")"
        [[ "$abs" == "$root"/* ]] || { echo "FAIL $lib: $proj -> $ref escapes libs/$lib"; bad=1; }
      done < <(refs "$proj")
      if [[ "$proj" != */tests/* ]] && grep -q 'ProjectReference' "$proj"; then
        echo "FAIL $lib: main project must have no ProjectReference"; bad=1
      fi
      # 库之间没有跨库依赖（根 AGENTS.md《布局与工具》）；WorkspaceState 作为单一状态树特例放行兄弟包（见《例外清单》）。
      local sibs
      sibs="$([[ "$lib" == "WorkspaceState" ]] && true || grep -oE 'Wanxiang2\.[A-Za-z0-9]+' "$proj" | sort -u | grep -vE '^Wanxiang2\.WorkspaceState$' || true)"
      if [[ -n "$sibs" ]]; then echo "FAIL $lib: $proj uses sibling package(s): $(echo $sibs | tr '\n' ' ')"; bad=1; fi
    done < <(find "libs/$lib" \( -name '*.fsproj' -o -name '*.csproj' \) -not -path '*/obj/*' -not -path '*/bin/*')
  done
  # 组合者：不经 ProjectReference 碰 libs/（只用交付包）；库不知道组合者
  for c in $(composers); do
    [[ -f "compose/$c/AGENTS.md" ]] || { echo "FAIL compose/$c: missing AGENTS.md"; bad=1; }
    while IFS= read -r proj; do
      local dir; dir="$(dirname "$proj")"
      while IFS= read -r ref; do
        [[ -z "$ref" ]] && continue
        [[ "$(abspath "$dir/$ref")" == "$(realpath "compose/$c")"/* ]] || { echo "FAIL compose/$c: $proj -> $ref escapes compose/$c"; bad=1; }
      done < <(refs "$proj")
    done < <(find "compose/$c" -name '*.fsproj' -not -path '*/obj/*' -not -path '*/bin/*')
  done
  if ! diff -q <(gen_sln) wanxiang2.slnx >/dev/null 2>&1; then echo "FAIL wanxiang2.slnx out of date (run ./x sln)"; bad=1; fi
  # 每个库都要有消费者：至少一个 libs/compose/app 的生产工程（非 tests/）引用它的包。
  # 没有消费者的库是死重（Axiom 2）：接上、删掉，或者确有过渡必要就在库目录放 UNWIRED.md
  #（登记理由与可验证的解除条件，LEMMA 9）。这条检查是 2026-10-18 那次「孤儿库」整理的固化。
  local consumed
  consumed="$(
    for proj in $(find libs compose app \( -name '*.fsproj' -o -name '*.csproj' \) -not -path '*/obj/*' -not -path '*/bin/*' -not -path '*/tests/*'); do
      # `|| true`：没有 Wanxiang2.* 引用的工程 grep 返回 1；管道状态以最后一次为准，
      # 缺了它 set -e 会在赋值处静默中止（目录遍历顺序一变就复现，2026-10-18 CI 抓到）。
      grep -oE 'Wanxiang2\.[A-Za-z0-9]+' "$proj" | sed 's/^Wanxiang2\.//' || true
    done | sort -u
  )"
  for lib in $(libs); do
    if ! grep -qx "$lib" <<< "$consumed"; then
      if [[ -s "libs/$lib/UNWIRED.md" ]]; then :; else
        echo "FAIL $lib: no production consumer (wire it into a composer/app, delete it, or add libs/$lib/UNWIRED.md with reason + exit condition)"
        bad=1
      fi
    fi
  done
  # 文档与各 AGENTS.md 里点名的 libs/<名> / compose/<名> 必须存在：拆库/删库后最容易留下的就是这种过期引用
  #（docs/archive 是历史档案，不查；`<Name>` 是模板占位，放过）。
  local stale=""
  for f in AGENTS.md $(find libs compose docs tools -name '*.md' -not -path '*/archive/*' | sort); do
    while IFS= read -r ref; do
      case "$ref" in "libs/<Name>"|"compose/<Name>") continue;; esac
      [[ -d "$ref" ]] || stale="$stale
FAIL $f: references missing $ref"
    done < <(grep -oE '(libs|compose)/[A-Za-z0-9_]+' "$f" 2>/dev/null | sort -u || true)
  done
  if [[ -n "$stale" ]]; then
    echo "$stale"
    bad=1
  fi
  # 包二进制不得进 Git 索引。忽略规则挡不住已经暂存的文件。
  local packed
  packed="$(git ls-files '*.nupkg' '*.snupkg' '*.tgz' 2>/dev/null || true)"
  if [[ -n "$packed" ]]; then
    echo "FAIL git index contains package binaries:"
    echo "$packed"
    bad=1
  fi
  # F# 格式门禁（Fantomas）：规则在 .editorconfig，工具版本钉在 .config/dotnet-tools.json。
  # 只需源码、不需构建产物，所以放在依赖检查之后、最后汇总之前。
  if ! format_check; then bad=1; fi
  [[ $bad == 0 ]] && echo "check ok: $(libs | wc -l) libs with no sibling references; $(composers | wc -l) composer(s) using packages only"
  return $bad
}

# F# 格式门禁：dotnet fantomas --check 覆盖全仓 .fs（.fantomasignore 挡掉 obj/bin 等生成物）。
# 格式工具是本地工具（.config/dotnet-tools.json），没恢复过就先 restore 一次。
format_check() {
  local restore_out out rc=0
  if ! restore_out="$(dotnet tool restore 2>&1)"; then
    echo "FAIL format: dotnet tool restore failed"
    echo "$restore_out" | tail -5
    return 1
  fi
  out="$(dotnet fantomas --check . 2>&1)" || rc=$?
  if [[ $rc == 0 ]]; then return 0; fi
  if [[ $rc == 99 ]]; then
    echo "FAIL format: F# files are not formatted (run: dotnet tool restore && dotnet fantomas .)"
    echo "$out" | grep -E 'needs formatting' | head -40
    return 1
  fi
  echo "FAIL format: dotnet fantomas --check failed (exit $rc)"
  echo "$out" | tail -20
  return 1
}

# 一次性把全仓 F# 排成 Fantomas 格式（改完跑这个；./x check 负责拦未格式化的提交）。
format() {
  dotnet tool restore
  dotnet fantomas .
}

# 组件工程枚举：libs/ 与 compose/ 顶层目录直属（路径深度恰好 <root>/<dir>/<file>）的全部 .fsproj/.csproj，
# 按工程实际文件名收集，不再要求目录名==工程名。异名工程（实测全量仅此 2 个：
# libs/StartupTrace/LocalHostGate.fsproj、compose/ImageShrink/ImageShrinkIo.fsproj）不再被漏。
# 排除语义保持：测试工程在 <组件>/tests/、金样工具工程在 <组件>/tools/（均更深，被 maxdepth 2 挡掉），
# 再叠加名称保险不取 *.Tests.*；app/ 入口不在 libs/ 与 compose/ 之下，本就不在枚举范围。
component_projs() {
  find libs compose -mindepth 2 -maxdepth 2 \( -name '*.fsproj' -o -name '*.csproj' \) \
    -not -path '*/obj/*' -not -path '*/bin/*' -not -name '*.Tests.*' | sort_c
}

pack() {
  rm -rf artifacts/feed
  # 库与组合者都是交付物，都从 feed 消费 Wanxiang2.* 包（库只允许依赖基础库 UiKit）。
  rm -rf artifacts/packages/wanxiang2.*
  mkdir -p artifacts/feed artifacts
  # 按依赖顺序分波打包：同一波内的工程互不依赖，可以放进一个临时 solution 里
  # 让 MSBuild 自己并行（一次 restore + 一次 pack），不再为每个包各起一个 dotnet 进程。
  # 这是「每个包的编译准备时间太长」的主要来源：进程启动 + 逐包 restore + 逐包 MSBuild 求值。
  local todo; todo="$(component_projs)"
  local wave=0
  while [[ -n "$todo" ]]; do
    local ready="" blocked=""
    for proj in $todo; do
      local missing=0
      # 末尾 `|| true` 同上：没有 Wanxiang2.* 依赖的工程不让那次替换把整条命令置为非零。
      for dep in $(grep -oE 'Include="Wanxiang2\.[A-Za-z0-9]+"' "$proj" | sed -E 's/.*Wanxiang2\.([A-Za-z0-9]+)"/\1/' || true); do
        [[ -f "artifacts/feed/Wanxiang2.$dep.0.0.0-local.nupkg" ]] || missing=1
      done
      if [[ $missing == 1 ]]; then blocked="$blocked $proj"; else ready="$ready $proj"; fi
    done
    if [[ -z "$ready" ]]; then
      echo "composer dependency cycle or missing package:$blocked" >&2
      return 1
    fi
    wave=$((wave+1))
    # 临时 solution 放在 artifacts/ 下（NuGet.config 向上查找能找到根配置）。
    # solution 里的工程路径按 solution 自身所在目录解析，所以要写成 ../ 开头；
    # 否则 MSBuild 会去找 artifacts/libs/...（MSB3202）。
    local sln; sln="artifacts/pack-wave-$wave.slnx"
    {
      echo '<Solution>'
      for proj in $ready; do echo "  <Project Path=\"../$proj\" />"; done
      echo '</Solution>'
    } > "$sln"
    echo "== pack wave ${wave}（$(printf '%s\n' $ready | wc -l) 个工程，一个 solution 一次 restore/pack）"
    if ! dotnet restore "$sln" -p:RestorePackagesPath="$PWD/artifacts/packages" --verbosity q --nologo; then
      rm -f "$sln"; return 1
    fi
    if ! dotnet pack "$sln" -c Release -p:Version=0.0.0-local -p:RestorePackagesPath="$PWD/artifacts/packages" -o artifacts/feed --no-restore --verbosity q --nologo; then
      rm -f "$sln"; return 1
    fi
    rm -f "$sln"
    todo="$blocked"
  done
}

compose() {
  [[ -d artifacts/feed ]] || pack
  # 干净消费：包缓存里不留任何 Wanxiang2.*，确保用的是刚打的包，而不是旧缓存或源码
  rm -rf artifacts/packages/wanxiang2.* compose/*/obj compose/*/tests/obj
  # 无 tests/ 的组合者（CredentialStore、EntryShells、ExternalLinks、UiPrefsFile、UrlSchemeRegistrationIo）
  # 跳过，与 gen_sln 的口径一致：不要求每个组合者都有测试工程。
  # 逐个 `if` 而不是 `[[ ]] && echo`：后者在最后一个组合者没有测试工程时让整条替换返回 1，
  # set -e 会在赋值处静默中止（与 consumed 同一类，2026-10-18 一起修）。
  local list=""
  for c in $(composers); do
    if [[ -f "compose/$c/tests/$c.Tests.fsproj" ]]; then list="$list"$'\n'"compose/$c/tests/$c.Tests.fsproj"; fi
  done
  run_tests_parallel "compose（组合者测试）" "$list" "$(test_jobs)" "-p:RestorePackagesPath=$PWD/artifacts/packages"
}

# 并发数：默认由核数与**内存**共同决定，XJOBS 可覆盖。
# 只按核数来会 OOM：每个 `dotnet test`（restore + F# 编译 + 跑用例）峰值近 1 GiB，
# 12 GiB 无 swap 的机器上开 10 路会把系统打死（2026-10-18 实测）。规则：
# 每个并发工程按 2 GiB 计，另给系统与编辑器留 4 GiB，再按核数封顶（最多 8）。
test_jobs() {
  if [[ -n "${XJOBS:-}" ]]; then echo "$XJOBS"; else
    local n; n="$(nproc 2>/dev/null || echo 4)"
    local mem_gb=8
    if [[ -r /proc/meminfo ]]; then
      mem_gb="$(awk '/^MemTotal:/ {printf "%d", $2 / 1048576}' /proc/meminfo)"
    fi
    local by_mem=$(( (mem_gb - 4) / 2 ))
    # 用 if 而不是 `(( )) && ...`：条件不成立时后者让整条语句返回 1，set -e 会当场中止。
    if (( by_mem < 1 )); then by_mem=1; fi
    local j=$n
    if (( j > by_mem )); then j=$by_mem; fi
    if (( j > 12 )); then j=12; fi
    echo "$j"
  fi
}

# 并行跑一组测试工程：list 是换行分隔的工程路径；每工程一份日志（/tmp），结束只回显失败项与汇总。
# extra 作为每个 dotnet test 的附加参数（如 RestorePackagesPath）。
run_tests_parallel() {
  local title="$1" list="$2" jobs="$3" extra="${4:-}"
  local logdir; logdir="$(mktemp -d /tmp/wanxiang2-tests-XXXXXX)"
  local total=0 running=0
  echo "== ${title}（并发 ${jobs}；日志 ${logdir}）"
  # 并发的 dotnet 首跑会抢同一套一次性初始化（NuGet-Migrations 互斥量、XDG_RUNTIME_DIR 下
  # .dotnet/shm/session* 的建立），抢输的进程直接起不来（CI 上 AguiWire 就这么挂过，2026-10-18）。
  # 对策：先补 shm 目录，再串行跑第一个工程把初始化坐实，然后才并发其余。
  local xdg="${XDG_RUNTIME_DIR:-}"
  if [[ -n "$xdg" && -d "$xdg" ]]; then mkdir -p "$xdg/.dotnet/shm" 2>/dev/null || true; fi
  local warm; warm="$(printf '%s\n' "$list" | awk 'NF {print; exit}')"
  local rest="$list"
  if [[ -n "$warm" ]]; then
    rest="$(printf '%s\n' "$list" | awk 'seen {print; next} NF {seen=1}')"
    local wname; wname="$(basename "$(dirname "$(dirname "$warm")")")"
    total=1
    echo FAIL > "$logdir/$wname.status" 2>/dev/null || true
    if dotnet test --project "$warm" $extra > "$logdir/$wname.log" 2>&1; then
      echo ok > "$logdir/$wname.status" 2>/dev/null || true
    else
      echo "-- FAIL ${wname}（热身第一个，串行）"
    fi
  fi
  while IFS= read -r proj; do
    [[ -n "$proj" ]] || continue
    local name; name="$(basename "$(dirname "$(dirname "$proj")")")"
    total=$((total+1))
    # 先写下「失败」占位：状态文件写不出来（子进程被杀、日志盘满）时不能当成成功。
    echo FAIL > "$logdir/$name.status" 2>/dev/null || true
    (
      if dotnet test --project "$proj" $extra > "$logdir/$name.log" 2>&1; then
        echo ok > "$logdir/$name.status" 2>/dev/null || true
      fi
    ) &
    running=$((running+1))
    if [[ $running -ge $jobs ]]; then wait -n || true; running=$((running-1)); fi
  done <<< "$rest"
  wait || true
  local failed=0 recorded=0
  for s in "$logdir"/*.status; do
    [[ -e "$s" ]] || continue
    recorded=$((recorded+1))
    if [[ "$(cat "$s")" == FAIL ]]; then
      local name; name="$(basename "$s" .status)"
      failed=$((failed+1))
      echo "-- FAIL ${name}（日志 $logdir/$name.log 末尾）"
      tail -25 "$logdir/$name.log"
    fi
  done
  # 没留下状态文件的（子进程被 OOM/ENOSPC 之类的系统因素干掉）也按失败算：
  # 只数现存的状态文件会把「什么都没跑成」报成全部通过（2026-10-18 实测过）。
  if [[ $recorded -ne $total ]]; then
    failed=$((failed + total - recorded))
    echo "-- $(( total - recorded )) 个工程没有留下状态文件（子进程被杀或日志盘满），按失败计"
  fi
  if [[ $failed -eq 0 ]]; then
    echo "== ${title}：$total 个工程全部通过（日志 ${logdir}）"
  else
    echo "== ${title}：$total 个工程中 $failed 个失败" >&2
    return 1
  fi
}

# 仓库外消费：交付包要能在仓库外使用（根 SPEC 2；compose/LedgerStore SPEC 14）。
# 在仓库外的临时目录里拷出消费者项目（tools/consume/），只从 artifacts/feed 取 Wanxiang2.* 交付包，
# 构建并真实运行；程序自己断言落盘行为，失败就以非零码退出。
consume() {
  [[ -d artifacts/feed ]] || pack
  local root; root="$PWD"
  # 干净消费：包缓存里不留任何 Wanxiang2.*，确保用的是刚打的包，而不是旧缓存或源码
  rm -rf artifacts/packages/wanxiang2.*
  local tmp; tmp="$(mktemp -d /tmp/wanxiang2-consume-XXXXXX)"
  cp tools/consume/Program.fs tools/consume/Consumer.fsproj "$tmp/"
  sed "s#__FEED__#$root/artifacts/feed#" tools/consume/NuGet.config.in > "$tmp/NuGet.config"
  echo "== consume：仓库外消费者 ${tmp}（Wanxiang2.* 只从 artifacts/feed 取）"
  if ( cd "$tmp" && NUGET_PACKAGES="$root/artifacts/packages" dotnet run --project Consumer.fsproj -c Release ); then
    rm -rf "$tmp"
  else
    echo "consume 失败：消费者项目保留在 $tmp" >&2
    return 1
  fi
}

# 产品入口：从交付包构建 app/ 下的全部入口（Desktop 与 Pwa），只构建、不启动。
# PWA 需要 wasm-tools workload：系统 SDK 没有、用户级 SDK 有时优先用后者（run-pwa.sh 的同一约定）。
app() {
  [[ -d artifacts/feed ]] || pack
  # 干净消费：包缓存里不留任何 Wanxiang2.*，确保入口用的是刚打的包，而不是旧缓存或源码
  rm -rf artifacts/packages/wanxiang2.* app/*/obj
  local jobs; jobs="$(test_jobs)"
  local logdir; logdir="$(mktemp -d /tmp/wanxiang2-app-XXXXXX)"
  local running=0
  for proj in $(find app -mindepth 2 -maxdepth 2 \( -name '*.fsproj' -o -name '*.csproj' \) -not -path '*/obj/*' -not -path '*/bin/*' | sort_c); do
    echo "== app：$proj"
    (
      dotnet build "$proj" -c Debug -p:RestorePackagesPath="$PWD/artifacts/packages" --verbosity minimal -nologo > "$logdir/$(basename "$(dirname "$proj")").log" 2>&1
    ) &
    running=$((running+1))
    if [[ $running -ge $jobs ]]; then wait -n || true; running=$((running-1)); fi
  done
  wait || true
  for f in "$logdir"/*.log; do
    [[ -e "$f" ]] || continue
    if grep -Eq 'error |Build FAILED|Failed!' "$f"; then
      echo "-- APP FAIL $(basename "$f" .log)"; tail -15 "$f"; return 1
    fi
  done
}

# 组件目录：从 component_projs 反推顶层目录（每个至少含一个直属组件工程）。
# 让 gen_sln 与 pack 共用同一套工程枚举（component_projs），不再各自靠「目录名==工程名」拼路径。
# sed 把 <root>/<dir>/<file> 归约到 <root>/<dir> 再去重；libs 段与 compose 段在 gen_sln 里分别遍历，
# 保持与现有 slnx 相同的 libs 排序，避免整段重排。
component_dirs() {
  component_projs | sed -E 's#^(libs|compose)/([^/]+)/.*#\1/\2#' | sort_c -u
}

# 一个组件目录对应的 slnx folder：先列该目录直属的全部组件工程（含异名，如 LocalHostGate / ImageShrinkIo），
# 再列其 tests/ 下真实存在的测试工程；无 tests/ 目录的（实测 compose 有 6 个）自然跳过，不写悬空条目。
emit_dir_folder() {
  local d="$1"
  echo "  <Folder Name=\"/${d#*/}/\">"
  find "$d" -mindepth 1 -maxdepth 1 \( -name '*.fsproj' -o -name '*.csproj' \) -not -path '*/obj/*' -not -path '*/bin/*' \
    | sort_c | sed 's#^#    <Project Path="#; s#$#" />#'
  if [[ -d "$d/tests" ]]; then
    find "$d/tests" -mindepth 1 -maxdepth 1 \( -name '*.fsproj' -o -name '*.csproj' \) -not -path '*/obj/*' -not -path '*/bin/*' \
      | sort_c | sed 's#^#    <Project Path="#; s#$#" />#'
  fi
  echo '  </Folder>'
}

# 生成整份 wanxiang2.slnx：libs 组件目录（字典序，与现有一致）-> compose 组件目录 -> /App/ 入口。
# /App/ 单 folder 含 app/ 下的真实工程（Desktop 与 Pwa 目录名异名于工程名，故按文件枚举而非目录名派生）。
gen_sln() {
  echo '<Solution>'
  local d
  for d in $(component_dirs | grep '^libs/'); do emit_dir_folder "$d"; done
  for d in $(component_dirs | grep '^compose/'); do emit_dir_folder "$d"; done
  echo '  <Folder Name="/App/">'
  find app -mindepth 2 -maxdepth 2 \( -name '*.fsproj' -o -name '*.csproj' \) -not -path '*/obj/*' -not -path '*/bin/*' \
    | sort | sed 's#^#    <Project Path="#; s#$#" />#'
  echo '  </Folder>'
  echo '</Solution>'
}

case "${1:-}" in
  test)    need_lib "${2:-}"; dotnet test --project "libs/$2/tests/$2.Tests.fsproj" ;;
  check)   check ;;
  # 断言卫生：把「恒真断言 / 恒真谓词 / 空转的事实」变成红灯（tools/assert-hygiene.sh）。
  # 不需要构建产物，秒级；CI 里单独一个 job（只 checkout）。
  hygiene) bash tools/assert-hygiene.sh ;;
  # 界面文案：产品文案规则没有运行时可观察，所以走独立的 lint 脚本而不是 xunit 用例
  # （tools/lint-copy.sh）。扫描 libs + compose + app 的生产 .fs。
  lint) bash tools/lint-copy.sh ;;
  # F# 格式：一次性把全仓 .fs 排成 Fantomas 格式（规则见 .editorconfig）。
  format)  format ;;
  pack)    pack ;;
  compose) compose ;;
  consume) consume ;;
  app)     app ;;
  sln)     gen_sln > wanxiang2.slnx; echo "wrote wanxiang2.slnx" ;;
  new-widget)
    [[ "${2:-}" =~ ^[A-Z][A-Za-z0-9]+$ ]] || { echo "usage: ./x new-widget <PascalName>" >&2; exit 2; }
    [[ -e "libs/$2" ]] && { echo "refuse: libs/$2 already exists" >&2; exit 1; }
    cp -r tools/widget-template "libs/$2"
    for f in $(find "libs/$2" -name '*__NAME__*' | sort -r); do mv "$f" "${f//__NAME__/$2}"; done
    find "libs/$2" -type f -exec sed -i "s/__NAME__/$2/g" {} +
    gen_sln > wanxiang2.slnx; echo "created libs/$2" ;;
  all)
    check
    bash tools/assert-hygiene.sh
    bash tools/lint-copy.sh
    # pack 在逐库 test 之前：少数库引用兄弟包（WorkspaceState 的例外清单项、以及依赖它的控件库），
    # 测试时要从 artifacts/feed 取包。CI 用 needs: [check, pack] 保证同样顺序。
    pack
    run_tests_parallel "逐库测试" "$(libs | sed 's#^\(.*\)$#libs/\1/tests/\1.Tests.fsproj#')" "$(test_jobs)"
    compose; consume ;;
  # 用法块 = 开头连续的注释行；行数变化自动同步（末行是第一条非注释行，去掉）
  *) sed -n '2,/^[^#]/p' "$0" | sed '$d'; exit 2 ;;
esac
