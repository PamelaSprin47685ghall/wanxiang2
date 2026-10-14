# JintScriptHost

进程内 JS 宿主（Jint）：垫平 console、同一脚本只加载一次、绑定变量后求值、所有调用串行（从旧 RichJint.fs 拆出引擎部分）。依赖 Jint。根规则见项目根目录 AGENTS.md。

公开面：`JintScriptHost()`：`LoadOnce(key, source)`、`IsLoaded`、`EvaluateString(expr, bindings)`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **它不知道 hljs 或 KaTeX；递归上限 10000 层与旧实现相同（默认 1MB 栈上更早溢出，见测试）。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

# 迁入或覆盖的旧测试
本库测试（新写，边界与失败路径）：
- `同一个键只执行一次，源码只在需要时取`
- `绑定的值原样进入脚本，引号不需要转义`
- `console 被垫平，调用不抛`
- `脚本异常原样抛给调用方，之后仍可用`
- `无限递归被递归上限拦下并抛出`

另由以下覆盖：
- compose/RichJint 的 `着色与排版金样与旧实现逐字节相同`（golden/rich.txt 由旧 RichBackend.fs、Highlight.fs、KatexHtml.fs、RichJint.fs 原样编译生成）
