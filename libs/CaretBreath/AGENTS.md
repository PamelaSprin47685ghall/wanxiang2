# CaretBreath（库）

旧 `Views/MessageCard.fs` 的流式光标呼吸数学 `caretBreathPhase`、`caretBreathWave`、`caretBreathOpacity` 迁入，改为以毫秒为参数的纯函数。运行原点与单调时钟（beginBreathRun、breathElapsed）仍归 MessageCard；常量由调用方传入：MotionLedger.caretBreathPhase 为 900，Tokens.opacityCaretBreathMin 与 Max 为 0.55 与 1.0。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一流逝时间，相位、波与不透明度和旧实现相同（12 位小数）。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/caretbreath.txt`。
2. t=0 时在峰值，半周期时在下限，结果始终落在 [lo, hi] 内。

# 迁入或覆盖的旧测试
旧呼吸连续性用例（beginBreathRunAtElapsedMs 接缝）要起窗口，留在旧仓库。本库新增：金样与 2 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
