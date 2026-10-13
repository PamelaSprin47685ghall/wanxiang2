# ReconnectBackoff

断线重连的等待时长：从 1 秒起翻倍，封顶 30 秒（从旧 `Client/ReconnectBackoff.fs` 原样搬来）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`ReconnectBackoff.baseDelayMs`、`maxDelayMs`、`next`、`schedule`。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **重置时机归调用方（连接成功、用户手动连接），本库只算下一次等多久。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] 下列迁入的旧测试与本库补充的边界测试通过。

# 迁入或覆盖的旧测试
- 旧 ReconnectBackoffTests：`等待时长逐次翻倍`、`等待时长封顶在三十秒`、`连续失败时真的退避而不是原地打转`、`异常输入不会退出合法区间`。
- 旧 ClientStateAndWsClientTests：`ReconnectBackoff base delay is 1000ms and max delay is 30000ms`、`ReconnectBackoff next doubles delay under cap`、`ReconnectBackoff next clamps at maxDelayMs`、`ReconnectBackoff next handles smaller than baseDelayMs values safely`、`ReconnectBackoff schedule produces correct sequence for failure counts`、`ReconnectBackoff schedule and state machine correctly clamps at max delay and resets`。
- 补充：`失败次数为负是调用方错误，直接抛出`、`长时间宕机时一直停在上限`。
