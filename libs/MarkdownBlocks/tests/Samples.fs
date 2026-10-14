module Samples
let markdown = [
  "# Title\n\nSome **bold** and *italic* and ~~strike~~ and `code`."
  "## H2 with [link](https://example.com/a?b=1) and ![alt](https://img.example.com/x.png)"
  "- a\n- b\n  - nested\n- c"
  "1. one\n2. two\n10. ten"
  "- [x] done\n- [ ] todo"
  "> quote line\n> > nested quote\n\n> second"
  "```fsharp\nlet x = 1\n```\n\n```\nplain\n```"
  "    indented code"
  "---"
  "| a | b |\n|---|:-:|\n| 1 | **2** |\n| `c` | [d](u) |"
  "Inline $E=mc^2$ math and display:\n\n$$\n\\int_0^1 x\\,dx\n$$"
  "line one  \nline two\nsoft"
  "中文段落，含**强调**与链接 <https://例子.中国/路径>。"
  ""
  "   "
  "***bold italic*** __u__"
  "<div>html block</div>\n\ntext"
  "Term\n: definition"
  "a | b\n-- | --\nx | y"
  "1) paren list\n2) next"
  "* star\n+ plus"
  "```\nunterminated fence"
  "Footnote[^1].\n\n[^1]: note"
  "> - quoted list\n>   ```\n>   code in quote\n>   ```"
  "$$x^2$$ inline display"
]
let longText = [
  "https://example.com/" + System.String('a', 40)
  "short words only here"
  "x" + System.String('y', 32)
  "x" + System.String('y', 31)
  "a\u200B" + System.String('b', 33)
]
let chunkInputs = [
  "", 5
  "abc", 0
  "abcdefghij", 3
  "👨‍👩‍👧‍👦家庭e\u0301x", 2
  "https://example.com/路径，含标点。（括号）后", 4
  "a。b", 1
  "(abc)[def]", 3
  "“引号”‘单’", 2
]
let images = [ "", "https://img.example.com/x.png"; "logo", "relative/path.png"; "  ", "data:image/png;base64,AAAA"; "图", "https://例子.中国/a.png" ]
