# ReconnectLoop（库）

旧 `AppShell.fs` 连接与重连流程里能独立成立的一块记账迁入，成为一个对象：
- 连接尝试编号：`Connect`、`IsCurrent`，用来丢掉过期的异步回调；
- 至多一个挂起的重连：`Schedule` 返回票据和等待时长，`Fire` 在到点时校验票据；
- 网络恢复立刻重连：`ResumeNow`；
- 退避归零：`ResetDelay`，用于手动连接和连接成功。

退避规则由构造参数给（通常是 ReconnectBackoff.baseDelayMs 与 next）。计时、真正的连接、认证、断线时的清理与界面文案仍归 AppShell。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串操作，编号、挂起、等待时长与旧记账相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/reconnectloop.txt`（37 步脚本）。
2. 主机一直不在时等待时长逐次翻倍到上限，只在连接成功或用户手动连接时归零；没有令牌不排重连；新的连接让挂起的计时作废；ResumeNow 只在有挂起时才动作。

# 迁入或覆盖的旧测试
旧 ClientStateAndWsClientTests 的退避用例已在 ReconnectBackoff；AppShell 的重连用例要起窗口，留在旧仓库。本库新增：金样与 4 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/OldReconnectLoop.fs`（照抄旧记账，CancellationTokenSource 原样保留）重写金样；输出应与提交的金样逐字节相同。金样证据较弱（旧代码与 Async 交织，摘出的是记账部分），规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
