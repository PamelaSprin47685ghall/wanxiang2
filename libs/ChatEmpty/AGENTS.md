# ChatEmpty

合并包：ChatEmpty、ChatScroller、ExchangeNav、SkeletonList。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ChatEmpty`

## ChatEmpty

对话区空态（迁自旧 `Views/ChatView.fs` 的 `emptyPanel` / `ShowEmpty` / `ShowEmbeddedEmpty` / `HideEmpty`）：对话起点提示、品牌空态卡、起手式与预设芯片、或内嵌内容。
P2 去脑化：废除 Look 记录与 Spot 仿制件，改用原生 Button（样式类 primary/secondary）；单入口 `Render(ChatPhase)` 幂等渲染。

### 外观

统一接入 Avalonia 主题资源（`Palette.*`）与 ControlTheme / StyleClass，不再通过 Look 注入。

### 证据与旧测试

覆盖旧空态渲染分支（对话起点 / 品牌卡 / 起手式与预设 / 内嵌 / 起步贴底）。本库无头行为用例覆盖显隐、重复显示不重建、主按钮与芯片的读屏 Invoke、无起手式回调时不画行。

## 成员 `ChatScroller`

## ChatScroller

对话滚动区（`scroller`、回到最新浮钮）。废除控件内 DispatcherTimer 补间动画与 Post 队列重试，滚动由宿主 Runner 直设并保证最终状态正确性。贴底跟随、未读由使用方持有；本控件只报滚动观察并执行滚动。浮钮使用原生 Button（样式类 primary / icon-button-accent）。分页前置历史的高度补偿与锚定动作交由 Effect.ScrollChat(PreparePrepend / RestorePrepend) 在入口 Runner 统一执行。

### 外观

外观接入主题动态资源（`Palette.*`），废除 `ChatScrollerLook`。使用方注入补间判定接口 `ScrollEase`。
### 证据与旧测试

覆盖旧用例：`CraftsmanshipPolishTests` 回到最新显隐与读屏名、`EndpointScrollTests` / `InteractabilityPolishTests` 平滑滚动与滚轮打断、分页前置锚点。本库无头行为用例覆盖 Invoke、两拍到达、滚轮打断、前置补偿。

- PinOffset(y)：立即写偏移并经 ease.Pin 记为自己写的（旧 tween.Pin），钉住期间宿主重新对齐不会报 UserUp。

## 成员 `ExchangeNav`

## ExchangeNav

阅读导航端点态与「更早一页取失败」行内提示（迁自旧 `Views/ChatView.fs` 的 `canJumpPrevious` / `canJumpNext`、`olderFailureBanner`）。跳转状态机仍归 `ExchangeJump`；重试按钮改用原生 Button（样式类 secondary）。

### 外观

废除 `ExchangeNavLook`，颜色使用 `Palette.Text`、`Palette.SurfaceRaised`、`Palette.Warning` 等动态资源。

### 证据与旧测试

覆盖旧用例：`WindowedNavigationTests` / `ExchangeNavTests` 中失败横幅与端点态部分。本库无头行为用例覆盖端点读写、重试 Invoke 后隐藏。

## 成员 `SkeletonList`

## SkeletonList

历史加载骨架：助手 → 用户 → 助手三块占位。废除 DispatcherTimer 与 Look，视觉呼吸通过声明式动效样式类 `anim-breathe` 实现，减弱动效经 MotionSwitch 自动降级。

### 外观

废除 `SkeletonListLook`，统一走 Avalonia 资源字典与样式系统。

### 证据与旧测试

覆盖旧用例：`CraftsmanshipPolishTests.ChatView skeleton loading transitions toggle skeletonPanel and messagePanel visibility` 中骨架树与呼吸部分（显隐切换、挂载才转、减弱动效静态透明度）。本库金样 `tests/golden/skeleton.txt` 由旧 `ChatView.ShowSkeletonLoading`（减弱动效）生成；行为用例覆盖挂上才呼吸、减弱即时切换。
