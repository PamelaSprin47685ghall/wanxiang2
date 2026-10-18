# ConfirmPane

破坏性操作确认的内容（迁自旧 `Views/Dialogs.fs` 的 confirmWithFocus / confirm）。开关对话框、Escape、关后执行动作归组合者。根规则见项目根目录 AGENTS.md。

- `ConfirmPane(look, title, body, confirmLabel, dangerFirst)`：`View`、`CancelButton`、`ConfirmButton`、`InitialFocus`（dangerFirst 为假时是取消，即安全项）。
- 事件：`Confirmed`、`Cancelled`。主键 Danger 语气，提示「确认“X”，此操作可能无法撤销」，读屏说明附正文。
- 没有行级 Enter/Space 确认：必须落在确认键上激活。对话框页脚（来自 UiKit 的 `Kit.DialogFooter`）左右键互跳。可点件是 `Tap`（Invoke）。

## 外观

- `ConfirmPaneLook`：公共字段加 `BodyLineHeight`。样式类：`button-ghost`、`button-danger`。

## 证据与旧测试

- DialogsTests：`confirm and confirmWithFocus dialog trigger callbacks correctly`
- CraftsmanshipPolishTests：`Dialogs confirm focuses the safe cancel action by default`
