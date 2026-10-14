# TranscriptMarkdown（库）

旧 `Views/Brand.fs` 的 `Export.toMarkdown` 迁入（改名 `render`）：把已提交消息写成可读的 Markdown 转录，保留思考、工具调用与结果、附件，代码围栏比内容里最长的反引号串多一个。旧函数收 MessageView（MessageModel，不归本库）；本库自带 `TranscriptMessage` 记录，附件大小由调用方格式化好再传入（旧代码用 `AttachmentRef.formatSize`）。跳过规则（无正文、思考、工具调用、附件且无工具结果）随函数一起迁入。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组消息，转录与旧实现逐字节相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/transcriptmarkdown.txt` 由旧 `Export.toMarkdown` 生成：样例记录先还原成旧 MessageView（附件大小 512、1536、2097152 字节，对应 `512 B`、`1.5 KiB`、`2.0 MiB`）。
2. 换行用 `Environment.NewLine`，与旧实现一致。

# 迁入或覆盖的旧测试
旧 ConversationExportTests（去掉 Headless 包装，消息改用 TranscriptMessage）：
- `Markdown keeps orphan tool results timestamps and embedded fences`

`export retrieves all 450 messages…` 等用例测导出分页与 MessageView 投影，不在本库，留在旧仓库。本库新增：金样、不可见消息跳过。
