# ConfirmPane

合并包：ConfirmPane、ConnectPane、PromptPane、SessionPrefsPane、ShortcutHelpPane、TextEditPane。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ConfirmPane`

## ConfirmPane

破坏性操作确认的内容（迁自旧 `Views/Dialogs.fs` 的 confirmWithFocus / confirm）。开关对话框、Escape、关后执行动作归组合者。根规则见项目根目录 AGENTS.md。

- `ConfirmPane(title, body, confirmLabel, dangerFirst)`：`View`、`CancelButton`、`ConfirmButton`、`InitialFocus`（dangerFirst 为假时是取消，即安全项）。
- 事件：`Confirmed`、`Cancelled`。主键 Danger 语气，提示「确认“X”，此操作可能无法撤销」，读屏说明附正文。
- 没有行级 Enter/Space 确认：必须落在确认键上激活。对话框页脚按钮左右键互跳。

### 外观

- 样式类：`button-ghost`、`button-danger`、`dialog-title`、`dialog-message`。正文字号与行高绑定 `Metric.FontBody` 与 `Metric.UiBodyLineHeight`。

### 证据与旧测试

- DialogsTests：`confirm and confirmWithFocus dialog trigger callbacks correctly`
- CraftsmanshipPolishTests：`Dialogs confirm focuses the safe cancel action by default`

## 成员 `ConnectPane`

## ConnectPane

连接对话框的内容（迁自旧 `Views/Dialogs.fs` 的 connectWithClosed）：地址 + 令牌，或切到配对码。失败归类（ConnectionIssue）、重试种类、自动提交时机（PairingAutoSubmit）归组合者。根规则见项目根目录 AGENTS.md。

- `ConnectPane(defaultUrl, rules)`：`CodeRules` 给归一、字距、配对说明三个函数。
  - 成员：`View`、`UrlBox`、`TokenBox`、`CodeBox`、`ConnectButton`、`CodeButton`、`PairingToggle`、`RetryButton`、`InitialFocus`（地址空则地址，否则令牌）、`StatusText`、`IsPairing`、`Code`。
  - 动作：`Announce`（中性，无重试）、`Fail(message, retryLabel, refocusCode)`、`StartPairing`、`Connect`、`SubmitCode`、`ClearToken`。
  - 事件：`ConnectRequested ConnectAsk`、`CodeSubmitted`、`CodeIncomplete`、`CodeChanged`（归一后）、`RetryClicked`、`Cancelled`。
- 配对模式下令牌框与页脚「连接」退场，「提交配对码」是唯一主键；配对码框数字键盘 + Done、最多 6 位。

### 外观

- 样式类：`control-stroke`、`muted`、`danger`、`grouping-card`、`hairline`、`button-ghost`、`button-primary`、`button-secondary`。

### 证据与旧测试

- DialogsTests：`connect dialog validates pairing code and token connections`、`pairing code box normalizes pasted text and auto-submits at six digits`（归一部分；自动提交在组合者）
- DismissFocusTests：`connect focuses the first field that still needs input`
- 新增：类型化重试键与中性提示

## 成员 `PromptPane`

## PromptPane

单行输入对话框的内容（迁自旧 `Views/Dialogs.fs` 的 prompt，用于重命名等）。根规则见项目根目录 AGENTS.md。

- `PromptPane(title, hint, initial, confirmLabel, validationFor)`：`View`、`Box`、`CancelButton`、`ConfirmButton`；`SetPending`。
- 事件：`Submitted`（去空白的值；空白或保存中不发）、`Cancelled`、`FieldError`（全空白字符时的行内错误，None 为清除，组合者写进 FieldCheck）。
- 空输入时禁用确认并显示「内容不能为空，请输入有效文本。」；回车、确认键、页脚行级 Enter 都提交；焦点在取消上的行级 Enter/Space 只取消。

### 外观

- 样式类：`control-stroke`、`muted`、`button-ghost`、`button-primary`。

### 证据与旧测试

- DialogsTests：`prompt dialog validates input and handles confirm and cancel`
- 新增：`prompt footer arrows and row enter follow the old action row`

## 成员 `SessionPrefsPane`

## SessionPrefsPane

对话设置的内容（迁自旧 `Views/Dialogs.fs` 的 sessionSettings）。模型选择键由组合者给（OverlayDesk.SelectButton）；数字校验、载荷、保存往返与提示条归组合者。根规则见项目根目录 AGENTS.md。

- `SessionPrefsPane(modelPicker, init, validationFor)`：`View`、四个参数框与系统指令框、`SaveButton`、`CancelButton`、`Parameters`、`ToolsExpander`、`Summary`。
  - 动作：`ShowSummary`（多错摘要，任一参数框改动即收起）、`RevealParameters`、`SetPending`。
  - 事件：`SaveRequested SessionPrefsDraft`（原始输入 + 选中工具 id）、`Cancelled`。Ctrl/⌘ + Enter 与页脚行级 Enter 保存。
- 工具折叠头「工具 · 已启用 N 项」在展开 / 收起时更新，不重建开关。

### 外观

- 样式类：`control-stroke`、`text-area`、`muted`、`button-ghost`、`button-primary`。最大高度绑定 `Metric.DialogContentMaxHeight`。

### 证据与旧测试

- DialogsTests：`sessionSettings dialog validates numeric bounds and updates config`（界面部分；数字界限在组合者）
- 新增：工具开关改变保存集合与计数

### 与旧实现的差异

- 参数区用两列均分网格；旧 `twoColumnForm` 在窄宽下改单列，本库未做（对话框宽 520 下总是两列，与旧观感一致）。
- Ctrl/⌘ + Enter 在隧道阶段接（旧实现在系统指令框里按会先插换行）。

## 成员 `ShortcutHelpPane`

## ShortcutHelpPane

快捷键帮助的内容（迁自旧 `Views/Dialogs.fs` 的 shortcuts）。分组条目由组合者给（旧原文在组合者）。根规则见项目根目录 AGENTS.md。

- `ShortcutHelpPane(sections)`：`View`（可滚动，上限跟主题）、`CloseButton`、`InitialFocus`；事件 `Closed`。唯一停留点是关闭键，无内容级 Enter。

### 外观

- 样式类：`grouping-card`、`button-primary`。最大高度绑定 `Metric.DialogContentMaxHeight`。

### 证据与旧测试

- DialogsTests：`shortcuts dialog displays sections and closes on button click`

## 成员 `TextEditPane`

## TextEditPane

长文本编辑的内容（迁自旧 `Views/Dialogs.fs` 的 editText：编辑消息并分叉）。根规则见项目根目录 AGENTS.md。

- `TextEditPane(title, initial, confirmLabel)`：`View`、`Box`、`CancelButton`、`ConfirmButton`；`FocusEnd()`（聚焦并把光标放末尾）。
- 事件：`Submitted`（原文）、`Cancelled`。Ctrl/⌘ + Enter 或对话框页脚提交。

### 外观

- 样式类：`control-stroke`、`text-area`、`muted`、`button-ghost`、`button-primary`。

### 证据与旧测试

- DialogsTests：`editText dialog loads text and submits on confirm`

### 与旧实现的差异

- Ctrl/⌘ + Enter 在隧道阶段接：旧实现冒泡接时多行框先插入换行，提交的文字尾部多一个换行。
