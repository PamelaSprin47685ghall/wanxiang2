# ColorBlend

合并包：ColorBlend、ThemeTween。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ColorBlend`

## ColorBlend（库）

旧 `Controls/Interaction.fs` 的 `blendOver` 的分量计算迁入：`over`，颜色以 `(A, R, G, B)` 字节四元组表示。不是纯色画笔时原样返回底色的分支仍归 UI。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一对颜色，结果与旧实现逐分量相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] `tests/ColorBlend.Tests.fs` 通过：`opaque overlay replaces color but keeps base alpha`、`transparent overlay leaves the base`、`result keeps base alpha and each channel lies between base and overlay`、`more overlay alpha moves the channel monotonically toward the overlay`。

## 迁入或覆盖的旧测试
旧仓库没有直接测它的用例。本包新增：不透明覆盖保留底色 alpha、全透明不变、结果各通道落在底色与覆盖色之间、覆盖 alpha 越大越单调靠近覆盖色。

## 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。

## 成员 `ThemeTween`

## ThemeTween（库）

旧 `Design/Tokens.fs` 主题颜色补间（200-320，apply / advanceFrame / completeTransition）的记账迁入为泛型 `ThemeTween<'K>`（'K 是画笔的键）：`Apply(now, sameMode, durationMs, brushes)` 返回 `ThemeStart.Snap | Tween`，`Frame(now, durationMs)` 返回 `ThemeFrame`（要写的颜色、是否结束），`Complete()`，`IsRunning`，以及逐通道插值 `LerpChannel` / `Lerp`。时长 200 毫秒，帧间隔为其 1/12。中途再切换时以前一段的逻辑当前色为起点，两段不叠加。画笔、帧计时器、跨线程投递、代次与 `Changed` 事件仍归宿主。颜色插值自带，不用 ColorBlend：两者规则不同（这里四舍五入并裁剪、补间每通道含 alpha）；若宿主要组合两者，由组合者负责。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一串切换与帧时刻，颜色与旧实现相同。** [根 AXIOM: 5]
2. **后来的切换接管前一段。** 不靠「帧有没有跑」，按时间定出起点。

## SPEC
1. [LEMMA: 1, 2] `tests/ThemeTween.Tests.fs` 通过：`brushes tween to the target and land exactly at 200 ms`、`the latest switch takes over from the logical current color`、`reduced motion and replaying the same theme snap`。

## 迁入或覆盖的旧测试
旧 `UiStabilityTests.theme color transition tweens brushes and the latest switch takes over` 要真实画笔与 dispatcher，留在旧仓库；其中插值、接管与到位规则由本库覆盖。

