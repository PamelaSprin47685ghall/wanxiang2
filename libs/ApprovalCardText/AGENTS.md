# ApprovalCardText（库）

旧 `Views/ToolApprovalCard.fs` 里不碰控件的部分：参数 JSON 的展示（中文不转义）、按钮与状态文案、策略说明。模块改名为 `ApprovalCardText`，避免与留在 UI 的 `ToolApprovalCard` 模块同名而互相遮蔽。`touchTarget`、`buttonRowMaxWidth` 与 `build` 留在 UI。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **全部文案与参数展示与旧实现逐字相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/approval.txt` 由旧代码生成。

# 迁入或覆盖的旧测试
旧 W3ToolApprovalUiTests 的用例要点击按钮，依赖 Avalonia，留在旧仓库。本库新增：金样、中文转义还原、无法解析或空白参数原样返回、未知策略与状态回落。
