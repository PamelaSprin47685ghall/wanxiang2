# MathTypeset（组合）

旧 `Wanxiang.UI/Rich/MathLayout.fs` 的排版逻辑：把 KaTeX 的 HTML 盒模型摆成与界面框架无关的图元（字形、横线、SVG 路径）。协作者：KatexHtml（包）、调用方给的 `IGlyphMeter` 字形量尺。Avalonia 侧只剩：用 FormattedText 实现量尺、把图元转成 FormattedText/Geometry（路径加 `F1 ` 前缀，变换为先缩放 scale 再平移 dx、dy）。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同样的 HTML 与同样的字形度量，排出的盒子与图元与旧实现逐位相同。** [根 AXIOM: 5]
2. **字族选择、vlist 堆叠、居中、根号拉伸与裁剪的规则归本组合者；量字形归宿主。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/math.txt` 由旧 MathLayout 在 Avalonia headless + Skia + 内嵌 KaTeX 字体下生成（旧 4 个 fixtures 与 12 个公式；公式 HTML 由旧 JintRichBackend.MathHtml 现场生成，完整保留 SVG 路径里的换行），每个字形记下字号与度量；测试回放这张度量表，输出文本与金样逐字节相同。
2. [LEMMA: 2] 字族空串表示正文字体（金样中显示为 Sarasa Gothic SC）。
3. 与旧实现的差异：图元不再携带 FormattedText/Typeface/Geometry；旧实现在 Geometry.Parse 失败时整个 SVG 盒子变成空盒，新实现不解析路径，此情形由宿主负责（KaTeX 产出的路径都可解析）。

# 迁入或覆盖的旧测试
旧 MathRenderTests（改用回放量尺）：
- `数字与运算符用直立的 KaTeX_Main`
- `变量用斜体的数学字体`
- `大型算符取 Size 系列字体`
- `减号与正负号落在有这些字形的字体上`
- `分子分母与分数线共享同一条中轴`
- `上下限居中于大型算符`
- `根号画成 SVG 且保留上方横线`

旧 UiPureLogicTests 第 6 节：
- `katexFontFactor constant value is verified`
- `MathBox structure can hold items and baselines`

未迁入（需要真实绘制，留在旧 UI 测试）：`公式的墨迹不越出它自己声明的尺寸`、MathBaselineTests。

本组合新增：金样、无 katex-html 子树、量尺抛错、零宽空格、CJK 回落、外边距。

# 金样生成
`tools/mathgold/run.sh <旧仓库检出目录>`：旧仓库取 extract-ledger 的 `68e10a5^`（`git archive 68e10a5^ src tests`），在 Avalonia headless + Skia + 内嵌 KaTeX 字体下跑旧 MathLayout 与旧 JintRichBackend，重写 `tests/golden/math.txt`；输出应与提交的金样逐字节相同。生成器有自己的 Directory.Build.props，不继承仓库设置，也不打包。
