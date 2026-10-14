# ProbeSlot

模型服务探活的归属与结果缓存（迁自旧 `Views/SettingsProviders.fs` 的 `activeProbe`、`probeResults`，及编辑器打开时的模型清单回填规则）。编辑器的按钮与输入框回调、提示文字（ProviderProbeText）仍归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **结果只交给发起它的编辑器。** 不是当前编辑器的模型服务时不夺归属，只缓存；绝不后台自动保存（W2.5）。
2. **非空缓存优先于配置**；默认模型不在清单里时取第一项。

# SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- SettingsViewTests：`SettingsProviders ApplyProbe updates draft models in open editor and caches without auto-upserting`（归属与缓存部分；输入框回填留旧仓库）
