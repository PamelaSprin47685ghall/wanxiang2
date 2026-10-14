# ChatEmptyText（库）

旧 `Views/ChatView.fs` 的 `ChatStarters`、`ChatEmptyState` 与 `ShowEmpty` 里的标题和提示文案表迁入（`ChatEmptyText.text`）；`ChatEmptyState.showsBrand` 把 ShowEmpty 里「哪些空态画品牌标」的分支写成函数。控件搭建留在 UI。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一空态，标题、提示与起手式与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/chatemptytext.txt` 由旧文案表与旧 ChatStarters 生成。
2. 空态的用例名是具体业务名，不是泛名，联合类型不加 RequireQualifiedAccess，旧调用点不用改。

# 迁入或覆盖的旧测试
旧仓库的空态用例都要渲染 ChatView，留在旧仓库。本库新增：金样、品牌标分支、失败原因写进提示。
