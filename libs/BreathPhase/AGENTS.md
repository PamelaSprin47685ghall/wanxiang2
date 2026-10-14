# BreathPhase（库）

旧 `Views/ChatView.fs` 骨架屏呼吸（940-975 StartSkeletonBreathing）的相位记账迁入：`Start(reduced, attached)` 决定开不开始并把相位归零，`Step()` 走一拍返回透明度（0.75 + 0.2·sin，每拍 0.12 弧度），`FrameMs` 为 50。与 CaretBreath（按时间算的光标呼吸）分开：两者曲线、驱动方式都不同。计时器、减弱动效时的静态透明度（旧 Tokens.skeletonOpacityReduced）仍归 ChatView。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串拍子，透明度与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/breathphase.txt`；`tests/Tests.fs` 两条行为用例。

# 迁入或覆盖的旧测试
旧 `CraftsmanshipPolishTests.ChatView skeleton loading transitions toggle skeletonPanel and messagePanel visibility` 要起窗口，留在旧仓库；呼吸数学由本库覆盖。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/OldBreathPhase.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样证据较弱；规则另由 `tests/Tests.fs` 直接断言。
