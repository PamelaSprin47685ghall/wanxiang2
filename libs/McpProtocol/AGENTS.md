# McpProtocol

MCP 协议的纯数据部分：initialize 参数、tools/list 解析、tools/call 参数、调用结果渲染给模型的文本。不做 I/O。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`McpToolInfo`、`McpProtocol.ProtocolVersion/initializeParams/parseTools/parseArguments/callParams/renderCallResult`。

# LEMMA
1. **只认一个协议版本 2025-06-18。** [根 AXIOM: 3]
    - 推理：沿用旧实现。协商多版本要等真有服务器需要。
2. **坏工具条目丢掉，不让整张表失败。** [根 AXIOM: 4]
    - 推理：一个没名字的工具不该让其余工具都不可用。
3. **结果文本保留中文原样。** [根 AXIOM: 5]
    - 推理：结果要交给模型读，\uXXXX 转义只会浪费 token，还和旧实现的字节不同。

# SPEC
1. [LEMMA: 1] `initialize carries...`，通过。
2. [LEMMA: 2] `tools without a name are dropped...`，通过。
3. [LEMMA: 3] `call results keep Chinese readable...`，通过。
4. 参数为空或 null 时视为 {}，JSON 坏了就拒绝：`arguments: ...`，通过。
