# RichJint（组合）

旧 `Wanxiang.App.JintRichBackend`：桌面端在进程内跑 hljs 与 KaTeX。协作者：JintScriptHost、RichAssets。不再实现界面的 IRichBackend（组合者不引用界面）：旧 App 入口用一个对象表达式转接。库只以包的形式引用。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **着色与排版的输出与旧实现逐字节相同。** [根 AXIOM: 5]
2. **加载次序、按需语言、失败一律退化为 None 的规则归本组合者。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1, 2] tests 通过。
2. `golden/rich.txt`：旧代码原样编译（RichBackend.fs、Highlight.fs、KatexHtml.fs、RichJint.fs + 旧 Assets/Js），跑 tests/Scenario.fs 的 17 段代码与 12 个公式，记下语言解析、hljs HTML、token、KaTeX HTML、KNode 树；新实现逐字节相同。生成器的 Api.fs 只把同名函数接到旧 API 上，Golden.fs、Scenario.fs 与本目录相同。

# 迁入或覆盖的旧测试
金样与本组合补充：
- `着色与排版金样与旧实现逐字节相同`
- `认不出的语言不抛，返回 None`
- `预热之后结果不变，预热可重复`
- `后台预热与 UI 线程着色并发也安全`
