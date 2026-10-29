# ExportSheet

合并包：ExportSheet、ImportSheet、ExportFileName、ExportView、ImportView、ProgressThrottle、TranscriptMarkdown。

- ExportSheet + ImportSheet：上一波按 MERGE.md 原判据（消费者集合 + 外部依赖 + Wanxiang2 依赖全同）合并。
- 2026-10-21（W7-CLEANBREAK）并入 ExportFileName、ExportView、ImportView、ProgressThrottle、TranscriptMarkdown：生产消费者集合相同（`compose/TransferDesk`），且合并后的依赖并集（`Avalonia`）已被该消费者直接声明——按 MERGE.md《追加判据（2026-10-21 W7-CLEANBREAK）：依赖闭包判据》合并是纯减法，消费者不多拿任何 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ExportSheet`

## ExportSheet

导出对话框的内容：标题、对话名、assertive 状态行、进度条、说明与隐私说明、「取消导出 / 重新导出 / 保存 JSON / 保存 Markdown」四键（迁自旧 `Views/ExportDialog.fs` 的界面部分）。根规则见项目根目录 AGENTS.md。

- `Render state`：把一帧 `ExportSheetState`（收敛为 `ExportSheetProgress` 与 `ExportSheetAction` DU）同步到控件；保存键不可用且全局焦点已空时，焦点补到关闭键。
- 内部按键辅助（`internal Tap`，不外泄）：`ApplyText(value: string)`（换字并同步读屏名，ExportSheet.fs:35）、`ApplyEnabled(enabled: bool)`（可用性，ExportSheet.fs:41）。命名已无 `Set*`。
- 页脚左右方向键：沿行找下一个「可见且可用」的键，中间态被藏掉的键不挡路；找不到就不处理（事件不标 Handled）。
- 事件：`CloseClicked`（读取中组合者当作取消，其余当作关闭对话框）、`RetryClicked`、`SaveClicked`（true 为 JSON）。分页读取、到期检查、进度节流、生成 Markdown / JSON、保存窗口（须在点击回调里直接打开）都由组合者接。
- 减弱动效：组合者按 ExportView 给 `Indeterminate = false`。

### 外观

- `ExportSheetAppearance`：只剩画笔 `Text`、`Muted`、`Success`、`Danger`（同一实例，切主题原地改色）。字号、行高、间距不再经记录传活值，改走主题动态资源键：标题 `Metric.FontTitle`、状态 `Metric.FontBody`、说明 `Metric.FontCaption` + `Metric.CaptionLineHeight`、段间距 `Space.Xl`、按钮间距 `Space.Md`（W7 外观退役）。
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
- 组合者推状态：单入口 `Render(vs: ImportSheetVS)`、`FocusPick` / `FocusImport`。
- 内部按键辅助（`internal Tap`，不外泄）：`ApplyText(value: string)`（换字并同步读屏名，ImportSheet.fs:35）、`ApplyEnabled(enabled: bool)`（可用性，ImportSheet.fs:41）。命名已无 `Set*`。
- 隐私说明文字由构造参数给（ConversationTransfer.privacyNote），控件名 `import-privacy-note`；状态 `import-status`、详情 `import-details` 同旧名。

### 外观

- `ImportSheetAppearance`：只剩画笔 `Text`、`Muted`、`Danger`（同一实例，切主题原地改色）。字号、行高、间距不再经记录传活值，改走主题动态资源键：标题 `Metric.FontTitle`、状态 `Metric.FontBody`、详情 `Metric.FontSmall`、说明 `Metric.FontCaption` + `Metric.CaptionLineHeight`、段间距 `Space.Xl`、按钮间距 `Space.Md`（W7 外观退役）。
- 用到的样式类（compose/Theme）：取消 `surface button-ghost hover-plain`；选择 `surface button-secondary hover-raised`；导入 `surface button-primary hover-accent`；分隔 `hairline`。

### 证据与旧测试

- R14TransferTests：`313 导入对话框：选文件前不能导入，预览通过后才发命令，失败可重试`（控件部分：门控、状态与详情、提交中不可再按、失败后可重试；解析与提交回执在组合者测）
- `r14 截图：导出、导入、更早一页失败、撤销提示条` 是截图用例，留在旧仓库。
- 新增：按钮读屏名与帮助、主题类、隐私说明、初始焦点。

### 与旧实现的差异

- 废除 UiKit 与 Tap，改用原生 Button 与样式类（primary / secondary / ghost）；换「选择」键文字时读屏名随之变化。
- `ImportSheet` 内部保留的焦点 Post（`FocusPick`、`FocusImport`）：布局首帧排队，非焦点竞态（与 `OverlayLayer` 同口径）。

## 成员 `ExportFileName`

## ExportFileName（库）

旧 `Views/Brand.fs` 的 `Export.safeFileName` 原样迁入（改名 `ofTitle`）：对话标题变成安全的导出文件名（替换分隔符与控制字符、去首尾点与空白、按字素截到 220 字节、加 `.md`，空则为「对话.md」）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **同一标题，文件名与旧实现相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/ExportFileName.ExportFileName.Tests.fs` 通过：`export filenames stay a bounded filename including long emoji titles`。

### 迁入或覆盖的旧测试
旧 ConversationExportTests（去掉 Headless 包装）：
- `export filenames stay a bounded filename including long emoji titles`

本库新增：文件名规则用例。


## 成员 `ExportView`

## ExportView（库）

旧 `Views/ExportDialog.fs` 的 RenderProgress（状态文字、颜色档、进度、按键）与 Refresh 节流决定迁入：`decide`、`refresh`。入参采用互斥的 `ExportOutputKind`（None | MarkdownOnly | JsonOnly | Both）收敛输出物状态，消除了多裸 bool 参数。读取阶段用本库自己的 `ExportPhase`（与旧 ConversationExport 状态同形）。计时器、文件保存、焦点兜底仍归 ExportDialog。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **同一组状态，呈现与节流决定和旧实现相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/ExportFileName.ExportView.Tests.fs` 通过：`reading shows progress and the close key cancels`、`ready with output can save both formats; empty or saving cannot`、`only reading is throttled`。
2. 只有读取中受节流（窗口由 MotionLedger.progressTextThrottle 给，见 `only reading is throttled`）；读完后两种格式都能再存；空对话或保存中不能存（`ready with output can save both formats; empty or saving cannot`）；减弱动效时没有不确定进度条。
3. 已知旧行为，原样保留：读完但还没有输出时，重试键可见（`ready with output can save both formats; empty or saving cannot`）。

### 迁入或覆盖的旧测试
旧导出用例要起窗口，留在旧仓库。本库新增：3 条规则用例。


## 成员 `ImportView`

## ImportView（库）

旧 `Views/ImportDialog.fs` 的按键门控（`ImportView.chrome`）、状态文案，以及「只认最新一次提交的回执」（`ImportAttempt`，旧 busy 加 currentAttempt；尝试号从 Guid 改成计数）迁入。预览解析、文件读取仍归 ImportDialog。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **同一串操作，门控与回执取舍和旧实现相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/ExportFileName.ImportView.Tests.fs` 通过：`only the latest attempt's reply counts`、`import key needs a preview and an idle dialog`。

### 迁入或覆盖的旧测试
旧导入用例要起窗口，留在旧仓库。本库新增：2 条规则用例。


## 成员 `ProgressThrottle`

## ProgressThrottle（库）

旧 `Views/ExportDialog.fs` 进度文字节流（54-60 字段，105-150 Refresh / RenderProgress）的记账迁入：`Request(now, force, highFrequency)` 返回 `ProgressRefresh.Now | Schedule | Skip`，`Rendered(now)`，`TailFired()`，`Pending`，窗口 `WindowMs` 为 100。判据与 ExportView 库里的 `ExportView.refresh` 相同；那里是无状态判据，这里连同两个状态字段一起拥有。宿主接上本库后，`ExportView.refresh` 即可删去（由集成方决定）。尾巴计时器、控件与导出控制器仍归 ExportDialog。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **高频进度合并，终态不延迟。** [根 AXIOM: 5] 分页到达比人眼快，合并后仍补上最后计数；完成、失败、取消立即显示。

### SPEC
1. [LEMMA: 1] `tests/ExportFileName.ProgressThrottle.Tests.fs` 通过：`reads inside one window merge into a single tail render`、`urgent and terminal states are never delayed`。

### 迁入或覆盖的旧测试
旧 `ConversationExportTests.export of an unopened conversation has its own progress view without navigating`、`PresentationRefreshTests.export progress bar is determinate under reduced motion` 要起对话框，留在旧仓库；节流规则由本库覆盖。


## 成员 `TranscriptMarkdown`

## TranscriptMarkdown（库）

旧 `Views/Brand.fs` 的 `Export.toMarkdown` 迁入（改名 `render`）：把已提交消息写成可读的 Markdown 转录，保留思考、工具调用与结果、附件，代码围栏比内容里最长的反引号串多一个。旧函数收 MessageView（MessageModel，不归本库）；本库自带 `TranscriptMessage` 记录，附件大小由调用方格式化好再传入（旧代码用 `AttachmentRef.formatSize`）。跳过规则（无正文、思考、工具调用、附件且无工具结果）随函数一起迁入。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **同一组消息，转录与旧实现逐字节相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/ExportFileName.TranscriptMarkdown.Tests.fs` 通过：`Markdown keeps orphan tool results timestamps and embedded fences`、`messages without visible body or tool results are skipped`。样例记录先还原成旧 MessageView（附件大小 512、1536、2097152 字节，对应 `512 B`、`1.5 KiB`、`2.0 MiB`）。
2. 换行用 `Environment.NewLine`，与旧实现一致（`Markdown keeps orphan tool results timestamps and embedded fences`）。

### 迁入或覆盖的旧测试
旧 ConversationExportTests（去掉 Headless 包装，消息改用 TranscriptMessage）：
- `Markdown keeps orphan tool results timestamps and embedded fences`

`export retrieves all 450 messages…` 等用例测导出分页与 MessageView 投影，不在本库，留在旧仓库。本库新增：转录用例、不可见消息跳过。
