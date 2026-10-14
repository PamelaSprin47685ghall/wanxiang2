# SidebarEmpty（库）

旧 `Views/Sidebar.fs` 的 Rebuild 末段迁入：搜索无结果提示、空态卡可见，以及三种空态（未连接、加载中、无对话，`SidebarEmptyKind` 加了 RequireQualifiedAccess）各自的按钮、骨架、标题与说明。卡的建法与种类翻转才重建仍归 Sidebar：调用方记住上次的 kind 自己比较。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组状态，决定与旧分支相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/sidebarempty.txt`（96 种组合）。
2. 未连接优先于加载中，加载中优先于无对话；「连接主机」按钮只在窄屏出现。

# 迁入或覆盖的旧测试
旧 Sidebar 空态用例要起窗口，留在旧仓库。本库新增：金样与 4 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
