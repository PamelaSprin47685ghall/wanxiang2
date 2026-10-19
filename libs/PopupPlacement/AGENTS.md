# PopupPlacement

合并包：PopupPlacement、ToastDwell、ToastQueue。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `PopupPlacement`

## PopupPlacement（库）

旧 `Controls/OverlayHost.fs` 的 `positionPopup` 里的坐标计算迁入：`place`。锚点下方隔 gap 处左对齐（或右对齐），下方放不下翻到上方，最后钳进视口并留 inset。量控件尺寸、`TranslatePoint`、写 Margin 仍归 OverlayHost（旧值：inset = `Spacing.space2xl` 16，gap = `Spacing.spaceMd` 8）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组尺寸，落点与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/popupplacement.txt` 由照抄的旧计算在 30 组尺寸上生成。

## 迁入或覆盖的旧测试
旧 UiStabilityTests 的 `popup flips and clamps inside the viewport then restores anchor focus` 还测焦点归还，**旧用例不删**（部分覆盖）。本库新增：金样、翻到上方、右对齐。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

## 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。

## 成员 `ToastDwell`

## ToastDwell（库）

旧 `Controls/OverlayHost.fs` 里一条提示的 `remainingMs` 与 `paused` 迁入，成为一个对象：`Tick` 返回该不该撤下，`PointerEntered`、`PointerExited`、`ActionFocused`、`ActionUnfocused` 控制暂停。语气对应的停留时长仍由 UI 的 MotionLedger 给（失败 9000、警告 6500、其他 4000 毫秒，步进 200 毫秒）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一串事件，剩余时间、暂停与到时和旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/toastdwell.txt` 由照抄的旧计时生成。
2. 带行内动作的至少 8 秒；悬停或按钮有焦点时暂停；指针离开时按钮仍有焦点则继续暂停。
3. 已知旧行为，原样保留：按钮失焦一律恢复计时，即使指针还停在提示上。

## 迁入或覆盖的旧测试
旧仓库没有直接测计时的用例。本库新增：金样、各档时长、动作下限、暂停与恢复。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

## 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。

## 成员 `ToastQueue`

## ToastQueue（库）

旧 `Controls/OverlayHost.fs` 里提示条的 `toastCap` 与 `toastEntries` 以及 `ToastWith` 入条前的去重和挤出迁入，成为一个对象：`Admit` 返回要撤下的提示，`Remove` 处理点击、Esc、到时撤下，`Showing` 是当前队列。控件、动画、焦点归还仍归 OverlayHost。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一串入条与撤下，撤下次序与队列和旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/toastqueue.txt` 由照抄的旧入条步骤生成。
2. 同文只留最新一条；超过上限（默认 3）先撤最旧的；撤下时同文条目全部移出队列。

## 迁入或覆盖的旧测试
旧 UiStabilityTests 的 `toast stays inside narrow viewport and is keyboard dismissible live content` 测控件，留在旧仓库。本库新增：金样、同文去重、上限、撤下未知文本。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

## 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。
