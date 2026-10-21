# ErrorRecovery

生成失败的就地恢复动作（§215、决策 366）：`ErrorRecovery` 联合与按钮文案 `ErrorRecovery.label`，迁自旧 `Model/ErrorRecovery.fs`。旧 `ErrorRecovery.forKind` 按协议的 GenerationErrorKind 选动作，移到组合者 ConversationRuns（同名模块合并，旧调用方不用改）。分支名各自具体，不与别的联合撞名，故不加 RequireQualifiedAccess，保持旧调用写法。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **文案与旧代码一致。** [根 AXIOM: 1] 每个动作的按钮文案与旧程序集相同。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过：`366 限流的恢复动作文案是「换个模型」`。

# 迁入或覆盖的旧测试
- LibraryUndoTests：`§215 每类不可自愈的错误都有恢复动作`（`label SwitchModel` 一行；forKind 各行与 `GenerationErrorKind.hint` 一行在 ConversationRuns）
- R19SimplifyTests：`366 限流只给「换个模型」并直达切换器`（label 一行；forKind 两行在 ConversationRuns）
