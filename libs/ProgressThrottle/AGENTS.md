# ProgressThrottle（库）

旧 `Views/ExportDialog.fs` 进度文字节流（54-60 字段，105-150 Refresh / RenderProgress）的记账迁入：`Request(now, force, highFrequency)` 返回 `ProgressRefresh.Now | Schedule | Skip`，`Rendered(now)`，`TailFired()`，`Pending`，窗口 `WindowMs` 为 100。判据与 ExportView 库里的 `ExportView.refresh` 相同；那里是无状态判据，这里连同两个状态字段一起拥有。宿主接上本库后，`ExportView.refresh` 即可删去（由集成方决定）。尾巴计时器、控件与导出控制器仍归 ExportDialog。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **高频进度合并，终态不延迟。** [根 AXIOM: 5] 分页到达比人眼快，合并后仍补上最后计数；完成、失败、取消立即显示。

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/progressthrottle.txt`；`tests/Tests.fs` 两条行为用例。

# 迁入或覆盖的旧测试
旧 `ConversationExportTests.export of an unopened conversation has its own progress view without navigating`、`PresentationRefreshTests.export progress bar is determinate under reduced motion` 要起对话框，留在旧仓库；节流规则由本库覆盖。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/OldProgressThrottle.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样证据较弱；规则另由 `tests/Tests.fs` 直接断言。
