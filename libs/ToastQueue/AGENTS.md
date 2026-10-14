# ToastQueue（库）

旧 `Controls/OverlayHost.fs` 里提示条的 `toastCap` 与 `toastEntries` 以及 `ToastWith` 入条前的去重和挤出迁入，成为一个对象：`Admit` 返回要撤下的提示，`Remove` 处理点击、Esc、到时撤下，`Showing` 是当前队列。控件、动画、焦点归还仍归 OverlayHost。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串入条与撤下，撤下次序与队列和旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/toastqueue.txt` 由照抄的旧入条步骤生成。
2. 同文只留最新一条；超过上限（默认 3）先撤最旧的；撤下时同文条目全部移出队列。

# 迁入或覆盖的旧测试
旧 UiStabilityTests 的 `toast stays inside narrow viewport and is keyboard dismissible live content` 测控件，留在旧仓库。本库新增：金样、同文去重、上限、撤下未知文本。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

# 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。
