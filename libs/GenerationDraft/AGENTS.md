# GenerationDraft（库）

旧 `Views/SettingsGeneral.fs` 里「生成」草稿的状态（`generationDirty`、`generationSaving`、`generationSyncing`、`generationRevision`、`generationBaseline`、`generationScope`、`draftScope`、`scopeEpoch`）与状态文案迁入，成为泛型对象 `GenerationDraft<'T>`（'T 为生成设置的值，旧代码里是 Catalog.generation）：`Edited`、`CatalogArrived`、`SetScope`、`Discard`、`CanSave`、`BeginSave`、`Finish`、`Status`。表单回填由调用方传入的 fill 完成，期间的编辑不算修改。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串事件，脏、保存中、基线、能否保存与状态文案和旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/generationdraft.txt` 由照抄的旧分支在 31 步脚本上生成。
2. 目录更新不覆盖未保存的草稿；草稿只能保存到开始编辑时的主机；切换主机作废在途保存的确认；较早的保存确认不把之后的编辑标成已保存；保存失败返回 true，调用方弹「保存失败，请重试。」。

# 迁入或覆盖的旧测试
旧 SettingsSurfaceTests 里的生成设置用例要操作设置页，留在旧仓库。本库新增：金样与 7 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

# 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。
