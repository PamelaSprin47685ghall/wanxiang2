# MessageText

从消息 JSON（ChatMessage 形状）取出用户看得见的正文：text 字段和 contents 里的 text 项，跳过思维链。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`MessageText.visible maxChars payload`。

# LEMMA
1. **思维链、工具参数和工具结果不算正文。** [根 AXIOM: 5]
    - 推理：搜索只匹配用户看得见的内容，沿用旧决策 201。
2. **有上限。** [根 AXIOM: 4]
    - 推理：超长消息不能拖慢整次扫描。

# SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs`，通过。
