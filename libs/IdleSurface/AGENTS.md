# IdleSurface（库）

旧 `AppShell.fs` 刷新界面时「没有打开对话」分支的决定迁入：`IdleSurface.decide` 给出空态（`IdleEmpty`，泛名，加了 RequireQualifiedAccess）、空态主按钮文案、输入框是否可用及说明、模型标签；另有加载中与已打开对话的输入框说明。控件调用、目录查询（模型描述、看图能力）仍归 AppShell。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一组状态，决定与旧分支相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/idlesurface.txt` 由照抄的旧分支在 64 种状态组合上生成。

# 迁入或覆盖的旧测试
旧仓库的空态与输入框用例都要起窗口，留在旧仓库。本库新增：金样、连接失败入口、首次与重连、首次运行卡片、可输入。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

# 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。
