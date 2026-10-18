# MotionSwitch（库）

旧 `Design/MotionPolicy.fs` 迁入：减弱动效开关 `MotionSwitch`（`IsReduced`、`SetReduced`、只在变化时触发的 `Changed`、门控时长 `Duration`），以及语义时长（`Timing`、`MotionTimings.all`、`initialMs`、`geometryAnimationAllowed`）。旧账本 28 项；`OutboxConfirmTimeout`（30000 ms）追加在联合类型末尾，发件箱确认超时，原先写死在 `compose/AppShell`。不插在中间：F# 案例标签按声明序编码，已打进包的组合者仍按旧标签引用。缓动曲线的数学在 ScrollTween，Avalonia 的 Easing 形态在 `compose/Theme`；本库不依赖界面框架，不引用任何其他库。模块名避开旧宿主的 `MotionPolicy`、`MotionLedger`。开关是实例，由主题管理者拥有，不再是全局可变状态。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **每项时长与旧实现相同；减弱动效时门控时长为零。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/motionswitch.txt`；`tests/Tests.fs` 开关用例。

# 迁入或覆盖的旧测试
旧用例里减弱动效的分支都要起窗口，留在旧仓库；开关与门控规则由本库覆盖。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 编译 `tools/OldMotion.fs`（照抄旧 MotionLedger）逐项输出。
