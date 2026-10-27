# KatexHtml

解析 KaTeX 的 HTML 输出为 KNode 树，取字号倍率与 em 值（从旧 Rich/KatexHtml.fs 原样搬来）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`KNode`、`KatexHtml.fontScaleOf`、`emOf`、`parse`、`visualSubtree`、`isDisplay`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **布局（MathLayout）与绘制（MathVisual）依赖 Avalonia，留在界面。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 的 `KatexHtml fontScaleOf computes ratio of size classes`、`KatexHtml emOf parses em and px correctly`、`KatexHtml parse and visualSubtree filters katex-html`、`KatexHtml isDisplay detects display formulas` 通过。

# 迁入或覆盖的旧测试
迁自旧 UiPureLogicTests（RichBackendAndKatexHtmlTests）（原文，只改名字）与本库补充的测试：
- `KatexHtml fontScaleOf computes ratio of size classes`
- `KatexHtml emOf parses em and px correctly`
- `KatexHtml parse and visualSubtree filters katex-html`
- `KatexHtml isDisplay detects display formulas`
- `单独的 size 类不是缩放`
- `标签没闭合时已收集的内容不丢`
- `svg 的 width height d viewBox 并进 style`
- `实体解码，空串为空`

另由以下覆盖：
- compose/RichJint 的 `着色与排版金样与旧实现逐字节相同`（golden/rich.txt 由旧 RichBackend.fs、Highlight.fs、KatexHtml.fs、RichJint.fs 原样编译生成）
