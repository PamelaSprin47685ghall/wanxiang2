# DialogsDesk（组合者）

ConfirmPane + PromptPane + TextEditPane + ConnectPane + SessionPrefsPane + ShortcutHelpPane，加上 FieldCheck、OverlayDesk、PairingCodeInput、PairingHint、ConnectionIssue、ConnectIssueText、HostCatalog、ToolSummary，等于旧 `Views/Dialogs.fs`。根规则见项目根目录 AGENTS.md。

- 宿主入口：`DialogsDesk(theme, overlay)`，成员有 `Prompt`、`Confirm`、`ConfirmWithFocus`、`EditText`、`Connect`、`ConnectWithClosed`（返回 `ConnectDeskHandle { Info; Fail; StartPairing; Pane }`）、`SessionSettings`、`Shortcuts`。它们对应旧 `Dialogs.prompt/confirm/confirmWithFocus/editText/connect/connectWithClosed/sessionSettings/shortcuts`，参数改成元组；连接请求类型是 `ConnectPane.ConnectAsk { Url; Token; UsePairing }`。
- 协作都在这里：保存往返与 pending 守卫（canDismiss）；空白校验和数字范围校验写入 FieldCheck；多处出错时显示摘要并弹提示条，同时展开生成参数；连接失败的重试键由 `ConnectionIssue.retry` 决定，码输错时不给重试键，而是全选重输；6 位码只自动提交一次（PairingAutoSubmit）；模型选择用 OverlayDesk.SelectButton。
- 宽度取自主题度量：ConfirmDialogWidth 用于确认和单行输入；ConnectDialogWidth 用于连接和快捷键帮助；SessionDialogWidth 用于对话设置和长文本编辑。
- 与旧实现的差别：Ctrl/⌘+Enter 在多行框里也能保存（面板用隧道处理）。

## 证据与旧测试

- DialogsTests：`prompt dialog validates input and handles confirm and cancel`、`confirm and confirmWithFocus dialog trigger callbacks correctly`、`editText dialog loads text and submits on confirm`、`connect dialog validates pairing code and token connections`、`pairing code box normalizes pasted text and auto-submits at six digits`、`sessionSettings dialog validates numeric bounds and updates config`、`shortcuts dialog displays sections and closes on button click`
