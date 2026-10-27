# MathTypeset（组合）

旧 `Wanxiang.UI/Rich/MathLayout.fs` 的排版逻辑：把 KaTeX 的 HTML 盒模型摆成与界面框架无关的图元（字形、横线、SVG 路径）。协作者：KatexHtml（包）、调用方给的 `IGlyphMeter` 字形量尺。Avalonia 侧只剩：用 FormattedText 实现量尺、把图元转成 FormattedText/Geometry（路径加 `F1 ` 前缀，变换为先缩放 scale 再平移 dx、dy）。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同样的 HTML 与同样的字形度量，排出的盒子与图元在几何意义上与旧实现一致。** [根 AXIOM: 5]
2. **字族选择、vlist 堆叠、居中、根号拉伸与裁剪的规则归本组合者；量字形归宿主。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] 排版结果在给定量尺下满足可断言的几何事实（`tests/Tests.fs` 的 `行内公式的主体字形落在盒基线上`、`所有字形的墨迹都落在盒宽之内`、`同一行的字形自左向右排列`、`盒宽不小于任何字形的右边缘` 等）：主体字形落在盒基线上；同一行的字形自左向右排列且不越出盒宽；上标基线高于基行、下标基线低于基行且都起于基字右侧；分数的分子在线上、分母在线下且与分数线同轴；根号是被开方内容右侧的 SVG 路径，裁剪宽度等于盒宽；展示式的上下限分居算符上下且与算符居中对齐；矩阵同列对齐、上行高于下行；行内式与展示式的主体字形落在同一条基线上。空公式与语法错误的公式返回带原因的 `Error`（`html without a katex-html subtree is an error with a reason`）。
2. [LEMMA: 2] 字族空串表示正文字体（`\text{中文}` 落在正文字体上，见 `数字与运算符用直立的 KaTeX_Main`、`变量用斜体的数学字体`、`大型算符用 Size 系列字体`）。
3. 与旧实现的差异：图元不再携带 FormattedText/Typeface/Geometry（`MathBox structure can hold items and baselines`）；旧实现在 Geometry.Parse 失败时整个 SVG 盒子变成空盒，新实现不解析路径，此情形由宿主负责（KaTeX 产出的路径都可解析）。

# 测试
- 公式的 KaTeX HTML 由生产用的脚本后端（`Wanxiang2.RichJint`）现场产出，测试因此直接对真实 KaTeX 输出断言排版事实；不再有逐字节复刻旧实现的黄金样本与生成工具。
- 用量尺是确定性的：宽度按字族与字符给出稳定推进量，基线与总高只随字号变。纵向位置全部来自 KaTeX 算好的 em 数值，故断言与具体字体度量无关。
- 迁入的旧用例（名字保留）：`数字与运算符用直立的 KaTeX_Main`、`变量用斜体的数学字体`、`大型算符用 Size 系列字体`、`减号与正负号落在有这些字形的字体上`、`katexFontFactor constant value is verified`、`MathBox structure can hold items and baselines`、`html without a katex-html subtree is an error with a reason`。
- 本组合新增：基线与盒宽几何、上下标、分数、根式、积分、矩阵、行内与展示式的差别、空公式、语法错误、未定义宏、长公式宽度单调、外边距、零宽空格、CJK 回落、量尺抛错。
