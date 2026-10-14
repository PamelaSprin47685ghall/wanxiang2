# GenerationForm（库）

旧 `Views/SettingsGeneral.fs` 里「生成」分组的 `floatText` 与 `SaveGenerationCore` 的校验和载荷迁入：`validate`（5 个输入框文本 → 数值或按字段的错误文案）、`instructions`、`payload`。字段联合类型 `GenerationField` 用例名是泛名，加了 RequireQualifiedAccess。错误怎么挂到控件、提交与回复仍归 UI。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组输入，错误与载荷 JSON 和旧实现逐字节相同。** [根 AXIOM: 5，线上字节不变]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/generationform.txt` 由照抄的旧流程在 8 组表单上生成（错误按字段次序；载荷键序 temperature、topP、maxTokens、instructions、maxContextMessages、maxToolRounds、autoTitle，未设项写 null）。
2. 数字一律按不变文化解析，小数点只认点号。

# 迁入或覆盖的旧测试
旧 SettingsSurfaceTests 的 `generation save failure surfaces a failure toast instead of silence` 等要操作设置页，留在旧仓库。本库新增：金样、空值与必填、边界、小数点、载荷形状、指令去空白。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

# 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。
