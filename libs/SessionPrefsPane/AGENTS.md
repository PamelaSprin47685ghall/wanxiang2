# SessionPrefsPane

对话设置的内容（迁自旧 `Views/Dialogs.fs` 的 sessionSettings）。模型选择键由组合者给（OverlayDesk.SelectButton）；数字校验、载荷、保存往返与提示条归组合者。根规则见项目根目录 AGENTS.md。

- `SessionPrefsPane(look, modelPicker, init, validationFor)`：`View`、四个参数框与系统指令框、`SaveButton`、`CancelButton`、`Parameters`、`ToolsExpander`、`Summary`。
  - 动作：`ShowSummary`（多错摘要，任一参数框改动即收起）、`RevealParameters`、`SetPending`。
  - 事件：`SaveRequested SessionPrefsDraft`（原始输入 + 选中工具 id）、`Cancelled`。Ctrl/⌘ + Enter 与页脚（来自 UiKit 的 `Kit.DialogFooter`）行级 Enter 保存。
- 工具折叠头「工具 · 已启用 N 项」在展开 / 收起时更新，不重建开关。

## 外观

- `SessionPrefsPaneLook`：公共字段加 `TextAreaMinHeight`、`ContentMaxHeight`、`PendingMinWidth`。

## 证据与旧测试

- DialogsTests：`sessionSettings dialog validates numeric bounds and updates config`（界面部分；数字界限在组合者）
- 新增：工具开关改变保存集合与计数

## 与旧实现的差异

- 参数区用两列均分网格；旧 `twoColumnForm` 在窄宽下改单列，本库未做（对话框宽 520 下总是两列，与旧观感一致）。
- Ctrl/⌘ + Enter 在隧道阶段接（旧实现在系统指令框里按会先插换行）。
