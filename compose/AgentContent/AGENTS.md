# AgentContent（组合）

旧 `Wanxiang.Agent.AttachmentContent`：MessageSerde 的附件引用交给 MediaContent 解析。公开面与旧实现相同。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **消息 JSON 与附件内容与旧实现逐字节一致。** [根 AXIOM: 5]
    - 推理：落盘消息与模型看到的内容都是外部可见格式。
2. **解析附件内容不认识消息格式，抽成库 MediaContent。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `golden/agentcontent.txt` 由旧 MessageSerde.fs、AttachmentContent.fs（旧版 Microsoft.Agents.AI 1.20.0）跑同一 Scenario 生成，覆盖序列化、宽松/严格解析、附件引用、三种传输下的各类附件，通过。

# 迁入或覆盖的旧测试（旧仓库 d9651ed，可在旧仓库删除）
- AgentCoverageTests.fs（MessageSerde、MediaSupport、AttachmentContent 各节）：
  - `MessageSerde roleName converts all ChatRole cases`
  - `MessageSerde textMessage and toolResultMessage helper creation`
  - `MessageSerde hasToolCall and toolCalls detection`
  - `MessageSerde attachmentRefs extraction with various object structures`
  - `MessageSerde fromJsonNode and fromJsonNodeWith loose parsing and empty contents behavior`
  - `MessageSerde attachmentRefs extracts attachment items and parses sizes`
  - `MessageSerde roleName and message helpers`
  - `MessageSerde hasToolCall and toolCalls detect function calls`
  - `MessageSerde fromJsonNodeWith parses untagged, tagged, and attachment contents`
  - `MediaSupport ofProviderKind assigns correct capabilities`
  - `AttachmentContent isImage correctly classifies media`
  - `AttachmentContent isPdf correctly classifies media`
  - `AttachmentContent isAudio correctly classifies media`
  - `AttachmentContent isText correctly classifies media`
  - `AttachmentContent resolve handles missing blob`
  - `AttachmentContent resolve handles empty blob`
  - `AttachmentContent resolve handles normal text file`
  - `AttachmentContent resolve handles truncated text file`
  - `AttachmentContent resolve handles supported image`
  - `AttachmentContent resolve handles unsupported or oversize media`
  - `AttachmentContent appendTo correctly adds to chat message`
- ProductizationTests.fs「消息解析」「附件送入模型」与 NativeTransportTests.fs 两条序列化往返：
  - `mixed message keeps its text while exposing attachment references`
  - `attachment-only message parses when empty contents are allowed`
  - `images reach the model as binary content`
  - `text attachments are inlined as a fenced block`
  - `a lost blob degrades to an explanation instead of failing`
  - `PDF 在支持的传输上作为二进制递过去`
  - `音频只在 Gemini 上作为二进制递过去`
  - `传输吃不下的类型要说清原因而不是默默省略`
  - `超过内联上限的二进制附件给出上限说明`
  - `工具调用 id 能穿过序列化往返`
  - `工具结果的调用 id 同样能穿过序列化往返`
- `MediaSupport.ofProviderKind`：由上面 `MediaSupport ofProviderKind assigns correct capabilities` 与 MediaContent 库测试 `ofProviderKind maps each transport to its capabilities` 覆盖。
