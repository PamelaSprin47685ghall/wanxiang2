# MessageView

消息的渲染视图：AttachmentRef、ToolCallView、ToolCallStatus、MessageView（解析快照条目、回填工具结果）、ReplyTail（尾部回复链），迁自旧 `Model/MessageModel.fs`。旧 `ReplyTail.unansweredError` 是 Wire 的 GenerationError，本库只给出它的两句文案 `unansweredMessage` / `unansweredDetail`，由使用方拼装。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **解析与旧代码逐字节一致。** [根 AXIOM: 1] 同一份快照条目经本库得到的视图、回填结果、尾部判断与旧程序集相同。

# SPEC
1. [LEMMA: 1] `tests/golden/expected.txt` 由旧 Wanxiang.UI 程序集跑 `tests/Render.fs` 生成；`tests/Tests.fs` 的金标准用例通过。
2. 金标准生成器 `tools/gen-golden.fsx`（跑旧仓库构建出的程序集）；重跑后 `tests/golden` 逐字节不变。

# 迁入或覆盖的旧测试
- UiModelTests：`AttachmentRef detects image mime types and formats file sizes`、`MessageView ofJson parses user and assistant roles, reasoning, and tools`、`MessageView mergeToolResults correctly associates results and removes tool messages`
- UiPureLogicTests：`AttachmentRef formatSize formats correctly`、`AttachmentRef isImage distinguishes correctly`、`ofJson parses text, reasoning, toolCalls, and attachments`、`mergeToolResults correlates callId into toolCalls`
- ToolCallStatusTests：`ToolCallStatus badgeText returns exact expected text for all states`、`ToolCallStatus isTerminal returns true only for terminal states`
- ConversationJourneyTests：`latest reply text skips empty assistant tail and ignores earlier turns`、`regenerate needs confirmation only when the tail has something to lose`
- JourneyR9Tests：`263 末尾用户消息超过宽限期仍无回复判为未回复`（除最后一行 `unansweredError.retryable`，那一行随 unansweredError 留在旧仓库）
