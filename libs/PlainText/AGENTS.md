# PlainText

把 Markdown 压成单行纯文本，给会话标题、侧栏预览这类纯文本位置用。纯函数，只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`PlainText.ofMarkdown`、`PlainText.summarize maxChars text`。

# LEMMA
1. **纯文本位置不露出标记，截断不切坏字符。** [根 AXIOM: 5]
    - 推理：用户在侧栏看到 `###`、`**` 会以为渲染坏了；截断时切开代理对会显示成乱码方块。

# SPEC
1. [LEMMA: 1] 标题号、强调、行内代码、链接网址、围栏代码都去掉，文字保留，没有换行：`tests/Tests.fs` `markdown is flattened for plain-text surfaces`，通过。
2. [LEMMA: 1] 截断加省略号；1 到 12 的任意长度都不切开代理对：`plain-text summary truncates with an ellipsis`、`summary never splits a surrogate pair`，通过。
