# ExchangeNav

阅读导航端点态与「更早一页取失败」行内提示（迁自旧 `Views/ChatView.fs` 的 `canJumpPrevious` / `canJumpNext`、`olderFailureBanner`）。跳转状态机仍归 `ExchangeJump`；平滑滚动与挂载窗口扩容由宿主做。重试按钮是本库 Spot，带读屏 Invoke。根规则见项目根目录 AGENTS.md。

## 外观

- `ExchangeNavLook`：`Ink`、`Gauge`、`BodyFont`、`DressRetry`、`Shadow`。

## 证据与旧测试

覆盖旧用例：`WindowedNavigationTests` / `ExchangeNavTests` 中失败横幅与端点态部分。本库无头行为用例覆盖端点读写、重试 Invoke 后隐藏。
