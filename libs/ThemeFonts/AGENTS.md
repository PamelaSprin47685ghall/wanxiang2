# ThemeFonts（库）

旧 `Wanxiang.UI/Assets/Fonts` 的内嵌字体原样迁入（Sarasa Gothic SC 子集、Sarasa Term SC 子集、KaTeX 全套，连同两份许可证），作为 Avalonia 资源打进本库：`ThemeFonts.body`、`ThemeFonts.mono`、`ThemeFonts.family name`，资源地址为 `avares://ThemeFonts/Assets/Fonts/`。只有资源与地址；活的字体实例归主题管理者。依赖 Avalonia，不引用任何其他库。根规则见项目根目录 AGENTS.md。

## 外观
- 无 Look 记录，无样式类。

# LEMMA
1. **字体文件与旧仓库逐字节相同，正文、等宽与 KaTeX 字族都能从本库解析。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs`：资源存在且与提交的 SHA-256 一致（`tests/golden/fonts.sha256`，由旧仓库文件生成）；headless + Skia 下三类字族都解析到真实字形（字形名不是回落字体）。

# 迁入或覆盖的旧测试
旧 `MathRenderTests.减号与正负号落在有这些字形的字体上` 依赖字体能解析，字形选择部分已在 MathTypeset；字体可解析由本库覆盖。
