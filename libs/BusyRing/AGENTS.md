# BusyRing

忙碌转圈（原生 Viewbox + Canvas + Path）：16×16 画布上的弧经 Viewbox 缩放，转动走声明式 `anim-spin` 样式类（由 `ThemeStyles` 与 GPU 驱动），无 `DispatcherTimer`、无 `BusyRingLook` 记录、无 `Follow.live` 逐属性订阅；减弱动效经 `MotionSwitch` 自动将动效时长归零。

## 接口（给宿主与组合者）

- `BusyRing(?size: float)`：构造转圈控件，默认尺寸 16.0。
- `member Host: Control`：放进布局的原生 Viewbox 控件。
- `member IsBusy: bool`：当前是否处于忙碌态。
- `member IsSpinning: bool`：旋转样式类是否已挂载。
- `member Render(busy: bool): unit`：单入口幂等渲染，根据忙碌态控制可见性与 `anim-spin` 样式类。
