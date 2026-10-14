# ToastDwell（库）

旧 `Controls/OverlayHost.fs` 里一条提示的 `remainingMs` 与 `paused` 迁入，成为一个对象：`Tick` 返回该不该撤下，`PointerEntered`、`PointerExited`、`ActionFocused`、`ActionUnfocused` 控制暂停。语气对应的停留时长仍由 UI 的 MotionLedger 给（失败 9000、警告 6500、其他 4000 毫秒，步进 200 毫秒）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一串事件，剩余时间、暂停与到时和旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/toastdwell.txt` 由照抄的旧计时生成。
2. 带行内动作的至少 8 秒；悬停或按钮有焦点时暂停；指针离开时按钮仍有焦点则继续暂停。
3. 已知旧行为，原样保留：按钮失焦一律恢复计时，即使指针还停在提示上。

# 迁入或覆盖的旧测试
旧仓库没有直接测计时的用例。本库新增：金样、各档时长、动作下限、暂停与恢复。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

# 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。
