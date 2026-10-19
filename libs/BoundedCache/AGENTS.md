# BoundedCache

合并包：BoundedCache、HighlightQueue、HljsHtml。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `BoundedCache`

## BoundedCache

带上限的线程安全记忆化（从旧 RichBackend.Cache 拆出）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`BoundedCache<'K,'V>(limit)`：`TryGet`、`Set`、`GetOrAdd`、`Clear`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **类型从 `RichBackend.Cache` 改名为 `BoundedCache`：`RichBackend` 模块（后端注册）留在界面。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

## 迁入或覆盖的旧测试
迁自旧 UiPureLogicTests（RichBackendAndKatexHtmlTests）（原文，只改名字）与本库补充的测试：
- `RichBackend Cache stores and evicts by capacity`
- `GetOrAdd 命中时不再计算`
- `Clear 清空全部`
- `并发写不越过上限也不抛`

## 成员 `HighlightQueue`

## HighlightQueue（库）

旧 `Rich/Highlight.fs`（22-75，enqueue / notifyOnce）后台着色的排队记账迁入为泛型 `HighlightQueue<'K>`：`TryClaim(key)`、`Finished(key, ok)`（返回这次完成是否负责安排合并通知）、`NotifyFired()`、`InFlightCount`，通知窗口 `NotifyWindowMs` 为 120。锁语义保持：在途集合与「通知已排上」共用一把锁，完成时先在锁内移出在途、再另进一次锁认领通知，与旧代码两段锁相同。Task.Run、延时、BoundedCache 缓存、HljsHtml 解析、Ready 事件仍归宿主。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一段代码不重复排队；一个窗口内的完成只通知一次。** [根 AXIOM: 5] 打开含很多代码块的旧对话不会连抖几秒。

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/highlightqueue.txt`；`tests/Tests.fs` 两条行为用例（含多线程争抢）。

## 迁入或覆盖的旧测试
旧 `AsyncHighlightTests.桌面端先给纯文本，后台算完再换成着色版`、`AsyncHighlightTests.流式进行中一次也不调用着色` 要真实后端与缓存，留在旧仓库；排队与合并通知规则由本库覆盖。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/OldHighlightQueue.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样证据较弱；规则另由 `tests/Tests.fs` 直接断言。

## 成员 `HljsHtml`

## HljsHtml

把 highlight.js 的 HTML 翻成带类别的文本片段，以及 scope → 配色类别（从旧 Rich/Highlight.fs 拆出纯逻辑）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`CodeTokenKind`、`CodeToken`、`HljsHtml.kindOfScope`、`HljsHtml.parseHtml`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **模块从 `Highlight` 改名为 `HljsHtml`：旧 UI 还留着 `Highlight.tokenize`（后台排队、缓存、重画通知），同名模块跨程序集会互相遮蔽。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过。

## 迁入或覆盖的旧测试
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
