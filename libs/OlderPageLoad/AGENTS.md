# OlderPageLoad

「加载更早消息」请求的在途判断、超时判断与两条失败提示，迁自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs `CheckOlderPageTimeout` / `FailOlderPage` / `ShowOlderPageRetry`。超时时长（旧 MotionLedger.olderPageTimeout）由宿主传入；计时器、请求、失败条留在宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **超时与提示与旧代码一致。** [根 AXIOM: 1]

# SPEC
1. [LEMMA: 1] 测试内的旧代码原样摘录与新实现在 loading × requestedAt × 时刻上逐例一致。

# 迁入或覆盖的旧测试
- 旧仓库没有直接覆盖这些私有成员的单元测试；新增上述差分测试与在途判断测试。
