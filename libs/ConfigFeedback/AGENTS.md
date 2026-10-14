# ConfigFeedback

配置写入的 request → ConfigApplied 确认台账（超时、断连全部失败），迁自旧 `Model/UiControllers.fs`。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **每个请求只结束一次：确认、超时、断连三者先到者生效。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- R27HostCapabilitiesTests：`config tracker expires silent requests and resolves only once`
