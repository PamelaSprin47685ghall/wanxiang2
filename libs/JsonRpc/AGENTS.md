# JsonRpc

合并包：JsonRpc、LineProcess、McpProtocol、StreamableHttp。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `JsonRpc`

## JsonRpc

JSON-RPC 2.0 信封和在途调用表：怎么拼请求和通知，怎么从响应里取 result 或 error，怎么把响应按 id 交还给等待者。不懂 MCP。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`JsonRpc.request/notification/errorNode/resultOf/idKey`、`PendingCalls`（Register、Forget、Complete、FailAll、Await、Count）。

## LEMMA
1. **id 按 JSON 原文匹配。** [根 AXIOM: 5]
    - 推理：字符串 "1" 和数字 1 是两个不同的 id。混为一谈会把别的调用的结果交给错误的等待者。
2. **任何结束路径都要把 id 从表里拿掉。** [根 AXIOM: 4]
    - 推理：超时、取消和进程退出都会留下孤儿。表只增不减就会漏内存，迟到的响应也会误投。

## SPEC
1. [LEMMA: 1] `pending calls match by id text...`，通过。
2. [LEMMA: 2] FailAll、超时、取消后表为空：`fail all ends every waiter...`，通过。
3. 信封格式与旧实现一致：`envelopes omit empty params...`、`result wins...`，通过；字节级由 `compose/McpClient` 金样覆盖。

## 成员 `LineProcess`

## LineProcess

按行通信的子进程：写一行进 stdin，stdout 每行回调一次，stderr 单独回调，退出只报告一次。不懂 JSON。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`LineProcess(command, args, env, onLine, onStderr, onExit)`、`Environment`、`IsRunning`、`Start`、`WriteLine`、`Kill`。

## LEMMA
1. **子进程只继承白名单环境变量。** [根 AXIOM: 4]
    - 推理：第三方 MCP 服务器不该读到宿主的密钥。白名单是 PATH、HOME、LANG、LC_ALL、TMPDIR、TZ，再加上调用方显式给的变量。
2. **Kill 杀整个进程树。** [根 AXIOM: 4]
    - 推理：npx 一类启动器会再起孙进程。只杀父进程会留下孤儿。

## SPEC
1. [LEMMA: 1] `child sees only the whitelist...`，通过。
2. [LEMMA: 2] `stderr goes to its own sink; kill ends...`，通过。
3. 行进行出、退出只报一次：`lines go in and come out...`，通过。

## 成员 `McpProtocol`

## McpProtocol

MCP 协议的纯数据部分：initialize 参数、tools/list 解析、tools/call 参数、调用结果渲染给模型的文本。不做 I/O。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`McpToolInfo`、`McpProtocol.ProtocolVersion/initializeParams/parseTools/parseArguments/callParams/renderCallResult`。

## LEMMA
1. **只认一个协议版本 2025-06-18。** [根 AXIOM: 3]
    - 推理：沿用旧实现。协商多版本要等真有服务器需要。
2. **坏工具条目丢掉，不让整张表失败。** [根 AXIOM: 4]
    - 推理：一个没名字的工具不该让其余工具都不可用。
3. **结果文本保留中文原样。** [根 AXIOM: 5]
    - 推理：结果要交给模型读，\uXXXX 转义只会浪费 token，还和旧实现的字节不同。

## SPEC
1. [LEMMA: 1] `initialize carries...`，通过。
2. [LEMMA: 2] `tools without a name are dropped...`，通过。
3. [LEMMA: 3] `call results keep Chinese readable...`，通过。
4. 参数为空或 null 时视为 {}，JSON 坏了就拒绝：`arguments: ...`，通过。

## 成员 `StreamableHttp`

## StreamableHttp

MCP Streamable HTTP 的一次请求往返：POST JSON，保存并回传 Mcp-Session-Id，响应可能是 JSON，也可能是 SSE。不懂 JSON-RPC 方法。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`StreamableHttpSession(endpoint, extraHeaders, timeout)`、`PickResponse`、`Post`。

## LEMMA
1. **SSE 响应取最后一个带 id 的帧。** [根 AXIOM: 5]
    - 推理：进度通知没有 id，它们不是这次调用的答案。
2. **失败返回可读文本，不抛异常。** [根 AXIOM: 4]
    - 推理：错误最后会交给模型和用户看，文本沿用旧实现的原文。

## SPEC
1. [LEMMA: 1] `SSE body yields the last frame...`，通过。
2. [LEMMA: 2] `failures come back as readable errors`，通过。
3. 会话 id 和额外请求头：`session id from the server...`，通过。
