# ModelPickerList（库）

旧 `Views/ModelSwitcherView.fs` 的列表呈现迁入：`groups`（按模型服务分组，只在无查询且多组时显示组头）、`enterPick`（回车选第一条）、`shouldPick`（当前模型不再切换）、`move`（列表内上下，不循环，在顶端上移回搜索框；`PickerMove` 加了 RequireQualifiedAccess），以及文案。筛选本身在 ModelSwitcher.filter。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组输入，结果与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/modelpickerlist.txt`。

# 迁入或覆盖的旧测试
旧模型切换浮层用例（决策 304）要起窗口，留在旧仓库。本库新增：金样与 2 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
