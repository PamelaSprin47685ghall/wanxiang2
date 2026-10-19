# BreadcrumbAction

合并包：BreadcrumbAction、MountWindow、RevealFlash。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `BreadcrumbAction`

## BreadcrumbAction（库）

旧 `Views/ChatView.fs` 的 SetBreadcrumb、BreadcrumbText 与 ActivateBreadcrumb 状态迁入，成为泛型对象 `BreadcrumbAction<'Id>`。`Set` 接收 (文字, 说明, 父对话可用, 父对话) 或 None，返回呈现；`Activate` 返回要回到的父对话。文字由 ForkLineage.breadcrumb 给（组合时传入）；控件与自动化属性仍归 ChatView。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一串操作，呈现与跳转目标和旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/breadcrumbaction.txt`。

## 迁入或覆盖的旧测试
旧面包屑用例要起窗口，留在旧仓库。本库新增：金样与 1 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。

## 成员 `MountWindow`

## MountWindow（库）

旧 `Views/ChatView.fs` 的挂载窗口记账（决策 293）迁入，成为一个对象：
- `Plan`：每次渲染算出第一张要挂载的卡和被隐藏的提交；
- `Expand`：向上扩一步；
- `EnsureMounted`：把窗口扩到某条提交，多留 4 张；
- `Reset`：换对话时窗口回到初始。

初始窗口和步长由构造参数给（旧值是 40 和 60，见 `Initial` 和 `Step`）。卡片、prepend 锚点、重绘仍归 ChatView。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一串操作，窗口、隐藏表与首张下标和旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/mountwindow.txt`（20 步脚本）。
2. 分页前置在途且更早一页到达时，整页一起挂上。

## 迁入或覆盖的旧测试
旧长对话挂载用例要起窗口，留在旧仓库。本库新增：金样与 4 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。

## 成员 `RevealFlash`

## RevealFlash（库）

旧 `Views/ChatView.fs` RevealAndHighlight 与 EndRevealHighlight 的高亮记账迁入：`Revealed`、`Begin`（返回要先复原的上一条）、`End`、`LastFlashed`、`Highlighted`，停留时长 `DwellMs` 为 1600。计时器、找高亮承载面、背景刷仍归 ChatView。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一串操作，记账与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/revealflash.txt`。

## 迁入或覆盖的旧测试
旧搜索命中高亮用例（第 211 节）要起窗口，留在旧仓库。本库新增：金样与 1 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
