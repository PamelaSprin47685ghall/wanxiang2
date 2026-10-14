# Catalog（组合者）

主机目录（HostCatalog）与看图判断（ModelVision）、会话配置（Wire 的 SessionConfig）、模型切换（ModelSwitcher）的关联：`Catalog.visionOf`、`Presets`（套用、另存负载、描述、起名）、`ModelSwitcher.options`、`ModelSwitcher.chipText`，迁自旧 `Model/Catalog.fs`。模块名与库里相同，F# 按名合并同名模块，旧调用方不用改（测试 `library and composer members resolve through one module name` 核对）。命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# SPEC
1. 真实组合：目录快照的 generation 由主机 ProviderCatalog 从 AppConfig 生成后再解析；预设的传输类型与配置层 `TomlCodec.supportedProviderKinds` 核对；探测错误码与主机 `ProviderCatalog.ProbeFailure` 一一核对（`tests/Tests.fs`）。

# 迁入或覆盖的旧测试
- R16ImageVisionTests：`vision precedence is user override, then provider metadata, then name`
- R26PresetsTests：`catalog snapshot carries presets to clients`、`applying a preset falls back when its model or tools are gone`、`preset names are suggested from the title without collisions`
- ModelChipRateLimitTests：`switcher lists usable models with provider, current and default marks`、`switcher search matches model or provider and ranks prefix hits first`
- NativeTransportTests：`每个预设的传输类型都是配置层认得的`
- OnboardingConfigTests：`错误码编码解码往返，客户端类型与主机错误码一一对应`（前半；后半用真实 HTTP 探测 ProviderCatalog.probeModels，留在旧仓库或归 ProviderCatalog）
- Catalog.idleModelChip：迁自旧 AppShell.Render 空闲分支；测试与 tests/OldIdleChip.fs（旧代码原样摘录）逐例差分。
