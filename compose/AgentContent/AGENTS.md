# AgentContent（组合）

旧 `Wanxiang.Agent.AttachmentContent`：MessageSerde 的附件引用交给 MediaContent 解析。公开面与旧实现相同。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **消息 JSON 与附件内容与旧实现逐字节一致。** [根 AXIOM: 5]
    - 推理：落盘消息与模型看到的内容都是外部可见格式。
2. **解析附件内容不认识消息格式，抽成库 MediaContent。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `golden/agentcontent.txt` 由旧 MessageSerde.fs、AttachmentContent.fs（旧版 Microsoft.Agents.AI 1.20.0）跑同一 Scenario 生成，覆盖序列化、宽松/严格解析、附件引用、三种传输下的各类附件，通过。
