# ConversationExport

组合者：单次导出的分页读取（决策 312）。`ConversationExportController` 按页校验（导出编号、水位、总数、分页边界、顺序、字节上限），全部通过才交出 `ConversationExportDocument`；取消、断线、超时、校验失败立即丢弃不完整数据。迁自旧 `Model/ConversationExport.fs`，代码不变。协作者：Wire（`ConversationExportQuery` / `ConversationExportPageData` / `ConversationExportLimits`）、MessageView（`ofSnapshotItem`、`hasVisibleBody`、`AttachmentRef.formatSize`）。命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# SPEC
1. 真实组合：测试用 ChatDomain 投影 + ServerModel.exportPage 产出真实分页，交给本组合者（`tests/Tests.fs` 的 `export retrieves all 450 messages while normal snapshot still contains only 200`、`later deletion insertion and renaming cannot alter an export already reading` 等）。

# 迁入或覆盖的旧测试
- ConversationExportTests：`export retrieves all 450 messages while normal snapshot still contains only 200`、`later deletion insertion and renaming cannot alter an export already reading`、`deletions before export stay hidden even on older pages`、`nested fork export preserves its frozen ancestors after parent deletion`、`empty conversation can be exported as an empty transcript`、`cancel retry and disconnect cannot expose partial or stale documents`、`invalid export pages never become saveable`（五种）、`changed watermark or total aborts a multi-page export`、`timeout and size limit stop without offering a partial file`、`page byte budget splits large messages without splitting or truncating a message`、`unrenderable messages retain their raw content in export`
- 其中两处原断言 Markdown 文本（`Export.toMarkdown`），改为直接断言文档标题与消息正文；Markdown 渲染归 TranscriptMarkdown。其余用例（MainView、导出对话框焦点）是视图，留给视图层。
