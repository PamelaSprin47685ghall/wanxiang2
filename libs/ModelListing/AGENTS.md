# ModelListing

读模型服务的清单响应：模型名、能力字段里的「能否看图」、Ollama /api/show 与 /api/version、Anthropic 已知清单。从旧 ProviderCatalog / ModelCapabilities 拆出。只依赖 BCL，不联网、不缓存。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **解析响应是纯函数，独立于缓存与探活。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过；旧 ProviderCatalogTests 经 compose/ProviderCatalog 覆盖。

# 迁入或覆盖的旧测试
- 见 compose/ProviderCatalog AGENTS.md（modelIdsFromProbe 三条）。
