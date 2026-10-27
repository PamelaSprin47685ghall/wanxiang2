# HttpRetry

合并包：HttpRetry、JsonPick、Sse。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `HttpRetry`

## HttpRetry

对模型提供方的 HTTP 请求：在首字节之前退避重试；把非 2xx 翻成带状态码的异常。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`HttpRetry.sendWithRetry http makeRequest maxRetries ct`、`HttpRetry.statusError response body`、`HttpRetry.backoffDelay attempt`。

## LEMMA
1. **只在首字节之前重试。** [根 AXIOM: 5]
    - 推理：流式输出开始后再重发，用户会看到同一段回答出现两次，而且多付一次钱。只对 429、408、5xx 重试；上游给了 Retry-After 就照它等。
2. **错误必须带状态码。** [根 AXIOM: 5]
    - 推理：调用方按状态码告诉用户是密钥错、限流还是服务挂了；没有状态码就只能说「未知错误」。
3. **这里不读限流头。** [根 AXIOM: 3]
    - 推理：限流信号是 Wire 里的 `ProviderRateLimit`，属于线协议；`statusError` 只返回异常，由组合者把限流信息挂上去。所以本库不依赖 Wire。

## SPEC
1. [LEMMA: 2] 2xx 不算错误；非 2xx 的异常带状态码、正文和正数秒的 Retry-After（`Delta` 与未来的 `Date` 都认，超出 `Int32.MaxValue` 秒时收敛到 `Int32.MaxValue`；已过期的 `Date` 或非正秒数不记入 `Data["RetryAfterSeconds"]`）：`tests/HttpRetry.Tests.fs`，通过。
2. [LEMMA: 1] 临时性失败重试到上限，然后返回最后一次响应；`maxRetries = Int32.MaxValue` 不会因 `+ 1` 溢出退化成不重试；每次尝试创建的 `HttpRequestMessage`（含 `Content`）在本次发送结束后释放；上游给出未来的 `Retry-After` 日期时重试等待也按该日期计算，且超大 `Retry-After` 收敛在 `Task.Delay` 定时器上限内不抛 `ArgumentOutOfRangeException`；非临时失败、重试次数为 0 时都不重试：`tests/HttpRetry.Tests.fs`，通过。
3. [LEMMA: 1] 退避时长从 250 ms 翻倍到 8 秒上限，任何尝试次数下都非负。`backoffDelay` 保留主仓的浮点幂与上限收敛实现，位移不参与计算：`tests/HttpRetry.Tests.fs` 的 `退避时长翻倍并收敛到八秒上限，位移不再溢出` 与 `退避时长在所有尝试次数上都非负`。合并后验证：2026-10-23 `./x all`（109 个库、67 个组合者测试工程及仓库外消费）与 `./x app` 双入口通过。

## 成员 `JsonPick`

## JsonPick

从不可信的 JSON 里按键取值：键缺失、类型不对、值为 null 一律得 None，从不抛异常。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`JsonPick.tryString/tryInt/tryBool/tryObject/tryArray`、`schemaOf`。

## LEMMA
1. **上游返回的 JSON 不可信，取值不能让流程崩掉。** [根 AXIOM: 5]
    - 推理：提供方会改字段、给 null、给字符串数字。取不到就得 None，由调用方决定默认值。

## SPEC
1. [LEMMA: 1] 各种类型取得到；缺失、null、类型不对、节点不是对象时都是 None：`tests/JsonPick.Tests.fs`，通过。

说明：Anthropic、Gemini 两个库各有一份私有的同类取值函数，这是有意的：库与库之间不互相依赖（根 AGENTS.md）。三份都只有几十行；第四个使用者出现时再考虑别的办法。

## 成员 `Sse`

## Sse

Server-Sent Events 分帧（W3C）：把字节流切成（事件名, data）。不懂任何事件的含义。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`SseEvent`、`Sse.read stream ct`。

## LEMMA
1. **只共享分帧，不共享语义。** [根 AXIOM: 3]
    - 推理：Anthropic 和 Gemini 都走 SSE，但事件含义完全不同。把两套语义塞进同一个解析器，只会得到一堆 if。
2. **按规范只去掉冒号后的一个空格。** [根 AXIOM: 5]
    - 推理：模型回答里的代码缩进就在 data 正文里。多去一个空格，用户看到的代码就错位了。

## SPEC
1. [LEMMA: 1] 空行分帧，多行 data 用换行拼接，末尾没有换行的帧也算，注释跳过，事件名在每帧之后重置：`tests/Sse.Tests.fs`，通过。
2. [LEMMA: 2] 缩进保留：`only the single space after the colon is stripped...`，通过。
