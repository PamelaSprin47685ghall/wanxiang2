#!/usr/bin/env bash
# wanxiang2 构建入口。
#   ./x test  <lib>   单独构建并测试 libs/<lib>（只凭本库与声明的外部包）
#   ./x check         依赖检查：库不引用本库以外的源码；组合者只经交付包使用库
#   ./x pack          把每个库和组合者打成包，放进 artifacts/feed
#   ./x compose       用干净的包缓存消费 artifacts/feed，测试 compose/ 下的组合者
#   ./x all           check + 逐库 test + pack + compose
#   ./x sln           由 libs/ 重新生成 wanxiang2.slnx
#   ./x new-widget <Name>  由 tools/widget-template 生成控件库 libs/<Name>（目录已存在则拒绝）
set -euo pipefail
export MSBUILDDISABLENODEREUSE=1
cd "$(dirname "$0")"

libs()     { for d in libs/*/;    do basename "$d"; done; }
composers(){ for d in compose/*/; do [[ -d "$d" ]] && basename "$d"; done; }

# 库的主项目：F# 为 <lib>.fsproj；需要 C# source generator（JSImport）的库为 <lib>.csproj
libproj()  { if [[ -f "libs/$1/$1.csproj" ]]; then echo "libs/$1/$1.csproj"; else echo "libs/$1/$1.fsproj"; fi; }

need_lib() {
  [[ -n "${1:-}" && -d "libs/$1" ]] || { echo "unknown lib: ${1:-}" >&2; echo "libs: $(libs | tr '\n' ' ')" >&2; exit 2; }
}

# 项目文件中的相对引用路径（ProjectReference / Import / Compile）
refs() { grep -oE '(ProjectReference|Import|Compile)[^>]*(Include|Project)="[^"]*"' "$1" | sed -E 's/.*"([^"]*)"/\1/' || true; }

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
        local abs; abs="$(realpath -m "$dir/$ref")"
        [[ "$abs" == "$root"/* ]] || { echo "FAIL $lib: $proj -> $ref escapes libs/$lib"; bad=1; }
      done < <(refs "$proj")
      if [[ "$proj" != */tests/* ]] && grep -q 'ProjectReference' "$proj"; then
        echo "FAIL $lib: main project must have no ProjectReference"; bad=1
      fi
      if grep -q 'Wanxiang2\.' "$proj"; then echo "FAIL $lib: $proj uses a sibling package"; bad=1; fi
    done < <(find "libs/$lib" \( -name '*.fsproj' -o -name '*.csproj' \) -not -path '*/obj/*' -not -path '*/bin/*')
  done
  # 组合者：不经 ProjectReference 碰 libs/（只用交付包）；库不知道组合者
  for c in $(composers); do
    while IFS= read -r proj; do
      local dir; dir="$(dirname "$proj")"
      while IFS= read -r ref; do
        [[ -z "$ref" ]] && continue
        [[ "$(realpath -m "$dir/$ref")" == "$(realpath "compose/$c")"/* ]] || { echo "FAIL compose/$c: $proj -> $ref escapes compose/$c"; bad=1; }
      done < <(refs "$proj")
    done < <(find "compose/$c" -name '*.fsproj' -not -path '*/obj/*' -not -path '*/bin/*')
  done
  if ! diff -q <(gen_sln) wanxiang2.slnx >/dev/null 2>&1; then echo "FAIL wanxiang2.slnx out of date (run ./x sln)"; bad=1; fi
  [[ $bad == 0 ]] && echo "check ok: $(libs | wc -l) libs with no sibling references; $(composers | wc -l) composer(s) using packages only"
  return $bad
}

# 组件工程枚举：libs/ 与 compose/ 顶层目录直属（路径深度恰好 <root>/<dir>/<file>）的全部 .fsproj/.csproj，
# 按工程实际文件名收集，不再要求目录名==工程名。异名工程（实测全量仅此 2 个：
# libs/StartupTrace/LocalHostGate.fsproj、compose/ImageShrink/ImageShrinkIo.fsproj）不再被漏。
# 排除语义保持：测试工程在 <组件>/tests/、金样工具工程在 <组件>/tools/（均更深，被 maxdepth 2 挡掉），
# 再叠加名称保险不取 *.Tests.*；app/ 入口不在 libs/ 与 compose/ 之下，本就不在枚举范围。
component_projs() {
  find libs compose -mindepth 2 -maxdepth 2 \( -name '*.fsproj' -o -name '*.csproj' \) \
    -not -path '*/obj/*' -not -path '*/bin/*' -not -name '*.Tests.*' | sort
}

pack() {
  rm -rf artifacts/feed
  # 库：主工程（F# 为 <lib>.fsproj；需要 C# source generator 的为 <lib>.csproj），按实际文件名打。
  for proj in $(component_projs | grep '^libs/'); do
    dotnet pack "$proj" -c Release -p:Version=0.0.0-local -o artifacts/feed -v q -nologo
  done
  # 组合者也是交付物；它自己从 feed 消费库包
  rm -rf artifacts/packages/wanxiang2.* compose/*/obj
  # 组合者可以消费别的组合者的包：按依赖顺序打包（依赖的包还不在 feed 里就留到下一轮）
  local todo; todo="$(component_projs | grep '^compose/')"
  while [[ -n "$todo" ]]; do
    local next="" progressed=0
    for proj in $todo; do
      local missing=0
      for dep in $(grep -oE 'Include="Wanxiang2\.[A-Za-z0-9]+"' "$proj" | sed -E 's/.*Wanxiang2\.([A-Za-z0-9]+)"/\1/'); do
        [[ -f "artifacts/feed/Wanxiang2.$dep.0.0.0-local.nupkg" ]] || missing=1
      done
      if [[ $missing == 1 ]]; then next="$next $proj"; continue; fi
      rm -rf "$(dirname "$proj")/obj"
      dotnet pack "$proj" -c Release -p:Version=0.0.0-local -p:RestorePackagesPath="$PWD/artifacts/packages" -o artifacts/feed -v q -nologo
      progressed=1
    done
    [[ $progressed == 1 ]] || { echo "composer dependency cycle or missing package:$next" >&2; return 1; }
    todo="$next"
  done
}

compose() {
  [[ -d artifacts/feed ]] || pack
  # 干净消费：包缓存里不留任何 Wanxiang2.*，确保用的是刚打的包，而不是旧缓存或源码
  rm -rf artifacts/packages/wanxiang2.* compose/*/obj compose/*/tests/obj
  for c in $(composers); do
    echo "== compose/$c"
    dotnet test --project "compose/$c/tests/$c.Tests.fsproj" -p:RestorePackagesPath="$PWD/artifacts/packages"
  done
}

# 组件目录：从 component_projs 反推顶层目录（每个至少含一个直属组件工程）。
# 让 gen_sln 与 pack 共用同一套工程枚举（component_projs），不再各自靠「目录名==工程名」拼路径。
# sed 把 <root>/<dir>/<file> 归约到 <root>/<dir> 再去重；libs 段与 compose 段在 gen_sln 里分别遍历，
# 保持与现有 slnx 相同的 libs 排序，避免整段重排。
component_dirs() {
  component_projs | sed -E 's#^(libs\|compose)/([^/]+)(/.*)?#\1/\2#' | sort -u
}

# 一个组件目录对应的 slnx folder：先列该目录直属的全部组件工程（含异名，如 LocalHostGate / ImageShrinkIo），
# 再列其 tests/ 下真实存在的测试工程；无 tests/ 目录的（实测 compose 有 6 个）自然跳过，不写悬空条目。
emit_dir_folder() {
  local d="$1"
  echo "  <Folder Name=\"\${d#*/}/\">"
  find "$d" -mindepth 1 -maxdepth 1 \( -name '*.fsproj' -o -name '*.csproj' \) -not -path '*/obj/*' -not -path '*/bin/*' \
    | sort | sed 's#^#    <Project Path="#; s#$#" />#'
  if [[ -d "$d/tests" ]]; then
    find "$d/tests" -mindepth 1 -maxdepth 1 \( -name '*.fsproj' -o -name '*.csproj' \) -not -path '*/obj/*' -not -path '*/bin/*' \
      | sort | sed 's#^#    <Project Path="#; s#$#" />#'
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
  pack)    pack ;;
  compose) compose ;;
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
    for lib in $(libs); do echo "== $lib"; dotnet test --project "libs/$lib/tests/$lib.Tests.fsproj"; done
    pack; compose ;;
  *) sed -n '2,10p' "$0"; exit 2 ;;
esac
