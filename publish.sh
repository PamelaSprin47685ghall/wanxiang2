#!/usr/bin/env bash
# wanxiang2 独立发布：产包 → dotnet publish（自包含）→ 打包（tar.gz/zip + sha256）。
# 镜像宿主 release.yml 的 package job 与 tools/package_release.sh，但对象是 wanxiang2 入口，
# 不引用宿主仓库路径、不依赖宿主脚本。
#
#   ./publish.sh <rid> [version]                  # 发布单个 RID；version 默认 v0.0.0-dev
#   ./publish.sh --all [version]                  # 四个 RID 依次发布（linux-x64 win-x64 osx-arm64 osx-x64）
#   ./publish.sh --draft <rid|--all> [version]    # 打包后把产物挂到 GitHub 草稿 Release（需 gh 与 GH_TOKEN）
#
# 环境变量：
#   WX_SKIP_PACK=1    跳过产包（artifacts/feed 已存在且与当前源码一致时）
#   WX_VERSION=...    版本号（只用于包名；不用 VERSION：MSBuild 会把环境变量当属性读）
set -euo pipefail
cd "$(dirname "$0")"

if [ -x "$HOME/.dotnet/dotnet" ]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$HOME/.dotnet:$PATH"
fi

RIDS=(linux-x64 win-x64 osx-arm64 osx-x64)
DRAFT=0
if [ "${1:-}" = "--draft" ]; then DRAFT=1; shift; fi
case "${1:-}" in
  --all) TARGETS=("${RIDS[@]}"); shift ;;
  "") echo "用法：./publish.sh [--draft] <rid|--all> [version]" >&2; exit 2 ;;
  *) TARGETS=("$1"); shift ;;
esac
for r in "${TARGETS[@]}"; do
  case "$r" in linux-x64|win-x64|osx-arm64|osx-x64) ;; *) echo "未知 RID：$r（可用：${RIDS[*]}）" >&2; exit 2 ;; esac
done
VER="${1:-${WX_VERSION:-v0.0.0-dev}}"

ENTRY="app/Desktop"
PROJ=$(find "$ENTRY" -maxdepth 2 \( -name '*.fsproj' -o -name '*.csproj' \) -not -path '*/obj/*' -not -path '*/bin/*' 2>/dev/null | head -1)
if [ -z "$PROJ" ]; then
  echo "未找到桌面入口项目：$ENTRY 下没有 .fsproj/.csproj（入口由 wx2-desktop 提供）" >&2
  exit 1
fi

# 产包：桌面入口经 Wanxiang2.* 0.0.0-local 包消费，restore 之前必须先有 artifacts/feed。
if [ "${WX_SKIP_PACK:-0}" != "1" ] || [ ! -d artifacts/feed ]; then
  echo "产包…" >&2
  ./x pack
fi

# 清掉入口工程的增量产物：app/*/bin 与 app/*/obj 留着旧的 AppBundle、wasm 与程序集时，
# 同版本本地包（0.0.0-local）+ 增量打包会把旧程序集带进发布目录（桌面发布还会把
# app/Pwa/bin/.../AppBundle 整份拷进 pwa/）。删干净后 publish 只可能对应当前源码。
rm -rf app/*/bin app/*/obj

mkdir -p dist
OUT_DIR=$(cd dist && pwd)
PUB_ROOT="$PWD/artifacts/publish"

package() {
  local rid="$1" pub="$2"
  local name="wanxiang2-$VER-$rid"
  local stage
  stage=$(mktemp -d /tmp/wanxiang2-pkg-XXXX)
  mkdir -p "$stage/$name"
  cp -a "$pub/." "$stage/$name/"
  # 若桌面发布目录内嵌 PWA（宿主 App 的 PublishPwaBundle 等价物），校验补丁后的运行时存在
  if [ -d "$stage/$name/pwa" ]; then
    [ -f "$stage/$name/pwa/_framework/avalonia.js" ] || { echo "pwa bundle missing in $pub" >&2; rm -rf "$stage"; return 1; }
  fi
  find "$stage/$name" -maxdepth 1 -type f -perm -u+x -exec chmod +x {} + 2>/dev/null || true
  case "$rid" in
    win-*) (cd "$stage" && python3 -c "import shutil,sys;shutil.make_archive(sys.argv[1],'zip','.',sys.argv[2])" "$OUT_DIR/$name" "$name") ;;
    *)     tar -C "$stage" -czf "$OUT_DIR/$name.tar.gz" "$name" ;;
  esac
  rm -rf "$stage"
  local art
  if [ -f "$OUT_DIR/$name.tar.gz" ]; then art="$name.tar.gz"; else art="$name.zip"; fi
  (cd "$OUT_DIR" && sha256sum "$art" > "$art.sha256")
  echo "$OUT_DIR/$art"
}

for rid in "${TARGETS[@]}"; do
  echo "== publish $rid ($VER)"
  PUB="$PUB_ROOT/$rid"
  rm -rf "$PUB"
  # 干净消费（与 ./x app、tools/smoke_desktop.sh 同一约定）：Wanxiang2.* 必须从 artifacts/feed
  # 解到仓库内缓存。走 ~/.nuget/packages 时，同版本号（0.0.0-local）的陈旧解包命中缓存后
  # NuGet 不再重解，发布会对着过期包编译（2026-10-18 run-desktop.sh 实测 37 个 FS0039/FS1129；
  # CI 的 release 工作流缓存 ~/.nuget/packages，同理）。
  rm -rf artifacts/packages/wanxiang2.*
  dotnet publish "$PROJ" -c Release -r "$rid" --self-contained true -o "$PUB" --verbosity minimal -p:RestorePackagesPath="$PWD/artifacts/packages"
  package "$rid" "$PUB"
done

if [ "$DRAFT" = "1" ]; then
  command -v gh >/dev/null 2>&1 || { echo "gh 不可用，跳过草稿 Release" >&2; exit 1; }
  : "${GH_TOKEN:?GH_TOKEN 未设置}"
  export GH_TOKEN
  if gh release view "$VER" >/dev/null 2>&1; then
    if [ "$(gh release view "$VER" --json isDraft -q .isDraft)" != "true" ]; then
      echo "release $VER already published; refusing to modify" >&2; exit 1
    fi
    gh release upload "$VER" "$OUT_DIR"/* --clobber
  else
    gh release create "$VER" "$OUT_DIR"/* --draft --verify-tag --generate-notes --title "万象2 $VER"
  fi
fi
