# MathFigure（组合，公式控件与公式量尺）

旧 `Rich/MathLayout.fs`（FormattedText 量尺、图元转成 Avalonia 类型）与 `Rich/MathVisual.fs`（`MathVisual` 控件、`MathRender.tryInline / inlineContainer / tryBlock`），extract-ledger@0c0b2a6。协作者（包）：MathTypeset（排版）、MathBaseline（基线规则）、ThemeFonts（KaTeX 字体）。KaTeX HTML 由调用方给（宿主 RichBackend 的 MathHtml），本组合不认识后端。已移除 UiKit 依赖；外观值经主题资源键声明式送达（W7 外观退役），不再有活值订阅。根规则见项目目录 AGENTS.md。

## 外观
- `MathFigureMetrics`：只剩 `Ink`（主题管理者的活画笔实例，原地改色不重排）。字体走资源键 `FontFamily.Body`（构造时先从应用资源取初值，保证 `HasLayout` 判定在挂树前就可用；之后经可继承的 `TextElement.FontFamily` 资源绑定随主题重排）；展示式上下留白走派生档 `Padding.MathBlock`。值由 ReadingLook 从主题管理者取。
- 用到的样式类：无。
- 无 IObservable 字段、无手工 Subscribe：主题改动由资源系统即时送达。

## 接口
- `MathDraw.tryLayout body brush size html` → `MathPicture`（`MathMark.Glyph | Rule | Path`，RequireQualifiedAccess）。
- `MathFigure(html, displayMode, fontSize, look)`：`HasLayout`、`Picture`、`Baseline`。
- `MathFigures.tryInline look html size tex`、`MathFigures.inlineContainer`、`MathFigures.tryBlock look html size tex`。

# LEMMA
1. **同一条公式，行内与展示式的布局、基线与读屏名和旧 MathVisual 完全相同。** [根 AXIOM: 5]
    - 像素不进跨平台金样：Skia 在 macOS（CoreText）与 Linux（FreeType）上光栅化结果不同，布局与基线则平台无关（2026-10-21 改）。
2. **换主题色只重画，换正文字体才重排。**

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/figure.txt` 由 `tools/figuregold`（编译旧 MathLayout.fs、MathVisual.fs、RichBackend.fs，HTML 由 RichJint 包现场产出）在 headless + Skia 下生成：11 条公式，记下段落与公式的 Bounds、基线、留白和读屏名（原金样里的整块像素 SHA-256 前缀已删，见 LEMMA 1）。`inline and display formulas lay out and paint exactly like the old MathVisual` 逐字节相同，Linux 与 macOS 都通过。
2. [LEMMA: 2] `recoloring the live ink repaints without relayout`（布局前后相同；像素只在本进程内前后对比，改色后不同）、`body font change relays out glyphs with the new family`、`live values are followed only while attached`。
3. `no html means no figure so the caller falls back to source text`、`inline container aligns to bottom and figures announce their source`、`katex glyph families come from the embedded fonts`。

# 与旧代码的差异
- 旧 MathVisual 自己去问全局 `RichBackend.current ()`；这里由调用方传 HTML。
- 旧画笔固定是 `Tokens.text`、正文字体固定；这里两者都来自 Look。
- 字体从 ThemeFonts 包解析（`avares://ThemeFonts/...`），字体文件与旧仓库逐字节相同。

# 迁入或覆盖的旧测试
- 旧 MathBaselineTests 的控件部分（行内公式基线对齐到正文基线）由 SPEC 1 的金样覆盖（基线同旧）。
- 旧 MathRenderTests 的字族选择用例已在 MathTypeset；`公式的墨迹不越出它自己声明的尺寸` 原由 SPEC 1 的像素金样间接覆盖，删去像素哈希后不再覆盖。[TODO] 改为直接断言：渲染位图在声明 Bounds 之外没有非透明像素。
