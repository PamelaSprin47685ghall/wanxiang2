# TextEditPane

长文本编辑的内容（迁自旧 `Views/Dialogs.fs` 的 editText：编辑消息并分叉）。根规则见项目根目录 AGENTS.md。

- `TextEditPane(look, title, initial, confirmLabel)`：`View`、`Box`、`CancelButton`、`ConfirmButton`；`FocusEnd()`（聚焦并把光标放末尾）。
- 事件：`Submitted`（原文）、`Cancelled`。Ctrl/⌘ + Enter 提交。

## 外观

- `TextEditPaneLook`：公共字段加 `TextAreaLongMinHeight`。

## 证据与旧测试

- DialogsTests：`editText dialog loads text and submits on confirm`

## 与旧实现的差异

- Ctrl/⌘ + Enter 在隧道阶段接：旧实现冒泡接时多行框先插入换行，提交的文字尾部多一个换行。
