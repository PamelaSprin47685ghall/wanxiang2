# MediaContent

按传输能力（MediaSupport）把一个附件（sha256、媒体类型、文件名、大小）变成 Provider 能消费的 AIContent：图片/PDF/音频走 DataContent，文本内联成代码块，其余给一行说明。从旧 AttachmentContent 拆出，不认识消息格式。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
    - 推理：模型看到的文字是行为的一部分。

# SPEC
1. [LEMMA: 1] 测试覆盖能力拒收、丢失、文本内联；完整输出由 AgentContent 金样钉住。

# 迁入或覆盖的旧测试
- 旧测试迁在 compose/AgentContent/tests（逐条列在 AgentContent AGENTS.md）。
