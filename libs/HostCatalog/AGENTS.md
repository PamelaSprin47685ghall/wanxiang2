# HostCatalog

主机能力目录（`catalog.snapshot`）：ProviderInfo、ToolInfo、McpInfo、GenerationInfo、PresetInfo、Catalog 的解析，以及可用模型服务、新对话默认模型、展示名、全局默认判断，迁自旧 `Model/Catalog.fs`。看图判断（要 ModelVision）、对话预设（要 Wire 的 SessionConfig）、从目录列出切换选项在组合者 Catalog。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **目录解析与旧代码一致。** [根 AXIOM: 1] 同一份线上目录读出的记录、默认选择与展示名与旧程序集相同。

# SPEC
1. [LEMMA: 1] `tests/golden/expected.txt` 由旧 Wanxiang.UI 程序集跑 `tests/Render.fs` 生成；金标准用例通过。
2. 金标准生成器 `tools/gen-golden.fsx`（跑旧仓库构建出的程序集）；重跑后 `tests/golden` 逐字节不变。

# 迁入或覆盖的旧测试
- UiPureLogicTests：`empty catalog defaults`、`parse and query catalog properties`
- 迁自旧 AppShell.HandleEvent 的自足部分（见 tests 里与旧代码原样摘录的差分用例）。
