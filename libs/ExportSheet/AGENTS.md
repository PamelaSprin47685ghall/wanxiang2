# ExportSheet

导出对话框的内容：标题、对话名、assertive 状态行、进度条、说明与隐私说明、「取消导出 / 重新导出 / 保存 JSON / 保存 Markdown」四键（迁自旧 `Views/ExportDialog.fs` 的界面部分）。根规则见项目根目录 AGENTS.md。

- `Render state`：把一帧 `ExportSheetState`（与 ExportView.decide 的 `ExportViewState` 一一对应）同步到控件；保存键不可见或不可用且全局焦点已空时，焦点补到关闭键。
- 页脚左右方向键：沿行找下一个「可见且可用」的键，中间态被藏掉的键不挡路；找不到就不处理（事件不标 Handled）。
- 事件：`CloseClicked`（读取中组合者当作取消，其余当作关闭对话框）、`RetryClicked`、`SaveClicked`（true 为 JSON）。分页读取、到期检查、进度节流、生成 Markdown / JSON、保存窗口（须在点击回调里直接打开）都由组合者接。
- 减弱动效：组合者按 ExportView 给 `Indeterminate = false`。

## 外观

- `ExportSheetLook`：画笔 `Text`、`Muted`、`Success`、`Danger`；字号、行高、间距是活的值。
- 用到的样式类（compose/Theme）：关闭 `surface button-ghost hover-plain`；重新导出与保存 JSON `surface button-secondary hover-raised`；保存 Markdown `surface button-primary hover-accent`；分隔 `hairline`。

## 证据与旧测试

- ConversationExportTests：`export footer arrows move focus between the three buttons`、`export ready state skips the hidden retry slot for arrow navigation`、`export save action appears only after the last page and fits the window`（呈现部分）、`cancel stays in the dialog and offers the retry entry`（用组合者替身推帧）、`export dialog returns focus to close when the save slot becomes unusable`
- PresentationRefreshTests：`export progress bar is determinate under reduced motion`
- R14TransferTests：`312 导出对话框同一次读取备好 Markdown 和 JSON，并显示隐私说明`（界面部分；文件内容在 ConversationTransfer / TranscriptMarkdown）
- 分页与控制器相关的其余用例（如 `export of an unopened conversation has its own progress view without navigating`）在组合者。

## 与旧实现的差异

- 无行为差异：四键用 `Wanxiang2.UiKit` 的 `Tap.TextButton`，关闭键文字变化时读屏名随之变化，与旧 `Ui.setButtonText` 相同。
