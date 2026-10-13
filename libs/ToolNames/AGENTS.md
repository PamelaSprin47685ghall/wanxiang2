# ToolNames

工具稳定标识与函数名的换算：`builtin:a.b` ⇄ `builtin_a_b`，`mcp:<server>/<tool>` 与 `mcp_<server>_<tool>`，以及解析 MCP 标识。只依赖 BCL。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
    - 推理：对话配置里存的标识不能变。

# SPEC
1. [LEMMA: 1] 测试覆盖往返与解析边界。
