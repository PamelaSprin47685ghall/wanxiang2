# ThemeTween（库）

旧 `Design/Tokens.fs` 主题颜色补间（200-320，apply / advanceFrame / completeTransition）的记账迁入为泛型 `ThemeTween<'K>`（'K 是画笔的键）：`Apply(now, sameMode, durationMs, brushes)` 返回 `ThemeStart.Snap | Tween`，`Frame(now, durationMs)` 返回 `ThemeFrame`（要写的颜色、是否结束），`Complete()`，`IsRunning`，以及逐通道插值 `LerpChannel` / `Lerp`。时长 200 毫秒，帧间隔为其 1/12。中途再切换时以前一段的逻辑当前色为起点，两段不叠加。画笔、帧计时器、跨线程投递、代次与 `Changed` 事件仍归宿主。颜色插值自带，不用 ColorBlend：两者规则不同（这里四舍五入并裁剪、补间每通道含 alpha）；若宿主要组合两者，由组合者负责。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串切换与帧时刻，颜色与旧实现相同。** [根 AXIOM: 5]
2. **后来的切换接管前一段。** 不靠「帧有没有跑」，按时间定出起点。

# SPEC
1. [LEMMA: 1, 2] 金样 `tests/golden/themetween.txt`；`tests/Tests.fs` 三条行为用例。

# 迁入或覆盖的旧测试
旧 `UiStabilityTests.theme color transition tweens brushes and the latest switch takes over` 要真实画笔与 dispatcher，留在旧仓库；其中插值、接管与到位规则由本库覆盖。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/OldThemeTween.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样证据较弱；规则另由 `tests/Tests.fs` 直接断言。
