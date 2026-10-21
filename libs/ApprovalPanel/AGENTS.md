# ApprovalPanel

合并包：ApprovalPanel、ApprovalCardText。2026-10-21（W7-CLEANBREAK）并入 ApprovalCardText：生产消费者集合相同（`compose/ApprovalDesk`），且合并后的依赖并集（`Avalonia`）已被该消费者直接声明——按 MERGE.md《追加判据（2026-10-21 W7-CLEANBREAK）：依赖闭包判据》合并是纯减法，消费者不多拿任何 API。

工具审批卡与卡宿主（迁自旧 `Views/ToolApprovalCard.fs`：`ToolApprovalCard.build` → `ApprovalCard.build`，`ToolApprovalPanel` → `ApprovalPanel`）。根规则见项目根目录 AGENTS.md。

- 批准 / 拒绝只发一次，随后两键不可用并在 polite 状态行显示「已发送」文案；参数默认收起，「查看参数」展开全文；没有参数（空或 `{}`）不显示该键。
- 原生 Avalonia `Button` 控件，命中高度不低于 44px（触控下限），两键等宽、行宽封顶。
- 文案是插槽 `ApprovalTexts`，组合者填 ApprovalCardText 的 `policyNote`、`sentText`、`displayArguments`、`argsToggleText`；固定标题「模型想使用工具，需要你批准」与读屏名格式照旧。
- AutomationId 与旧代码相同：`tool-approval-summary`、`-status`、`-approve`、`-deny`、`-args`、`-args-toggle`、`tool-approval-<id>`。
- 单入口幂等渲染：`Render(items, currentConversation)`，单出口意图事件 `Decide(Guid * bool)`；切换当前对话走幂等单槽 `ApplyConversation(Guid option)`（仅在值变化时刷新）。

## 外观

- 样式类（compose/Theme）：卡 `grouping-card`；批准 `primary`；拒绝 `secondary`；查看参数 `ghost`。废除 `ApprovalPanelLook`，样式与主题全部由 Avalonia 样式类与主题系统提供。

## 证据与旧测试

- W3ToolApprovalUiTests：`审批卡：展示人话摘要，批准与拒绝各只触发一次并立即置灰`、`审批面板：只显示当前对话的卡片，已处理即移除，断线清空`（`审批卡：说明批准的后果` 是纯文案，在 ApprovalCardText）
- PwaToolApprovalTests：`审批卡：批准/拒绝/查看参数的命中高度不低于触控下限`、`审批卡：手机宽度下摘要换行而不溢出，两个按钮等宽且都在卡内`、`审批卡：触屏没有悬停提示，点「查看参数」展开完整参数，再点收起`、`审批卡：没有参数时不显示「查看参数」`、`审批面板：放在输入框正上方的附属位，与输入框同列同宽，不浮层遮挡消息`（面板本身部分；进 Composer 附属位等 Composer 迁出后在组合者测）
- VisualPassR3Tests：`审批面板汇报待批准的 callId`（`AwaitingCallIds`，并入面板用例）

## 与旧实现的差异

- 记录类型改名 `ApprovalAsk`，字段大写开头。
- 不可用态不再手写 Opacity / IsHitTestVisible，交给主题 `:disabled`。

## 成员 `ApprovalCardText`

### ApprovalCardText（库）

旧 `Views/ToolApprovalCard.fs` 里不碰控件的部分：参数 JSON 的展示（中文不转义）、按钮与状态文案、策略说明。模块改名为 `ApprovalCardText`，避免与留在 UI 的 `ToolApprovalCard` 模块同名而互相遮蔽。`touchTarget`、`buttonRowMaxWidth` 与 `build` 留在 UI。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **全部文案与参数展示与旧实现逐字相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过：`escaped chinese in arguments is shown as is`、`unparsable or blank arguments come back unchanged`、`unknown policy and status fall back to the generic text`、`other conversation waiting text is the old toast`。

### 迁入或覆盖的旧测试
旧 W3ToolApprovalUiTests 的用例要点击按钮，依赖 Avalonia，留在旧仓库。本库新增：中文转义还原、无法解析或空白参数原样返回、未知策略与状态回落、他人对话等待文案。
- 迁自旧 AppShell.HandleEvent 的自足部分（见 tests 里与旧代码原样摘录的差分用例）。

### 合并说明

2026-10-21（W7-CLEANBREAK）并入本包：源文件 `ApprovalCardText.fs` 落盘为本包源文件（编译序在 `ApprovalPanel.fs` 之后）；测试按 MERGE.md《测试工程的合并约定》改名 `tests/ApprovalCardText.Tests.fs`，顶层模块改名 `ApprovalCardTextTests`；原 `Wanxiang2.ApprovalCardText` 包不再交付，消费方 `compose/ApprovalDesk` 改引 `Wanxiang2.ApprovalPanel`。命名空间与类型名不变，消费方源码零改。
