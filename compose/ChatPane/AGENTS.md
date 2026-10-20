# ChatPane（组合者）

ChatScroller + ExchangeNav + ChatEmpty + SkeletonList + ScrollFollow + ExchangeJump + ChatEmptyText + Theme = 旧 `Views/ChatView.fs` 的滚动区、空态、历史骨架与问答跳转。消息卡片由宿主放进 `Messages` 插槽（卡片组合者 MessageCardView）。根规则见项目根目录 AGENTS.md。

- 宿主入口：`ChatPane<'K>(ChatPaneMetrics.ofTheme manager styles, brandLogo: float -> Control)`（`'K` 为消息键；品牌标边长取 Metric.LogoEmpty）。
- 成员：`Host`、`Scroller`、`Nav`、`Empty`、`Skeleton`、`Follow`、`Jumps`、`Messages`、`ItemsControl`、`FocusReturned`；`Render(ChatVS)` 单入口投影；`ItemsControl` 配合原生 `VirtualizingStackPanel` 提供消息列表的真虚拟化回收；废除 Look 注入与 PrependAnchor 前置高度补偿（交由 Effect.ScrollChat）。
- 贴底与未读走 ScrollFollow；平滑滚动由 ScrollViewer 原生机制与 Effect.ScrollChat 驱动，废除手搓 ScrollTween；骨架呼吸走声明式 anim-breathe（废除手搓 BreathPhase 计时记账）；空态文案走 ChatEmptyText；问答跳转走 ExchangeJump。
- 外观全部取主题活实例；字距强调取 Spacing.Tracking.emphasis；常量只来自 DesignRhythm 的 LayoutPolicy。

## 证据与旧测试
- 行为断言：`tests/Tests.fs` 覆盖空态文案与品牌态、贴底跟随与回到最新、骨架显示与隐藏、问答跳转端点、虚拟化列表面板及 ChatVS 单入口幂等渲染和主体各状态可见性不变式（SPEC 7：退役纯界面整树金样，转为行为断言）。

## 与旧实现的差异
- 消息卡、标题栏 / 面包屑与分页前置的宿主侧接线仍在宿主；`Messages` 是插槽。
- 主体子项序与旧代码一致：ChatScroller 的叠放层拆开，滚动区、空态滚动区、「回到最新」、失败横幅直接放进主体 Grid；骨架时滚动区内容换成骨架。

- RefreshLatestButton()：宿主改了跟随状态后刷新「回到最新」按钮（不要再借 SetCompact 刷新）。PinOffset(y)：转给 ChatScroller.PinOffset。
- WireStartMode(bool)：起步模式空态贴近输入区布局插槽（命名避开 Set*，非每帧投影）。
- WireStarterPick / WirePresets：宿主注入的稳定回调/预设芯片插槽（非每帧投影状态）。ApplyCompact / ApplyOlderAvailable：宿主与跳转联动动作。
