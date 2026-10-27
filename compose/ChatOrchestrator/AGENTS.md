# ChatOrchestrator（组合）

旧 `ChatOrchestrator`（含 SubmitOutcome、EmptyContextFinish、ToolRoundLimit、运行时记录类型）、`GenerationContext`、`TitleGenerator`。
协作者：EventStore、ChatDomain、Wire、AppConfig、AgentRuntime、AgentContent、MessageSerde、MediaContent、AgentHistory、ToolRegistry、ToolApproval、
PartialReplies、HostLog、CommandId、PlainText、ContextTrim、TitleText。命名空间沿用 Wanxiang.Server。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **广播事件、记账消息、发给上游的请求体与旧实现一致。** [根 AXIOM: 5]
2. **上下文裁剪与标题文字规则各有独立语义，抽成库 ContextTrim、TitleText。** [根 AXIOM: 3]
    - 推理：裁剪只看消息，标题规则只看字符串；构造上下文、调用模型起标题是协作，留在组合者。
3. **生成循环、排队、工具执行、记账是对话这段合作本身，留在组合者。** [根 LEMMA: 4]

# SPEC
1. [LEMMA: 1] `golden/orchestrator.txt` 由旧 GenerationContext.fs、TitleGenerator.fs、ChatOrchestrator.fs（其余协作者用已验证等价的包）跑同一真实组合场景生成（`tests/GoldenTests.fs` 的 `chat orchestration behaves byte for byte like the old implementation`、`next request waits for the title broadcast`）：
   真实 EventStore、ToolRegistry、本机假 OpenAI 服务；工具调用 → 工具结果 → 回答 → 自动标题，然后 429 失败。通过；连跑三次一致。
2. [LEMMA: 1] 迁入的旧测试全部通过（下列，如 `SubmitOutcome ofSubmitResult correctly maps all commit outcomes`、`auto title keeps only the first sentence and strips markdown`）。
3. 用量累加与半截回复判定已拆出（见下节《细拆》）：`UsageTally` 在 `libs/ContextTrim`，`ContinuableReply.hasText` 在 `libs/ContinuableReply`；`compose/ChatOrchestrator` 只剩组装与线上字节，金样 `chat orchestration behaves byte for byte like the old implementation` 与 `GenerationUsage 累加规则：各轮 token 无损合并，duration 取最新` 照旧逐字节通过。

# 迁入或覆盖的旧测试（旧仓库 d9651ed，可在旧仓库删除）
- ChatOrchestratorTests.fs 整个文件：
  - `SubmitOutcome ofSubmitResult correctly maps all commit outcomes`
  - `EmptyContextFinish decide handles true and false branches`
  - `ToolRoundLimit error formats correctly and is retryable`
  - `GenerationUsage 累加规则：各轮 token 无损合并，duration 取最新`
  - `ConversationRuntime 状态初始化与排队队列维护`
  - `GenerationError 配置失效格式化包含模型服务标识`
  - `ConversationRuntime 排队去重：相同 invocationId 幂等忽略，不同 invocationId FIFO 保序`
  - `SessionConfig 思维链预算层级回退契约：对话显式设置优先于全局默认`
  - `MediaSupport 传输支持判定：无模型服务或未知模型服务回退至保守 OpenAI 兼容能力`
  - `工具名称消歧归一化：支持 builtin: 与 mcp: 前缀转换为模型安全标识`
  - `未找到工具时构造标准 JSON 错误格式并可被模型消费`
  - `对话删除后内存运行时被移除且可重建（R5 无界集合）`
- ProductizationTests.fs「对话标题」「上下文裁剪」「编排记账与结束决策」三节，PartialPersistAndUrlSchemeTests.fs 一条：
  - `auto title keeps only the first sentence and strips markdown`
  - `auto title falls back to the user question when the model returns nothing`
  - `only system placeholder titles are auto-renamed`
  - `context trimming keeps the most recent messages`
  - `trimming never leaves an orphan tool result at the front`
  - `zero means no message limit`
  - `queued submit outcomes always resolve to committed or visible error`
  - `empty built context with a drained batch fails retryably, never completes`
  - `tool round limit error is retryable and names maxToolRounds`
  - `250 只有助手正文才算可续写的半截`

# 细拆（用量累加、半截回复判定）
- 逐轮 token 累加拆成库 UsageTally（对象自己拥有累加状态）；durationMs 取最后一次报告用量时的耗时、拼成线上 GenerationUsage 留在组合者，线上字节不变，金样照旧逐字节通过。
- `ChatOrchestrator.PartialText` 转发到库 ContinuableReply.hasText，公开面不变。
