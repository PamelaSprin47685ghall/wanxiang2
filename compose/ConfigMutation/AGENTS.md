# ConfigMutation（组合）

旧 `ConfigMutation`：把客户端配置片段合并进 AppConfig 并校验。协作者：AppConfig、PayloadFields。命名空间沿用 Wanxiang.Server。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **合并结果与错误文字与旧实现一致。** [根 AXIOM: 5]
2. **字段读取规则独立，抽成库 PayloadFields。** [根 AXIOM: 3]
    - 推理：validId 只在本处用、和各配置项的校验是一体的，留在组合者。

# SPEC
1. [LEMMA: 1] `golden/config-mutation.txt` 由旧 ConfigMutation.fs（AppConfig 用已验证等价的包）生成（`tests/GoldenTests.fs` 的 `ConfigMutation behaves byte for byte like the old implementation`）：
   provider/mcp/preset 的增改删、工具与生成参数、各类校验失败，每步给出 TOML 或错误。通过。

# 迁入或覆盖的旧测试
- 由金样覆盖，可删：无整文件（旧测试与 UI/WsConnection 混在一起）。
- 留到后续阶段：OnboardingConfigTests.fs（引用 Wanxiang.UI 与 Catalog）、R26PresetsTests.fs（引用 Protocol Catalog 与 ToolInfo）、ServerStoreMutationTests.fs（实为 Store/AttachmentStore 变更，归 ServerApp 阶段）。
