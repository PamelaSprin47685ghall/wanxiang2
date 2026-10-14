# MathFigure（组合，公式控件与公式量尺）

旧 `Rich/MathLayout.fs`（FormattedText 量尺、图元转成 Avalonia 类型）与 `Rich/MathVisual.fs`（`MathVisual` 控件、`MathRender.tryInline / inlineContainer / tryBlock`），extract-ledger@0c0b2a6。协作者（包）：MathTypeset（排版）、MathBaseline（基线规则）、ThemeFonts（KaTeX 字体）。KaTeX HTML 由调用方给（宿主 RichBackend 的 MathHtml），本组合不认识后端。根规则见项目根目录 AGENTS.md。

## 外观
- `MathFigureLook`：`Ink`（活画笔，原地改色不重排）、`BodyFont`（活字体，变了就重排）、`BlockPadding`（展示式公式上下留白的活值）。值由 ReadingLook 从主题管理者取。
- 用到的样式类：无。
- 活值只在控件挂进可视树期间订阅，摘下即退订。

## 接口
- `MathDraw.tryLayout body brush size html` → `MathPicture`（`MathMark.Glyph | Rule | Path`，RequireQualifiedAccess）。
- `MathFigure(html, displayMode, fontSize, look)`：`HasLayout`、`Picture`、`Baseline`。
- `MathFigures.tryInline look html size tex`、`MathFigures.inlineContainer`、`MathFigures.tryBlock look html size tex`。

# LEMMA
1. **同一条公式，行内与展示式的布局、基线与像素和旧 MathVisual 完全相同。** [根 AXIOM: 5]
2. **换主题色只重画，换正文字体才重排。**

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/figure.txt` 由 `tools/figuregold`（编译旧 MathLayout.fs、MathVisual.fs、RichBackend.fs，HTML 由 RichJint 包现场产出）在 headless + Skia 下生成：11 条公式，记下段落与公式的 Bounds、基线、读屏名和整块像素的 SHA-256 前缀。`inline and display formulas lay out and paint exactly like the old MathVisual` 逐字节相同。
2. [LEMMA: 2] `recoloring the live ink repaints without relayout`、`body font change relays out glyphs with the new family`、`live values are followed only while attached`。
3. `no html means no figure so the caller falls back to source text`、`inline container aligns to bottom and figures announce their source`、`katex glyph families come from the embedded fonts`。

# 与旧代码的差异
- 旧 MathVisual 自己去问全局 `RichBackend.current ()`；这里由调用方传 HTML。
- 旧画笔固定是 `Tokens.text`、正文字体固定；这里两者都来自 Look。
- 字体从 ThemeFonts 包解析（`avares://ThemeFonts/...`），字体文件与旧仓库逐字节相同。

# 迁入或覆盖的旧测试
- 旧 MathBaselineTests 的控件部分（行内公式基线对齐到正文基线）由 SPEC 1 的金样覆盖（基线与像素同旧）。
- 旧 MathRenderTests 的字族选择用例已在 MathTypeset；`公式的墨迹不越出它自己声明的尺寸` 由 SPEC 1 的像素金样覆盖。
