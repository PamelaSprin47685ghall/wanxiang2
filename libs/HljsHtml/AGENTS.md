# HljsHtml

把 highlight.js 的 HTML 翻成带类别的文本片段，以及 scope → 配色类别（从旧 Rich/Highlight.fs 拆出纯逻辑）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`CodeTokenKind`、`CodeToken`、`HljsHtml.kindOfScope`、`HljsHtml.parseHtml`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **模块从 `Highlight` 改名为 `HljsHtml`：旧 UI 还留着 `Highlight.tokenize`（后台排队、缓存、重画通知），同名模块跨程序集会互相遮蔽。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

# 迁入或覆盖的旧测试
迁自旧 PolishTests（原文，只改名字）与本库补充的测试：
- `span parser restores the source text exactly`
- `html entities are decoded back to source characters`
- `the innermost scope wins over the enclosing one`
- `unknown scopes inherit the enclosing kind instead of resetting to plain`
- `hljs sub scopes map through their dotted path`
- `a deep unregistered sub scope falls back to its first segment`
- `adjacent tokens of one kind are merged`
- `malformed markup never throws and never drops visible text`
- `空串得到空列表`
- `纯文本整段为 CodePlain`
- `多余的闭合标签不弹出外层之外的类别`
- `没有 class 的 span 继承外层`
- `class 引号没闭合时按无类别处理`
- `每个登记的 scope 都映射到一个类别`

另由以下覆盖：
- compose/RichJint 的 `着色与排版金样与旧实现逐字节相同`（golden/rich.txt 由旧 RichBackend.fs、Highlight.fs、KatexHtml.fs、RichJint.fs 原样编译生成）
