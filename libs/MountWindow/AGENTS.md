# MountWindow（库）

旧 `Views/ChatView.fs` 的挂载窗口记账（决策 293）迁入，成为一个对象：
- `Plan`：每次渲染算出第一张要挂载的卡和被隐藏的提交；
- `Expand`：向上扩一步；
- `EnsureMounted`：把窗口扩到某条提交，多留 4 张；
- `Reset`：换对话时窗口回到初始。

初始窗口和步长由构造参数给（旧值是 40 和 60，见 `Initial` 和 `Step`）。卡片、prepend 锚点、重绘仍归 ChatView。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串操作，窗口、隐藏表与首张下标和旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/mountwindow.txt`（20 步脚本）。
2. 分页前置在途且更早一页到达时，整页一起挂上。

# 迁入或覆盖的旧测试
旧长对话挂载用例要起窗口，留在旧仓库。本库新增：金样与 4 条规则用例。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
