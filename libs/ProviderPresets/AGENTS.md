# ProviderPresets

模型服务预设表、按端点反查、端点归一化（迁自旧 `Model/ProviderPresets.fs`）。旧的 ProviderKind 与 kindOf 没有生产使用方，按 LEMMA 8 删除，不迁。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **每个预设的传输类型都被配置层接受。** [根 AXIOM: 1] 本库不引用 AppConfig，测试用同一固定集合核对。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- UiPureLogicTests：`ProviderKind conversions and ProviderPresets all list`（只迁 ProviderPresets 部分，测试名改为 `ProviderPresets all list`）
- OnboardingConfigTests：`常用 4 家置顶，原生与兼容端点合并为一个条目`
- NativeTransportTests：`预设表里存在原生传输的选项`、`每个预设的传输类型都是配置层认得的`
