# HljsCatalog

合并包：HljsCatalog、RichScripts。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `HljsCatalog`

## HljsCatalog

hljs 语言目录：解析目录 JSON、把语言标注解析成规范名、判断是否要按需加载（从旧 RichAssets 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`HljsCatalog`、`HljsCatalog.empty`、`parse`、`resolve`、`requiresLoad`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **目录文本由调用方给出；本库不读资源。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

## 迁入或覆盖的旧测试
本库测试（新写，边界与失败路径）：
- `别名优先，再认基座语言，大小写与空白不敏感`
- `按需语言只经别名到达（与旧实现相同）`
- `空白与未知标注为 None`
- `目录格式不对时退化为空目录`

另由以下覆盖：
- compose/RichAssets 迁入的 `language annotations resolve through hljs aliases`、`an unknown language annotation resolves to nothing`（用真实目录）
- compose/RichJint 的 `着色与排版金样与旧实现逐字节相同`（golden/rich.txt 由旧 RichBackend.fs、Highlight.fs、KatexHtml.fs、RichJint.fs 原样编译生成）

## 成员 `RichScripts`

## RichScripts

内嵌的 highlight.js（BSD-3-Clause，许可证随附）与 KaTeX（MIT）脚本和语言目录 JSON（从旧 Wanxiang.UI/Assets/Js 与 RichAssets 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`RichScripts.read`、`katex`、`hljsCore`、`hljsLanguage`、`hljsAliases`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **资源逐字节复制自旧仓库，清单逻辑名相同（js/…）；重新装配仍用旧仓库 tools/build_webassets.js。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

## 迁入或覆盖的旧测试
本库测试（新写，边界与失败路径）：
- `基座脚本都在且非空`
- `目录里每门按需语言都有对应脚本`
- `不存在的脚本抛出并带上名字`

另由以下覆盖：
- compose/RichJint 的 `着色与排版金样与旧实现逐字节相同`（golden/rich.txt 由旧 RichBackend.fs、Highlight.fs、KatexHtml.fs、RichJint.fs 原样编译生成）

## 资源生成
`js/` 由 `tools/build_scripts.js` 生成：用 `node libs/HljsCatalog/tools/build_scripts.js` 运行，或设置 `RICHSCRIPTS_OUT=<目录>` 输出到别处。脚本迁自旧仓库 `tools/build_webassets.js`，去掉了字体部分，字体仍由旧仓库生成。依赖锁定为 katex@0.18.4 和 highlight.js@11.12.0（cdn-release 打包体）。已验证生成结果与仓库里的 `js/` 逐字节相同（`diff -r` 无差异）。
