# JsonRpc

JSON-RPC 2.0 信封和在途调用表：怎么拼请求和通知，怎么从响应里取 result 或 error，怎么把响应按 id 交还给等待者。不懂 MCP。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`JsonRpc.request/notification/errorNode/resultOf/idKey`、`PendingCalls`（Register、Forget、Complete、FailAll、Await、Count）。

# LEMMA
1. **id 按 JSON 原文匹配。** [根 AXIOM: 5]
    - 推理：字符串 "1" 和数字 1 是两个不同的 id。混为一谈会把别的调用的结果交给错误的等待者。
2. **任何结束路径都要把 id 从表里拿掉。** [根 AXIOM: 4]
    - 推理：超时、取消和进程退出都会留下孤儿。表只增不减就会漏内存，迟到的响应也会误投。

# SPEC
1. [LEMMA: 1] `pending calls match by id text...`，通过。
2. [LEMMA: 2] FailAll、超时、取消后表为空：`fail all ends every waiter...`，通过。
3. 信封格式与旧实现一致：`envelopes omit empty params...`、`result wins...`，通过；字节级由 `compose/McpClient` 金样覆盖。
