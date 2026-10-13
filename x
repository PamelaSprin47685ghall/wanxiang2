#!/usr/bin/env bash
# wanxiang2 构建入口。
#   ./x test  <lib>   单独构建并测试 libs/<lib>（只凭本库与声明的外部包）
#   ./x check         依赖检查：库不引用本库以外的源码；组合者只经交付包使用库
#   ./x pack          把每个库和组合者打成包，放进 artifacts/feed
#   ./x compose       用干净的包缓存消费 artifacts/feed，测试 compose/ 下的组合者
#   ./x all           check + 逐库 test + pack + compose
#   ./x sln           由 libs/ 重新生成 wanxiang2.slnx
set -euo pipefail
export MSBUILDDISABLENODEREUSE=1
cd "$(dirname "$0")"

libs()     { for d in libs/*/;    do basename "$d"; done; }
composers(){ for d in compose/*/; do [[ -d "$d" ]] && basename "$d"; done; }

need_lib() {
  [[ -n "${1:-}" && -d "libs/$1" ]] || { echo "unknown lib: ${1:-}" >&2; echo "libs: $(libs | tr '\n' ' ')" >&2; exit 2; }
}

# 项目文件中的相对引用路径（ProjectReference / Import / Compile）
refs() { grep -oE '(ProjectReference|Import|Compile)[^>]*(Include|Project)="[^"]*"' "$1" | sed -E 's/.*"([^"]*)"/\1/' || true; }

check() {
  local bad=0
  for lib in $(libs); do
    local root; root="$(realpath "libs/$lib")"
    for f in AGENTS.md "$lib.fsproj" "tests/$lib.Tests.fsproj"; do
      [[ -f "libs/$lib/$f" ]] || { echo "FAIL $lib: missing $f"; bad=1; }
    done
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
    done < <(find "libs/$lib" -name '*.fsproj' -not -path '*/obj/*' -not -path '*/bin/*')
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

pack() {
  rm -rf artifacts/feed
  for lib in $(libs); do dotnet pack "libs/$lib/$lib.fsproj" -c Release -p:Version=0.0.0-local -o artifacts/feed -v q -nologo; done
  # 组合者也是交付物；它自己从 feed 消费库包
  rm -rf artifacts/packages/wanxiang2.* compose/*/obj
  for c in $(composers); do
    dotnet pack "compose/$c/$c.fsproj" -c Release -p:Version=0.0.0-local -p:RestorePackagesPath="$PWD/artifacts/packages" -o artifacts/feed -v q -nologo
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

gen_sln() {
  echo '<Solution>'
  for lib in $(libs); do
    echo "  <Folder Name=\"/$lib/\">"
    echo "    <Project Path=\"libs/$lib/$lib.fsproj\" />"
    echo "    <Project Path=\"libs/$lib/tests/$lib.Tests.fsproj\" />"
    echo '  </Folder>'
  done
  echo '</Solution>'
}

case "${1:-}" in
  test)    need_lib "${2:-}"; dotnet test --project "libs/$2/tests/$2.Tests.fsproj" ;;
  check)   check ;;
  pack)    pack ;;
  compose) compose ;;
  sln)     gen_sln > wanxiang2.slnx; echo "wrote wanxiang2.slnx" ;;
  all)
    check
    for lib in $(libs); do echo "== $lib"; dotnet test --project "libs/$lib/tests/$lib.Tests.fsproj"; done
    pack; compose ;;
  *) sed -n '2,9p' "$0"; exit 2 ;;
esac
