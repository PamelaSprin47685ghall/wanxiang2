# ApprovalPanel

工具审批卡与卡宿主（迁自旧 `Views/ToolApprovalCard.fs`：`ToolApprovalCard.build` → `ApprovalCard.build`，`ToolApprovalPanel` → `ApprovalPanel`）。根规则见项目根目录 AGENTS.md。

- 批准 / 拒绝只发一次，随后两键不可用并在 polite 状态行显示「已发送」文案；参数默认收起，「查看参数」展开全文；没有参数（空或 `{}`）不显示该键。
- 按钮是可聚焦 Border（读屏 Button + Invoke，Enter / Space / 左键），命中高度不低于 Look 的触控下限，两键等宽、行宽封顶。
- 文案是插槽 `ApprovalTexts`，组合者填 ApprovalCardText 的 `policyNote`、`sentText`、`displayArguments`、`argsToggleText`；固定标题「模型想使用工具，需要你批准」与读屏名格式照旧。
- AutomationId 与旧代码相同：`tool-approval-summary`、`-status`、`-approve`、`-deny`、`-args`、`-args-toggle`、`tool-approval-<id>`。

## 外观

- `ApprovalPanelLook`：画笔 `Text`、`Muted`、`Faint`、`Warning`；图标插槽 `Wrench`；字号、行高、等宽字体、触控下限、按钮行最大宽度、间距、卡内边距与圆角都是活的值。
- 用到的样式类（compose/Theme）：卡 `grouping-card`；批准 `surface button-primary hover-accent`；拒绝 `surface button-secondary hover-raised`；查看参数 `surface button-ghost hover-plain`。不可用态的透明度由主题 `:disabled` 给。

## 证据与旧测试

- W3ToolApprovalUiTests：`审批卡：展示人话摘要，批准与拒绝各只触发一次并立即置灰`、`审批面板：只显示当前对话的卡片，已处理即移除，断线清空`（`审批卡：说明批准的后果` 是纯文案，在 ApprovalCardText）
- PwaToolApprovalTests：`审批卡：批准/拒绝/查看参数的命中高度不低于触控下限`、`审批卡：手机宽度下摘要换行而不溢出，两个按钮等宽且都在卡内`、`审批卡：触屏没有悬停提示，点「查看参数」展开完整参数，再点收起`、`审批卡：没有参数时不显示「查看参数」`、`审批面板：放在输入框正上方的附属位，与输入框同列同宽，不浮层遮挡消息`（面板本身部分；进 Composer 附属位等 Composer 迁出后在组合者测）
- VisualPassR3Tests：`审批面板汇报待批准的 callId`（`AwaitingCallIds`，并入面板用例）

## 与旧实现的差异

- 记录类型改名 `ApprovalAsk`，字段大写开头。
- 不可用态不再手写 Opacity / IsHitTestVisible，交给主题 `:disabled`。
