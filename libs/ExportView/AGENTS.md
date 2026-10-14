# ExportView（库）

旧 `Views/ExportDialog.fs` 的 RenderProgress（状态文字、颜色档、进度、按键）与 Refresh 节流决定迁入：`decide`、`refresh`。读取阶段用本库自己的 `ExportPhase`（与旧 ConversationExport 状态同形）。计时器、文件保存、焦点兜底仍归 ExportDialog。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组状态，呈现与节流决定和旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/exportview.txt`。
2. 只有读取中受节流（窗口由 MotionLedger.progressTextThrottle 给）；读完后两种格式都能再存；空对话或保存中不能存；减弱动效时没有不确定进度条。
3. 已知旧行为，原样保留：读完但还没有输出时，重试键可见。

# 迁入或覆盖的旧测试
旧导出用例要起窗口，留在旧仓库。本库新增：金样与 3 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
