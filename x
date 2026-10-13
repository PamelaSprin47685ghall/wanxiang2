#!/usr/bin/env bash
# wanxiang2 构建入口。库之间零引用，所以每个库独立构建、独立测试，顺序无关。
#   ./x build <lib>   构建单个库及其测试
#   ./x test  <lib>   测试单个库
#   ./x all           check + 逐库 test
#   ./x check         正交性检查：引用只能留在本库目录内
#   ./x sln           由 libs/ 重新生成 wanxiang2.slnx
set -euo pipefail
cd "$(dirname "$0")"

libs() { for d in libs/*/; do basename "$d"; done; }

need_lib() {
  [[ -n "${1:-}" && -d "libs/$1" ]] || { echo "unknown lib: ${1:-}" >&2; echo "libs: $(libs | tr '\n' ' ')" >&2; exit 2; }
}

check() {
  local bad=0
  for lib in $(libs); do
    local root; root="$(realpath "libs/$lib")"
    [[ -f "libs/$lib/AGENTS.md" ]] || { echo "FAIL $lib: missing AGENTS.md"; bad=1; }
    [[ -f "libs/$lib/$lib.fsproj" ]] || { echo "FAIL $lib: missing $lib.fsproj"; bad=1; }
    [[ -f "libs/$lib/tests/$lib.Tests.fsproj" ]] || { echo "FAIL $lib: missing tests/$lib.Tests.fsproj"; bad=1; }
    while IFS= read -r proj; do
      local dir; dir="$(dirname "$proj")"
      # 抽取 ProjectReference / Import / Compile 中的相对路径，逐个解析
      while IFS= read -r ref; do
        [[ -z "$ref" ]] && continue
        local abs; abs="$(realpath -m "$dir/${ref//\\//}")"
        case "$abs" in
          "$root"/*) ;;
          *) echo "FAIL $lib: $proj -> $ref escapes libs/$lib"; bad=1 ;;
        esac
      done < <(grep -oE '(ProjectReference|Import|Compile)[^>]*(Include|Project)="[^"]*"' "$proj" | sed -E 's/.*"([^"]*)"/\1/')
      # 测试项目只能引用本库主项目
      if [[ "$proj" == */tests/* ]]; then
        while IFS= read -r ref; do
          [[ "$(realpath -m "$dir/${ref//\\//}")" == "$root/$lib.fsproj" ]] || { echo "FAIL $lib: test refs $ref"; bad=1; }
        done < <(grep -oE 'ProjectReference Include="[^"]*"' "$proj" | sed -E 's/.*"([^"]*)"/\1/')
      elif grep -q 'ProjectReference' "$proj"; then
        echo "FAIL $lib: main project must have no ProjectReference"; bad=1
      fi
    done < <(find "libs/$lib" -name '*.fsproj' -not -path '*/obj/*' -not -path '*/bin/*')
  done
  # slnx 必须与 libs/ 一致
  if ! diff -q <(gen_sln) wanxiang2.slnx >/dev/null 2>&1; then echo "FAIL wanxiang2.slnx out of date (run ./x sln)"; bad=1; fi
  [[ $bad == 0 ]] && echo "check ok: $(libs | wc -l) libs, zero cross references"
  return $bad
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
  build) need_lib "${2:-}"; dotnet build "libs/$2/tests/$2.Tests.fsproj" -v q -nologo ;;
  test)  need_lib "${2:-}"; dotnet test --project "libs/$2/tests/$2.Tests.fsproj" ;;
  check) check ;;
  sln)   gen_sln > wanxiang2.slnx; echo "wrote wanxiang2.slnx" ;;
  all)
    check
    for lib in $(libs); do echo "== $lib"; dotnet test --project "libs/$lib/tests/$lib.Tests.fsproj"; done ;;
  *) sed -n '2,8p' "$0"; exit 2 ;;
esac
