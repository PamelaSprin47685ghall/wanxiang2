# ExportFileName

合并包：ExportFileName、ExportView、ImportView、ProgressThrottle、TranscriptMarkdown。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ExportFileName`

## ExportFileName（库）

旧 `Views/Brand.fs` 的 `Export.safeFileName` 原样迁入（改名 `ofTitle`）：对话标题变成安全的导出文件名（替换分隔符与控制字符、去首尾点与空白、按字素截到 220 字节、加 `.md`，空则为「对话.md」）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一标题，文件名与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/exportfilename.txt` 由旧函数在 14 个标题上生成。

## 迁入或覆盖的旧测试
旧 ConversationExportTests（去掉 Headless 包装）：
- `export filenames stay a bounded filename including long emoji titles`

本库新增：金样。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

## 成员 `ExportView`

## ExportView（库）

旧 `Views/ExportDialog.fs` 的 RenderProgress（状态文字、颜色档、进度、按键）与 Refresh 节流决定迁入：`decide`、`refresh`。入参采用互斥的 `ExportOutputKind`（None | MarkdownOnly | JsonOnly | Both）收敛输出物状态，消除了多裸 bool 参数。读取阶段用本库自己的 `ExportPhase`（与旧 ConversationExport 状态同形）。计时器、文件保存、焦点兜底仍归 ExportDialog。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组状态，呈现与节流决定和旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/exportview.txt`。
2. 只有读取中受节流（窗口由 MotionLedger.progressTextThrottle 给）；读完后两种格式都能再存；空对话或保存中不能存；减弱动效时没有不确定进度条。
3. 已知旧行为，原样保留：读完但还没有输出时，重试键可见。

## 迁入或覆盖的旧测试
旧导出用例要起窗口，留在旧仓库。本库新增：金样与 3 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。

## 成员 `ImportView`

## ImportView（库）

旧 `Views/ImportDialog.fs` 的按键门控（`ImportView.chrome`）、状态文案，以及「只认最新一次提交的回执」（`ImportAttempt`，旧 busy 加 currentAttempt；尝试号从 Guid 改成计数）迁入。预览解析、文件读取仍归 ImportDialog。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一串操作，门控与回执取舍和旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/importview.txt`。

## 迁入或覆盖的旧测试
旧导入用例要起窗口，留在旧仓库。本库新增：金样与 2 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。

## 成员 `ProgressThrottle`

## ProgressThrottle（库）

旧 `Views/ExportDialog.fs` 进度文字节流（54-60 字段，105-150 Refresh / RenderProgress）的记账迁入：`Request(now, force, highFrequency)` 返回 `ProgressRefresh.Now | Schedule | Skip`，`Rendered(now)`，`TailFired()`，`Pending`，窗口 `WindowMs` 为 100。判据与 ExportView 库里的 `ExportView.refresh` 相同；那里是无状态判据，这里连同两个状态字段一起拥有。宿主接上本库后，`ExportView.refresh` 即可删去（由集成方决定）。尾巴计时器、控件与导出控制器仍归 ExportDialog。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **高频进度合并，终态不延迟。** [根 AXIOM: 5] 分页到达比人眼快，合并后仍补上最后计数；完成、失败、取消立即显示。

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/progressthrottle.txt`；`tests/Tests.fs` 两条行为用例。

## 迁入或覆盖的旧测试
旧 `ConversationExportTests.export of an unopened conversation has its own progress view without navigating`、`PresentationRefreshTests.export progress bar is determinate under reduced motion` 要起对话框，留在旧仓库；节流规则由本库覆盖。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/OldProgressThrottle.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样证据较弱；规则另由 `tests/Tests.fs` 直接断言。

## 成员 `TranscriptMarkdown`

## TranscriptMarkdown（库）

旧 `Views/Brand.fs` 的 `Export.toMarkdown` 迁入（改名 `render`）：把已提交消息写成可读的 Markdown 转录，保留思考、工具调用与结果、附件，代码围栏比内容里最长的反引号串多一个。旧函数收 MessageView（MessageModel，不归本库）；本库自带 `TranscriptMessage` 记录，附件大小由调用方格式化好再传入（旧代码用 `AttachmentRef.formatSize`）。跳过规则（无正文、思考、工具调用、附件且无工具结果）随函数一起迁入。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组消息，转录与旧实现逐字节相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/transcriptmarkdown.txt` 由旧 `Export.toMarkdown` 生成：样例记录先还原成旧 MessageView（附件大小 512、1536、2097152 字节，对应 `512 B`、`1.5 KiB`、`2.0 MiB`）。
2. 换行用 `Environment.NewLine`，与旧实现一致。

## 迁入或覆盖的旧测试
旧 ConversationExportTests（去掉 Headless 包装，消息改用 TranscriptMessage）：
- `Markdown keeps orphan tool results timestamps and embedded fences`

`export retrieves all 450 messages…` 等用例测导出分页与 MessageView 投影，不在本库，留在旧仓库。本库新增：金样、不可见消息跳过。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。
