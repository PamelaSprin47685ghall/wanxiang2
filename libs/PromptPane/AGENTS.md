# PromptPane

单行输入对话框的内容（迁自旧 `Views/Dialogs.fs` 的 prompt，用于重命名等）。根规则见项目根目录 AGENTS.md。

- `PromptPane(look, title, hint, initial, confirmLabel, validationFor)`：`View`、`Box`、`CancelButton`、`ConfirmButton`、`InitialFocus`；`SetPending`。
- 事件：`Submitted`（去空白的值；空白或保存中不发）、`Cancelled`、`FieldError`（全空白字符时的行内错误，None 为清除，组合者写进 FieldCheck）。
- 空输入时禁用确认并显示「内容不能为空，请输入有效文本。」；回车、确认键、页脚（来自 UiKit 的 `Kit.DialogFooter`）行级 Enter 都提交；焦点在取消上的行级 Enter/Space 只取消。

## 外观

- `PromptPaneLook`：公共字段加 `PendingMinWidth`。样式类：`button-ghost`、`button-primary`、输入壳。

## 证据与旧测试

- DialogsTests：`prompt dialog validates input and handles confirm and cancel`
- 新增：`prompt footer arrows and row enter follow the old action row`
