# ImportSheet

导入对话框的内容：标题、说明、polite 状态行、预览详情、隐私说明、「取消 / 选择 JSON 文件… / 导入为新对话」三键（迁自旧 `Views/ImportDialog.fs` 的界面部分）。根规则见项目根目录 AGENTS.md。

- 只呈现与发事件：`CancelClicked`、`PickClicked`、`ImportClicked`。打开对话框（旧宽 480）、文件选择器（须在点击回调里直接打开，保留浏览器用户手势）、读文件与大小上限、`ConversationTransfer.parseImport`、ImportAttempt 只认最新回执、超时、成功后关对话框，都由组合者接。
- 组合者推状态：`SetChrome`（ImportView.chrome）、`SetStatus`（文字 + `ImportTone`）、`SetDetails`、`FocusPick` / `FocusImport`。
- 隐私说明文字由构造参数给（ConversationTransfer.privacyNote），控件名 `import-privacy-note`；状态 `import-status`、详情 `import-details` 同旧名。

## 外观

- `ImportSheetLook`：画笔 `Text`、`Muted`、`Danger`；字号、行高、间距是活的值。
- 用到的样式类（compose/Theme）：取消 `surface button-ghost hover-plain`；选择 `surface button-secondary hover-raised`；导入 `surface button-primary hover-accent`；分隔 `hairline`。

## 证据与旧测试

- R14TransferTests：`313 导入对话框：选文件前不能导入，预览通过后才发命令，失败可重试`（控件部分：门控、状态与详情、提交中不可再按、失败后可重试；解析与提交回执在组合者测）
- `r14 截图：导出、导入、更早一页失败、撤销提示条` 是截图用例，留在旧仓库。
- 新增：按钮读屏名与帮助、主题类、隐私说明、初始焦点。

## 与旧实现的差异

- 无行为差异。与旧代码一样，`SetChrome` 换「选择」键文字时读屏名随之变化（旧 `Ui.setButtonText`）。
