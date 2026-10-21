# MarkdownBlocks（库）

旧 `Wanxiang.UI/Model/Markdown.fs` 原样迁入（Markdig 1.3.2 → MdBlock / MdInline 块级模型），并收进旧 MarkdownRenderer 里只依赖这套模型的纯规则：`hasLongToken`（表格格子是否要套横向滚动）、`commonPrefix`（流式渲染保留多少已挂块）、`imagePlaceholder`（远程图片占位文字与提示）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一 Markdown 文本解析出的块与旧实现完全相同（%A 文本逐字节）。** [根 AXIOM: 5]
2. **规则与旧渲染器一致：33 个无断点字符算长 token；公共前缀按块结构相等；占位只显示主机名。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 通过：`empty input has no blocks; blank input falls back to its raw text`、`code blocks inside quotes are collected`、`long token threshold is 33 unbroken characters`、`common prefix stops at the first changed block`、`image placeholder names the host and keeps the full url in the notice`、`parse should parse headings, paragraphs, lists, and codes`、`parse math inlines and math blocks`、`parse empty string returns empty list`。`commonPrefix` 与 `imagePlaceholder` 是把旧成员内联代码摘成函数，逻辑逐行相同。
2. 空串与 null 无块；只有空白的文本退化为原文段落（旧行为，测试钉住）。

# 迁入或覆盖的旧测试
旧 UiPureLogicTests 第 3 节（原样）：
- `parse should parse headings, paragraphs, lists, and codes`
- `parse math inlines and math blocks`
- `parse empty string returns empty list`

本库新增：空白回落、引用内代码块、长 token 阈值、公共前缀、图片占位。
