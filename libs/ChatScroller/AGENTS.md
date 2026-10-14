# ChatScroller

对话滚动区（迁自旧 `Views/ChatView.fs` 的 `scroller`、回到最新浮钮、平滑滚动计时器、延迟滚到底 / 顶、分页前置锚点）。贴底跟随、未读、补间曲线、挂载窗口的记账仍归 `ScrollFollow` / `ScrollTween` / `MountWindow`；本库只执行滚动并报观察。浮钮是本库 Spot，带读屏 Invoke。根规则见项目根目录 AGENTS.md。

## 外观

- `ChatScrollerLook`：`Ink`、`Gauge`、`Glyph`、`Frame`、`Reduced`、`DressLatest`、`Shadow`。
- `ScrollEase`：使用方注入的补间（旧 ScrollTween）。

## 证据与旧测试

覆盖旧用例：`CraftsmanshipPolishTests` 回到最新显隐与读屏名、`EndpointScrollTests` / `InteractabilityPolishTests` 平滑滚动与滚轮打断、分页前置锚点。本库无头行为用例覆盖 Invoke、两拍到达、滚轮打断、前置补偿。

- PinOffset(y)：立即写偏移并经 ease.Pin 记为自己写的（旧 tween.Pin），钉住期间宿主重新对齐不会报 UserUp。
