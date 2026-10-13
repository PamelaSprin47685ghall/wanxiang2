# McpClient（组合）

把 JsonRpc、McpProtocol、LineProcess、StreamableHttp 组合成旧 `Wanxiang.Server` 的 MCP 客户端：`IMcpTransport`、`McpStdioTransport`、`McpHttpTransport`、`McpClient`、`McpJson`。命名空间和公开面与旧代码相同，旧调用点不用改。库只以包的形式引用。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **字节和错误文本与旧实现一致。** [根 AXIOM: 5]
    - 推理：发给服务器的行、请求头和请求体，以及交给模型的结果文本，都是外部可见的格式。
2. **已知行为原样保留，改动等用户决定。** [根 AXIOM: 2]
    - 推理：`env` 对 stdio 是环境变量，对 HTTP 是额外请求头；HTTP 初始化失败时异常照旧向外抛。

# SPEC
1. [LEMMA: 1] `golden/mcp.txt` 由旧 McpTransport/McpClient 源码生成，`GoldenTests.fs` 逐字节比对，通过。
2. [LEMMA: 1, 2] 旧 McpTests 中传输、客户端和 McpJson 部分的测试已迁入 `tests/McpTests.fs`，通过。
