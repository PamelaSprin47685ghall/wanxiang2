# StreamableHttp

MCP Streamable HTTP 的一次请求往返：POST JSON，保存并回传 Mcp-Session-Id，响应可能是 JSON，也可能是 SSE。不懂 JSON-RPC 方法。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`StreamableHttpSession(endpoint, extraHeaders, timeout)`、`PickResponse`、`Post`。

# LEMMA
1. **SSE 响应取最后一个带 id 的帧。** [根 AXIOM: 5]
    - 推理：进度通知没有 id，它们不是这次调用的答案。
2. **失败返回可读文本，不抛异常。** [根 AXIOM: 4]
    - 推理：错误最后会交给模型和用户看，文本沿用旧实现的原文。

# SPEC
1. [LEMMA: 1] `SSE body yields the last frame...`，通过。
2. [LEMMA: 2] `failures come back as readable errors`，通过。
3. 会话 id 和额外请求头：`session id from the server...`，通过。
