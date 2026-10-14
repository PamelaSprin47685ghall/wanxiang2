# MarkdownView（组合，Markdown 正文控件）

旧 `Controls/MarkdownRenderer.fs` 的 `MarkdownRenderer` 与 `StreamingMarkdown`（extract-ledger@0c0b2a6），改名 `MarkdownView` 与 `MarkdownStream`。协作者（包）：MarkdownBlocks（块模型）、LinkChunks（长链接分段）、BlockDiff（流式增量，只在 MarkdownStream 里用）、DesignRhythm（阅读节奏与布局规则）、MathFigure（公式）、CodeTint（着色）、CopyButton（复制确认）。不认识主题管理者，外观全部经 `MarkdownLook` 由调用方（ReadingLook）给。根规则见项目根目录 AGENTS.md。

## 外观
- `MarkdownLook`：`Ink`（`MarkdownInk` 画笔槽，活实例原地改色）、`Gauge`（`MarkdownGauge` 尺寸槽，活值）、`BodyFont`、`MonoFont`、`Glyph`（按画笔造图标）、`Dress`（给按钮挂共享样式类）、`Reduced`、`Copy`（CopyButtonLook）、`Math`（MathFigureLook）。
- 用到的样式类：由 `Dress MarkdownButton.Icon | Ghost` 决定；ReadingLook 挂 `surface hover-plain icon-button` 与 `surface hover-plain button-ghost`。
- 活值只在控件挂进可视树期间订阅，摘下即退订。会被父级改写的外边距（引用卡）只在构造时取一次。
- 字号、行高、段距、列表缩进、表格最小列宽、代码 120 行限高仍是规则（DesignRhythm 与本组合的内容策略），不是外观值。

## 接口
- `MarkdownHost`：`CopyText`、`OpenLink`、`MathHtml`（宿主后端）、`Tint`（CodeTint 实例）、`Wrap`（`CodeWrapMemory`，折行记忆）。
- `MarkdownView(look, host, fontSize, allowHighlight)`：`Render`、`RenderText`、`RenderBlock`。
- `MarkdownStream(view)`：`Panel`、`BlockCount`、`Update raw`。
- `ActionSpot`：可点的 Border，自动化层报按钮并给 Invoke。

# LEMMA
1. **同一段 Markdown 渲染出的控件树与旧 MarkdownRenderer 相同。** [根 AXIOM: 5]（金样在 ReadingLook，因为要用真实主题填 Look）
2. **控件行为与读屏语义照旧：链接、复制、折行、展开、任务框。**
3. **流式面板只替换变了的块，结果与整段渲染同构。**

# SPEC
1. [LEMMA: 1] 见 `compose/ReadingLook/AGENTS.md` SPEC 1。
2. [LEMMA: 2] `tests/Tests.fs`：`MarkdownRenderer wide table (>4 columns) wraps in horizontal scroll viewer`、`Markdown hyperlink responds to Enter and Space key to openLink`、`long links split into chunks with one tab stop and one announced name`、`Markdown code block copy button confirms visually with check icon and tooltip`、`code block copy confirmation dips toward fade opacity and keeps the confirmed label`、`wrap toggle flips the block and is remembered for later blocks`、`huge code block expand button reports its expanded state`、`MarkdownRenderer task boxes expose checkbox automation state`、`markdown renderer renders complex markdown with code, tables and math without error`、`inline math without html falls back to italic mono source`、`last block bottom margin is cleared and quote edges are tight`、`live gauges follow while shown and stop after removal`。
3. [LEMMA: 3] `stream keeps finished blocks mounted and matches a full render`。

# 与旧代码的差异
- 链接键盘激活只打开一次。旧代码链接自己的 KeyDown 与 `Ui.onClick` 的 KeyDown 各开一次，按一次 Enter 打开两次。
- `DefaultCodeWrap` 由静态量改为宿主持有的 `CodeWrapMemory`。
- 公式 HTML 与着色由宿主传入，不再问全局 RichBackend。
- 图标按钮不再有 `InputModality.suppressTooltipOnTouch`（那是宿主的输入方式判断，接入时由宿主补）。
- 尺寸（边框、内边距、圆角、圆点、任务框、字距、复制淡出时长）改主题后已显示的块跟着变；旧代码只在构造时取一次。

# 迁入或覆盖的旧测试
旧 CraftsmanshipPolishTests：`MarkdownRenderer wide table (>4 columns) wraps in horizontal scroll viewer`、`Markdown hyperlink responds to Enter and Space key to openLink`、`Markdown code block copy button confirms visually with check icon and tooltip`、`huge code block expand button reports its expanded state`、`MarkdownRenderer task boxes expose checkbox automation state`。旧 DeliveryPolishingTests：`markdown renderer renders complex markdown with code, tables and math without error`。旧 PresentationRefreshTests：`code block copy confirmation dips toward fade opacity and keeps the confirmed label`。同名迁入。
