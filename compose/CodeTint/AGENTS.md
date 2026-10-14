# CodeTint（组合，代码着色的界面一侧）

旧 `Rich/Highlight.fs`（extract-ledger@0c0b2a6）：语言解析、记忆化、后台排队与合并重画通知。模块级状态改成 `CodeTint` 实例。协作者（包）：HljsHtml（HTML 解析）、RichAssets（语言名）、BoundedCache（192 条记忆化）、HighlightQueue（在途与 120 毫秒合并通知）。脚本后端（RichBackend 接口与 App/Pwa 适配）、线程与计时器都留在宿主，经 `CodeTintHost` 传入。根规则见项目根目录 AGENTS.md。

## 接口
- `CodeTintHost`：`HighlightHtml`（后端「代码进、hljs HTML 出」）、`Background`、`After ms`、`Synchronous`（浏览器端当场算）。
- `CodeTint(host)`：`Tokenize(allowHighlight, language, code)`、`Ready` 事件。

# LEMMA
1. **流式期间不着色；同一段只算一次；二十段同时完成只通知一次。** [根 AXIOM: 5]
2. **认不出的语言、后端失败，退化为整段纯文本，不丢字。**

# SPEC
1. [LEMMA: 1] `tests/Tests.fs`（宿主可控：后台与延时都排进队列逐个放行）：`桌面端先给纯文本，后台算完再换成着色版`、`流式进行中一次也不调用着色`、`同一段代码只排队一次`、`多段代码同时算完只通知一次`、`synchronous host colors on the spot and caches`。
2. [LEMMA: 2] `认不出的语言不进后台队列`、`empty code yields no tokens and a failing backend falls back to plain`、`instances do not share caches`。

# 与旧代码的差异
- 旧代码用 `Task.Run` 与 `Task.Delay` 自己起线程和计时；这里由宿主的 `Background` 与 `After` 决定。
- 旧代码按 `OperatingSystem.IsBrowser()` 选同步；这里由宿主的 `Synchronous` 决定。
- 旧代码没装后端时不排队；这里后端由 `HighlightHtml` 表达，没有后端的宿主传「总给 None」，此时会排一次队、得到纯文本并缓存。

# 迁入或覆盖的旧测试
旧 AsyncHighlightTests：`桌面端先给纯文本，后台算完再换成着色版`、`流式进行中一次也不调用着色`、`同一段代码只排队一次`、`认不出的语言不进后台队列`、`多段代码同时算完只通知一次`（同名迁入，改成可控宿主，不靠墙钟）。`a highlight finishing after the ui test ended does not create a dispatcher off the ui thread` 讲的是宿主切回 UI 线程的方式，留在宿主。
