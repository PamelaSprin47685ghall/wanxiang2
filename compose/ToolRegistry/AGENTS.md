# ToolRegistry（组合）

旧 `ToolRegistry` 与 `McpFunction`、`ToolDescriptor`。协作者：BuiltinTools、McpClient、AppConfig、ChatDomain、ToolNames、RefreshingPool。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **目录 JSON、构建出的工具、缺失判断、日志文字与旧实现一致。** [根 AXIOM: 5]
2. **客户端缓存（指纹换新）抽成 RefreshingPool。** [根 AXIOM: 3]
    - 推理：与 MCP 无关的通用语义。差别：读取配置不再在锁内，指纹相同的结果不变。

# SPEC
1. [LEMMA: 1] `golden/tools.txt` 由旧 BuiltinTools.fs、ToolRegistry.fs 跑同一 Scenario（真实 /bin/sh MCP 服务）生成，覆盖目录、工具 schema、调用结果、配置热更新、Prune，通过。
