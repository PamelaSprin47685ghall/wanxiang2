# ScrollTween（库）

旧 `Views/ChatView.fs` 三处平滑滚动（365-400 smoothScrollToCard，1370-1450 SmoothScrollToBeginning 与 SmoothScrollToEnd）的补间记账迁入：`Start(now, offset, target, reduced)`、`Frame(now, target)` 返回 `ScrollFrame.Offset | Done`、`Cancel`、`Pin`、`IsOwnOffset`，以及旧 `MotionPolicy.EaseOutCubicEasing` 的曲线数学 `EaseOutCubic.ease`。时长 180 毫秒，帧 16 毫秒，离目标不到 5 像素不做动画。帧计时器、ScrollViewer、ScrollToEnd、跟随状态仍归 ChatView。不引用任何其他库。根规则见项目根目录 AGENTS.md。

宿主分工：到顶补间每帧传目标 0；到底补间每帧传 `max 0 (extent - viewport)`，目标 ≤ 0 时宿主自己 `Cancel` 并收尾（旧判据 `target <= 0.0`）；对准卡片的补间 Done 后宿主调 `Pin target`（旧 `lastAnimatedOffset <- target`），另两种 Done 不 Pin，与旧代码相同。

# LEMMA
1. **同一串时刻与目标，偏移与旧实现相同。** [根 AXIOM: 5]
2. **用户滚轮能打断动画。** `IsOwnOffset` 区分动画写下的偏移与用户滚动。

# SPEC
1. [LEMMA: 1, 2] 金样 `tests/golden/scrolltween.txt`（含曲线取样）；`tests/Tests.fs` 四条行为用例。

# 迁入或覆盖的旧测试
旧 `InteractabilityPolishTests.wheel moving either way stops an in-flight smooth scroll`、`EndpointScrollTests.scroll to beginning lands at offset zero with the first round visible`、`PresentationRefreshTests.disclosure chevron and copy confirmation ride the shared easeOutCubic`（曲线部分）要起窗口，留在旧仓库；补间与曲线数学由本库覆盖。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/OldScrollTween.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样证据较弱；规则另由 `tests/Tests.fs` 直接断言。
