module Samples

/// 金样输入（旧代码生成器与新组合测试共用）：名字、Markdown 原文。
let all : (string * string) list =
    [ "headings", "# 一级标题\n\n## Second level\n\n### 三级\n\n#### 四\n\n正文第一段。"
      "inline", "普通 **加粗** *斜体* ~~删除~~ `inline code` 与 ***混合*** 文字。\n第二行  \n硬换行之后。"
      "links", "见 [文档](https://example.com/docs) 与 [一个特别特别长的链接文字用来测试分段换行的行为是否正确abcdefghijklmnopqrstuvwxyz](https://example.com/very/long/path?query=1)。"
      "image", "前文 ![示意图](https://img.example.org/a.png) 后文"
      "math", "行内 $x^2$ 与 $\\frac{a}{b}$ 公式。\n\n$$\\sum_{i=1}^{n} i = \\frac{n(n+1)}{2}$$\n\n坏公式 $\\frac{$ 结束。"
      "lists", "- 一\n- 二\n  - 嵌套\n    - 更深\n- 三\n\n1. first\n2. second\n10. tenth"
      "tasks", "- [x] 已完成\n- [ ] 未完成 `code`"
      "quote", "> 引用第一段\n>\n> > 嵌套引用\n>\n> 尾段"
      "code", "```fsharp\nlet x = 1\nlet y = \"s\"\n```\n\n```\nplain text block\n```"
      "hugecode", "```python\n" + String.concat "\n" [ for i in 1 .. 130 -> sprintf "print(%d)" i ] + "\n```"
      "rule", "上\n\n---\n\n下"
      "table", "| 名称 | 数量 | 价格 |\n|---|---|---|\n| 苹果 | 3 | $1.50 |\n| 梨 | 12 | 2,000 |\n| 桃 |  | -5% |"
      "widetable", "| a | b | c | d | e | f |\n|---|---|---|---|---|---|\n| 1 | 2 | 3 | 4 | 5 | 6 |\n| x | y | z | w | v | u |"
      "longtoken", "| key | value |\n|---|---|\n| hash | 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef |"
      "tablelinks", "| 链接 | 说明 |\n|---|---|\n| [x](https://x.y) | **粗** |"
      "mixed", "段落一\n\n```\nx\n```\n\n> q\n\n- a\n\n| h |\n|---|\n| 1 |" ]
