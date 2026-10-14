# RichScripts

内嵌的 highlight.js（BSD-3-Clause，许可证随附）与 KaTeX（MIT）脚本和语言目录 JSON（从旧 Wanxiang.UI/Assets/Js 与 RichAssets 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`RichScripts.read`、`katex`、`hljsCore`、`hljsLanguage`、`hljsAliases`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **资源逐字节复制自旧仓库，清单逻辑名相同（js/…）；重新装配仍用旧仓库 tools/build_webassets.js。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

# 迁入或覆盖的旧测试
本库测试（新写，边界与失败路径）：
- `基座脚本都在且非空`
- `目录里每门按需语言都有对应脚本`
- `不存在的脚本抛出并带上名字`

另由以下覆盖：
- compose/RichJint 的 `着色与排版金样与旧实现逐字节相同`（golden/rich.txt 由旧 RichBackend.fs、Highlight.fs、KatexHtml.fs、RichJint.fs 原样编译生成）
