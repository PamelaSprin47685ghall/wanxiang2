# ModelSwitcher

快速切换模型：ModelOption、按词搜索与排序、切换文案，迁自旧 `Model/Catalog.fs`。从目录列出选项（`options`）与芯片文字（`chipText`）要 HostCatalog，在组合者 Catalog。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **每个词都要命中模型名或服务名，模型名前缀命中排前。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 的 `filter matches every term in model or provider and ranks model prefix first`、`switching copy says it applies from the next reply`  通过。

# 迁入或覆盖的旧测试
- ModelChipRateLimitTests：`switching copy says it applies from the next reply`
