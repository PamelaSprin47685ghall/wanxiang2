# CopyConfirm（库）

旧 `Views/MessageCard.fs` 两处复制按钮（160-230、975-1030）与 `Controls/MarkdownRenderer.fs` 代码块复制按钮（276-330）共用的确认记账迁入：`Copied(now, reduced)`、`Tick(now)` 返回 `CopyVisual.Default | Confirmed | Fading`、`NextDue`、`Reset`、`Detached`。保持 `HoldMs` 1500，淡出 `FadeMs` 150，减弱动效时不淡出。计时器、图标、提示文字、读屏名、透明度过渡仍归宿主。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串操作，形态与旧实现相同。** [根 AXIOM: 5] 复制后先淡出、保持勾号、到点复位；再次复制重新起算；离开可视树立即复位。

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/copyconfirm.txt`；`tests/Tests.fs` 四条行为用例。

# 迁入或覆盖的旧测试
旧 `CraftsmanshipPolishTests.Markdown code block copy button confirms visually with check icon and tooltip`、`PresentationRefreshTests.code block copy confirmation dips toward fade opacity and keeps the confirmed label` 要起窗口查图标与透明度，留在旧仓库；它们背后的时序规则由本库金样与行为用例覆盖。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/OldCopyConfirm.fs`（照抄旧计时器结构）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的真实行为测试直接断言。
