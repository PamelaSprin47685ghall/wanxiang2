# ForkAnchor

「编辑并分叉」的客户端事实：分叉点（forkAfterId）怎么选、被编辑的用户消息怎样拼成上线 JSON，以及没有可分叉对话时的提示。迁自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs `ForkFrom`。发 ForkConversation、选中新对话、聚焦输入框留在宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **分叉点与上线字节与旧代码一致。** [根 AXIOM: 1]

# SPEC
1. [LEMMA: 1] `tests/golden/expected.txt` 由 `tools/OldForkAnchor.fs`（旧代码原样摘录）跑 `tests/Render.fs` 生成；金标准用例通过。
2. `tools/gen-golden.fsx` 重跑后 `tests/golden` 逐字节不变。

# 迁入或覆盖的旧测试
- 旧仓库没有直接覆盖 ForkFrom 私有逻辑的单元测试；新增分叉点与上线字节两条行为测试。
