# BusyRing

忙碌转圈（迁自旧 `Controls/Primitives.fs` 的 `Ui.spinner`）：16×16 画布上的弧经 Viewbox 缩放，挂在可视树上才按帧转 18°，摘下即停；减弱动效时停在 0° 并露出整圈底轨。计时器属于控件自己。根规则见项目根目录 AGENTS.md。

## 外观

- `BusyRingLook`：`Ink`（`BusyInk.Track` 底轨、`Arc` 弧，笔刷实例）、`StrokeWidth`、`Frame`（帧间隔，旧 `MotionLedger.busySpinnerFrame`）、`Reduced`，都是活值。尺寸由使用方传（旧 spinnerSize / spinnerCompactSize）。

## 证据与旧测试

覆盖旧用例：`UiStabilityTests.reduced motion keeps spinner static`。本库在 headless 下验证：挂上才转、摘下停、减弱动效即时切换与底轨显隐。
