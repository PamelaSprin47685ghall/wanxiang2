# ProviderProbeText（库）

旧 `Views/SettingsProviders.fs` 的看图说明（visionAutoText 的 describe）与探活回执（ApplyProbe）的文案和分支迁入：`visionLine`、`visionSummary`、`feedback`。看图判断用本库自己的 `VisionVerdict` 和 `VisionWhy`（与 ModelVision 同形，组合时映射）；错误说明先由调用方用 ProbeFailureKind.parse 解析。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组输入，文案与分支和旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/providerprobetext.txt`。
2. 编辑器打开时把模型填进草稿；没打开时只缓存，绝不自动保存；看图说明最多列 12 个模型。

# 迁入或覆盖的旧测试
旧探活用例要起窗口，留在旧仓库。本库新增：金样与 3 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
