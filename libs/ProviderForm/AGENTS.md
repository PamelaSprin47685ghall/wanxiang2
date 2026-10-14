# ProviderForm（库）

旧 `Views/SettingsProviders.fs` 里模型服务编辑器 saveCore 的校验段与 `isLoopbackUrl` 迁入：`validate` 返回各值、按字段的错误（`ProviderField` 加了 RequireQualifiedAccess）和自动选用的默认模型（`autoPicked`，配合 `autoPickedNote`）。端点归一（ProviderPresets.normalizeBaseUrl）、载荷、是否要密钥都属 ProviderPresets（writer 2），由调用方做。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组输入，结果与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/providerform.txt`（27 种编辑和 9 个地址）。
2. 默认模型留空时自动选第一个；超时与重试留空时沿用原值，否则用 120 和 2；看图两栏只能写已列出的模型，且同一模型不能两栏都写。
3. 已知旧行为，原样保留：即使有错误，`autoPicked` 也照样给出（旧代码有错时也会回填并提示）。

# 迁入或覆盖的旧测试
旧 SettingsProviders 的用例要起窗口，留在旧仓库。本库新增：金样与 5 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
