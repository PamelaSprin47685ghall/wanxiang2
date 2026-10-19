# ChatPane（组合者）

ChatScroller + ExchangeNav + ChatEmpty + SkeletonList + ScrollFollow + ScrollTween + ExchangeJump + BreathPhase + ChatEmptyText + Theme = 旧 `Views/ChatView.fs` 的滚动区、空态、历史骨架与问答跳转。消息卡片由宿主放进 `Messages` 插槽（卡片组合者 MessageCardView）。根规则见项目根目录 AGENTS.md。

- 宿主入口：`ChatPane<'K>(ChatPaneMetrics.ofTheme manager styles, brandLogo: float -> Control)`（`'K` 为消息键；品牌标边长取 Metric.LogoEmpty）。
- 成员：`Host`、`Scroller`、`Nav`、`Empty`、`Skeleton`、`Follow`、`Jumps`、`Messages`、`ItemsControl`、`FocusReturned`；`Render(ChatVS)` 单入口投影；`ItemsControl` 配合原生 `VirtualizingStackPanel` 提供消息列表的真虚拟化回收；废除 Look 注入与 PrependAnchor 前置高度补偿（交由 Effect.ScrollChat）。
- 贴底与未读走 ScrollFollow；平滑滚动曲线走 ScrollTween（经 ChatScroller 的 ScrollEase）；骨架呼吸走 BreathPhase；空态文案走 ChatEmptyText；问答跳转走 ExchangeJump。
- 外观全部取主题活实例；字距强调取 Spacing.Tracking.emphasis；常量只来自 DesignRhythm 的 LayoutPolicy。

## 证据与旧测试
- 整树金样 `tests/golden/chatpane.txt`（260 行，8 个状态：消息列、骨架、更早一页失败、5 种空态），由 `tools/panegold/run.sh <旧仓库>` 用 extract-ledger 533b85b 的旧 ChatView 主体生成，嵌入字体排版。
- ChatViewTests：`空态文案与品牌态`、`贴底跟随与回到最新`、`骨架显示与隐藏`、`问答跳转端点`
- 空态文案金样在 libs/BreathPhase；滚动/跳转/呼吸的逐分支金样在对应逻辑库。

## 与旧实现的差异
- 消息卡、标题栏 / 面包屑、挂载窗口（MountWindow）与分页前置的宿主侧接线仍在宿主；`Messages` 是插槽。
- 主体子项序与旧代码一致：ChatScroller 的叠放层拆开，滚动区、空态滚动区、「回到最新」、失败横幅直接放进主体 Grid；骨架时滚动区内容换成骨架。

- RefreshLatestButton()：宿主改了跟随状态后刷新「回到最新」按钮（不要再借 SetCompact 刷新）。PinOffset(y)：转给 ChatScroller.PinOffset。
- 空态整树金样已在 tests/golden/chatpane.txt（空对话、无对话带主操作、未连接、连接失败、无模型五态，由旧 ChatView 生成）。
- SetStartMode(bool)：起步模式空态贴近输入区（旧 ChatView.SetStartMode），留白取 Space2xl。
