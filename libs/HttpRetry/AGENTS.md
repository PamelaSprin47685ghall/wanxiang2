# HttpRetry

对模型提供方的 HTTP 请求：在首字节之前退避重试；把非 2xx 翻成带状态码的异常。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`HttpRetry.sendWithRetry http makeRequest maxRetries ct`、`HttpRetry.statusError response body`。

# LEMMA
1. **只在首字节之前重试。** [根 AXIOM: 5]
    - 推理：流式输出开始后再重发，用户会看到同一段回答出现两次，而且多付一次钱。只对 429、408、5xx 重试；上游给了 Retry-After 就照它等。
2. **错误必须带状态码。** [根 AXIOM: 5]
    - 推理：调用方按状态码告诉用户是密钥错、限流还是服务挂了；没有状态码就只能说「未知错误」。
3. **这里不读限流头。** [根 AXIOM: 3]
    - 推理：限流信号是 Wire 里的 `ProviderRateLimit`，属于线协议；`statusError` 只返回异常，由组合者把限流信息挂上去。所以本库不依赖 Wire。

# SPEC
1. [LEMMA: 2] 2xx 不算错误；非 2xx 的异常带状态码、正文和 Retry-After：`tests/Tests.fs`，通过。
2. [LEMMA: 1] 临时性失败重试到上限，然后返回最后一次响应；非临时失败、重试次数为 0 时都不重试：通过。
