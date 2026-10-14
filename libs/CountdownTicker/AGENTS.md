# CountdownTicker（库）

旧 `Views/MessageCard.fs` 限流倒计时（1109-1150）的刷新记账迁入：`Refresh(left)` 返回 `CountdownFrame`（剩余秒数、是否走完、是否该播报、计时器是否继续），`Left` 记最近一次剩余秒数，`IntervalMs` 为 1000。剩余秒数仍由宿主用 `RetryCountdown.remainingSeconds` 按截止时刻算；计时器、文字、颜色、读屏属性仍归 MessageCard。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **走完只播报一次，走完就停表。** [根 AXIOM: 5] 读屏用户只在可以重试那一刻听到一次，之后反复刷新不再打扰。

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/countdownticker.txt`；`tests/Tests.fs` 行为用例。

# 迁入或覆盖的旧测试
旧 `A11yRegressionTests.限流倒计时读屏名带剩余时间、走完才播报一次`、`ModelChipUiTests.rate limited error card shows a live countdown instead of a static hint`、`JourneyR9Tests.262 限流倒计时未结束时重试按钮不可按并显示剩余秒数` 要起窗口查控件，留在旧仓库；其中「只播报一次、走完停表」的规则由本库覆盖。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/OldCountdownTicker.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样证据较弱；规则另由 `tests/Tests.fs` 直接断言。
