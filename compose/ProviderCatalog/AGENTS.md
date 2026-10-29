# ProviderCatalog（组合）

旧 `ModelCapabilities`（能力缓存与落盘）与 `ProviderCatalog`（目录快照、模型列表探活）。协作者：AppConfig、ModelListing。命名空间沿用 Wanxiang.Server。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **快照与探活行为与旧实现一致。** [根 AXIOM: 5]
2. **响应解析是纯函数，抽成库 ModelListing；缓存、落盘、联网探活是协作，留下。** [根 AXIOM: 3]

# SPEC
1. [LEMMA: 1] 迁入的旧测试全部通过（下列，含本机 HttpListener 探活，如 `modelIdsFromProbe extracts openai and anthropic data arrays with sorting and deduplication`、`probeModels returns models when endpoint responds 200 OK`）。

# 迁入或覆盖的旧测试（旧仓库 d9651ed，可在旧仓库删除）
- ProviderCatalogTests.fs 整个文件：
  - `modelIdsFromProbe extracts openai and anthropic data arrays with sorting and deduplication`
  - `modelIdsFromProbe extracts gemini models array stripping prefix`
  - `modelIdsFromProbe handles empty or invalid json gracefully`
  - `probeModels returns models when endpoint responds 200 OK`
  - `probeModels handles http error status gracefully`
  - `providers snapshot hides api keys and formats models correctly`
  - `mcpServers snapshot formats command and url correctly`
  - `generation snapshot formats tools and defaults correctly`
- 留到后续阶段：R17CapabilityTests.fs、R16ImageVisionTests.fs（引用 Wanxiang.UI / SkiaSharp / Avalonia）。
