# StreamCaret

流式光标（迁自旧 `Views/MessageCard.fs` 的流式 caret）：只做透明度呼吸，尺寸几何不动。当前不透明度由使用方的 `breath` 函数给（相位挂在跨重建持久的时钟上，流式卡每次重建也不从峰值重启，见组合者 MessageCardView）；本控件只在挂在树上时按帧重算，减弱动效时静态取上限、不起计时器。根规则见项目根目录 AGENTS.md。

## 外观

- `StreamCaretLook`：`Ink`（光标笔刷实例）、`Gauge`（`CaretGauge` 宽、高、圆角、呼吸上限）、`Frame`（帧间隔）、`Reduced`。

## 证据与旧测试

覆盖旧用例：`PresentationRefreshTests.streaming caret keeps opacity inside the breath band and pins to max under reduced motion`（控件部分；相位纯函数由 CaretBreath 库与组合者覆盖）。本库在 headless 下验证：挂上才起表、按呼吸函数取值、摘下停表、减弱动效钉在上限与即时切换。

## 与旧实现的差异

旧代码的呼吸原点是 MessageCard 模块级可变量；现在由组合者持有一个呼吸时钟对象传进来。
