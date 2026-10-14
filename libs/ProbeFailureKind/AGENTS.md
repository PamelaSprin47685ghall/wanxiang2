# ProbeFailureKind

模型探测失败的类型：解析线上 `[code] 说明`，判断是否值得建议切换传输，迁自旧 `Model/Catalog.fs`。与主机 ProviderCatalog.ProbeFailure 错误码的一一对应由组合者 Catalog 的测试核对。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **没有前缀或认不出的码一律归为 ProbeUnclassified，说明原样保留。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- OnboardingConfigTests：`错误码编码解码往返，客户端类型与主机错误码一一对应` 的旧主机部分（其余在组合者 Catalog）
