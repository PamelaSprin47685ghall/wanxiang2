# ThemeFonts（库）

旧 `Wanxiang.UI/Assets/Fonts` 的内嵌字体原样迁入（Sarasa Gothic SC 子集、Sarasa Term SC 子集、KaTeX 全套，连同两份许可证），另加一份 Noto Sans Symbols 子集（OFL 1.1，符号逐字回落用），作为 Avalonia 资源打进本库：`ThemeFonts.body`、`ThemeFonts.mono`、`ThemeFonts.symbols`、`ThemeFonts.family name`，资源地址为 `avares://ThemeFonts/Assets/Fonts/`。只有资源与地址；活的字体实例归主题管理者。依赖 Avalonia，不引用任何其他库。根规则见项目根目录 AGENTS.md。

## 外观
- 无 Look 记录，无样式类。

# LEMMA
1. **迁入的旧字体文件（正文 Sarasa Gothic SC 子集、等宽 Sarasa Term SC 子集、KaTeX 全套）与旧仓库逐字节相同，正文、等宽与 KaTeX 字族都能从本库解析。** [根 AXIOM: 5]
2. **符号字族 `Wanxiang Symbols` 逐字回落：** 正文与等宽的复合字族尾部都带它，缺字（如 ⌘ ⚙ ✎ ✓ ✕ 📦 🔍 🗑）在浏览器端也落到真实轮廓字形而不是豆腐块。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs`：`every old font file ships byte for byte`、`body mono and katex families resolve to the embedded faces`、`katex main carries the minus and plus minus glyphs`——资源存在且与提交的 SHA-256 一致（`tests/golden/fonts.sha256`，由旧仓库文件生成）；headless + Skia 下三类字族都解析到真实字形（字形名不是回落字体）。
2. [LEMMA: 2] `tests/Tests.fs`：`the symbol family resolves to the embedded face`、`body and mono fall back to real glyphs for the symbol codepoints`——`Wanxiang Symbols` 自身可解析，且 `ThemeFonts.body` / `ThemeFonts.mono` 对每个符号码位都能匹配到非 `.notdef` 的字形。

## 新增字体文件（不属于「旧文件逐字节相同」的范围）
- `Assets/Fonts/WanxiangSymbols-Regular.subset.ttf`：Noto Sans Symbols / Noto Sans Symbols 2 的合并子集（OFL 1.1，许可证见 `Assets/Fonts/LICENSE-Noto-Symbols.txt`），字族名改为 `Wanxiang Symbols`（不沿用 Noto 的保留字体名）。码位：U+2315 ⌕、U+2318 ⌘、U+2699 ⚙、U+270E ✎、U+2713 ✓、U+2715 ✕、U+1F4E6 📦、U+1F50D 🔍、U+1F5D1 🗑。

# 迁入或覆盖的旧测试
旧 `MathRenderTests.减号与正负号落在有这些字形的字体上` 依赖字体能解析，字形选择部分已在 MathTypeset；字体可解析由本库覆盖。
