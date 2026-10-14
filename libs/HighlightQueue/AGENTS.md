# HighlightQueue（库）

旧 `Rich/Highlight.fs`（22-75，enqueue / notifyOnce）后台着色的排队记账迁入为泛型 `HighlightQueue<'K>`：`TryClaim(key)`、`Finished(key, ok)`（返回这次完成是否负责安排合并通知）、`NotifyFired()`、`InFlightCount`，通知窗口 `NotifyWindowMs` 为 120。锁语义保持：在途集合与「通知已排上」共用一把锁，完成时先在锁内移出在途、再另进一次锁认领通知，与旧代码两段锁相同。Task.Run、延时、BoundedCache 缓存、HljsHtml 解析、Ready 事件仍归宿主。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一段代码不重复排队；一个窗口内的完成只通知一次。** [根 AXIOM: 5] 打开含很多代码块的旧对话不会连抖几秒。

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/highlightqueue.txt`；`tests/Tests.fs` 两条行为用例（含多线程争抢）。

# 迁入或覆盖的旧测试
旧 `AsyncHighlightTests.桌面端先给纯文本，后台算完再换成着色版`、`AsyncHighlightTests.流式进行中一次也不调用着色` 要真实后端与缓存，留在旧仓库；排队与合并通知规则由本库覆盖。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/OldHighlightQueue.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样证据较弱；规则另由 `tests/Tests.fs` 直接断言。
