# ConnectionIssue

连接失败的类型化原因和对应的重试动作，以及按异常链归类（从旧 `Client/ConnectionIssue.fs` 搬来）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`ConnectionIssue`（RequireQualifiedAccess）、`ConnectionRetry`、`ConnectionIssue.retry`、`ConnectionIssue.ofException`。

# LEMMA
1. **界面按类型决定按钮，不看错误文案。** [根 AXIOM: 5]
2. **唯一改动：`ConnectionIssue` 的分支名（Rejected、TimedOut、Disconnected 等）是通用名字，按根规则加 `[<RequireQualifiedAccess>]`。** [根 AXIOM: 3]
    - 推理：不加的话会和其他库的同名分支在消费端冲突。旧 UI 要改为 `ConnectionIssue.Rejected` 这类限定写法。

# SPEC
1. [LEMMA: 1, 2] 迁入的旧测试和补充测试通过。

# 迁入或覆盖的旧测试
- 旧 DevicesUiTests：`exceptions are classified into typed issues`（只改为限定名）。
- 留在旧仓库（依赖 UI 的 Dialogs）：`each typed connection error maps to its retry action regardless of wording`、`retry visibility does not depend on message keywords` 等 DevicesUiTests 用例；`retry` 的映射由本库 `每种问题对应的重试动作` 直接覆盖。
- 补充：`取消算超时，其他套接字错误算断线并保留最外层消息`、`套接字错误优先于外层的超时异常`、`深层嵌套也能找到套接字错误`。
- 迁自旧 AppShell.HandleEvent 的自足部分（见 tests 里与旧代码原样摘录的差分用例）。
