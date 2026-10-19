# ExportSheet

合并包：ExportSheet、ImportSheet。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ExportSheet`

## ExportSheet

导出对话框的内容：标题、对话名、assertive 状态行、进度条、说明与隐私说明、「取消导出 / 重新导出 / 保存 JSON / 保存 Markdown」四键（迁自旧 `Views/ExportDialog.fs` 的界面部分）。根规则见项目根目录 AGENTS.md。

- `Render state`：把一帧 `ExportSheetState`（与 ExportView.decide 的 `ExportViewState` 一一对应）同步到控件；保存键不可见或不可用且全局焦点已空时，焦点补到关闭键。
- 页脚左右方向键：沿行找下一个「可见且可用」的键，中间态被藏掉的键不挡路；找不到就不处理（事件不标 Handled）。
- 事件：`CloseClicked`（读取中组合者当作取消，其余当作关闭对话框）、`RetryClicked`、`SaveClicked`（true 为 JSON）。分页读取、到期检查、进度节流、生成 Markdown / JSON、保存窗口（须在点击回调里直接打开）都由组合者接。
- 减弱动效：组合者按 ExportView 给 `Indeterminate = false`。

### 外观

- `ExportSheetAppearance`：画笔 `Text`、`Muted`、`Success`、`Danger`；字号、行高、间距是活的值。
- 用到的样式类（compose/Theme）：关闭 `surface button-ghost hover-plain`；重新导出与保存 JSON `surface button-secondary hover-raised`；保存 Markdown `surface button-primary hover-accent`；分隔 `hairline`。

### 证据与旧测试

- ConversationExportTests：`export footer arrows move focus between the three buttons`、`export ready state skips the hidden retry slot for arrow navigation`、`export save action appears only after the last page and fits the window`（呈现部分）、`cancel stays in the dialog and offers the retry entry`（用组合者替身推帧）、`export dialog returns focus to close when the save slot becomes unusable`
- PresentationRefreshTests：`export progress bar is determinate under reduced motion`
- R14TransferTests：`312 导出对话框同一次读取备好 Markdown 和 JSON，并显示隐私说明`（界面部分；文件内容在 ConversationTransfer / TranscriptMarkdown）
- 分页与控制器相关的其余用例（如 `export of an unopened conversation has its own progress view without navigating`）在组合者。

### 与旧实现的差异

- 废除 UiKit 与 Tap，改用原生 Button 与样式类（primary / secondary / ghost），关闭键文字变化时读屏名随之变化。
- `ExportSheet` 内部保留的焦点 Post（`FocusClose`）：布局首帧排队，非焦点竞态（与 `OverlayLayer` 同口径）。

## 成员 `ImportSheet`

## ImportSheet

导入对话框的内容：标题、说明、polite 状态行、预览详情、隐私说明、「取消 / 选择 JSON 文件… / 导入为新对话」三键（迁自旧 `Views/ImportDialog.fs` 的界面部分）。根规则见项目根目录 AGENTS.md。

- 只呈现与发事件：`CancelClicked`、`PickClicked`、`ImportClicked`。打开对话框（旧宽 480）、文件选择器（须在点击回调里直接打开，保留浏览器用户手势）、读文件与大小上限、`ConversationTransfer.parseImport`、ImportAttempt 只认最新回执、超时、成功后关对话框，都由组合者接。
- 组合者推状态：`SetChrome`（ImportView.chrome）、`SetStatus`（文字 + `ImportTone`）、`SetDetails`、`FocusPick` / `FocusImport`。
- 隐私说明文字由构造参数给（ConversationTransfer.privacyNote），控件名 `import-privacy-note`；状态 `import-status`、详情 `import-details` 同旧名。

### 外观

- `ImportSheetAppearance`：画笔 `Text`、`Muted`、`Danger`；字号、行高、间距是活的值。
- 用到的样式类（compose/Theme）：取消 `surface button-ghost hover-plain`；选择 `surface button-secondary hover-raised`；导入 `surface button-primary hover-accent`；分隔 `hairline`。

### 证据与旧测试

- R14TransferTests：`313 导入对话框：选文件前不能导入，预览通过后才发命令，失败可重试`（控件部分：门控、状态与详情、提交中不可再按、失败后可重试；解析与提交回执在组合者测）
- `r14 截图：导出、导入、更早一页失败、撤销提示条` 是截图用例，留在旧仓库。
- 新增：按钮读屏名与帮助、主题类、隐私说明、初始焦点。

### 与旧实现的差异

- 废除 UiKit 与 Tap，改用原生 Button 与样式类（primary / secondary / ghost）；换「选择」键文字时读屏名随之变化。
- `ImportSheet` 内部保留的焦点 Post（`FocusPick`、`FocusImport`）：布局首帧排队，非焦点竞态（与 `OverlayLayer` 同口径）。
