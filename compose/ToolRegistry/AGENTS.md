# ToolRegistry（组合）

旧 `ToolRegistry` 与 `McpFunction`、`ToolDescriptor`。协作者：BuiltinTools、McpClient、AppConfig、ChatDomain、ToolNames、RefreshingPool。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **目录 JSON、构建出的工具、缺失判断、日志文字与旧实现一致。** [根 AXIOM: 5]
2. **客户端缓存（指纹换新）抽成 RefreshingPool。** [根 AXIOM: 3]
    - 推理：与 MCP 无关的通用语义。差别：读取配置不再在锁内，指纹相同的结果不变。

# SPEC
1. [LEMMA: 1] `golden/tools.txt` 由旧 BuiltinTools.fs、ToolRegistry.fs 跑同一 Scenario（真实 /bin/sh MCP 服务）生成（`tests/GoldenTests.fs` 的 `builtin tools and registry behave byte for byte like the old implementation`），覆盖目录、工具 schema、调用结果、配置热更新、Prune，通过。

# 迁入或覆盖的旧测试（旧仓库 d9651ed，可在旧仓库删除）
- ProductizationTests.fs「内建工具沙箱」：
  - `file tools are unavailable without a configured sandbox`
  - `file tools appear once a sandbox root is configured`
  - `sandbox rejects paths outside every configured root`
  - `stable tool ids round-trip to function names`
- McpTests.fs「ToolRegistry 契约与配置联动测试」：
  - `ToolRegistry 支持 MCP 客户端缓存、指纹更新与失效剪裁`
  - `ToolRegistry 组装 Descriptors 与 DescriptorsJson 包含内置与有效 MCP 工具`
