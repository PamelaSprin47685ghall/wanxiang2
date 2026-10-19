# ChatEmpty

合并包：ChatEmpty、ChatScroller、ExchangeNav、SkeletonList。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ChatEmpty`

## ChatEmpty

对话区空态（迁自旧 `Views/ChatView.fs` 的 `emptyPanel` / `ShowEmpty` / `ShowEmbeddedEmpty` / `HideEmpty`）：对话起点提示、品牌空态卡、起手式与预设芯片、或内嵌内容。文案表与 `ChatEmptyState` 仍归 `ChatEmptyText`；本库只接收标题、提示与动作。主按钮与芯片是本库自带的 Spot，带读屏 Invoke。根规则见项目根目录 AGENTS.md。

### 外观

- `ChatEmptyLook`：`Ink`、`Gauge`（字号、间距、按钮几何、字距、行高）、`BodyFont`、`DressPrimary` / `DressSecondary` / `DressCard`（样式类），都是活值或主题样式。

### 证据与旧测试

覆盖旧空态渲染分支（对话起点 / 品牌卡 / 起手式与预设 / 内嵌 / 起步贴底）。本库无头行为用例覆盖显隐、重复显示不重建、主按钮与芯片的读屏 Invoke、无起手式回调时不画行。

## 成员 `ChatScroller`

## ChatScroller

对话滚动区（迁自旧 `Views/ChatView.fs` 的 `scroller`、回到最新浮钮、平滑滚动计时器、延迟滚到底 / 顶、分页前置锚点）。贴底跟随、未读、补间曲线、挂载窗口的记账仍归 `ScrollFollow` / `ScrollTween` / `MountWindow`；本库只执行滚动并报观察。浮钮是本库 Spot，带读屏 Invoke。根规则见项目根目录 AGENTS.md。

### 外观

- `ChatScrollerLook`：`Ink`、`Gauge`、`Glyph`、`Frame`、`Reduced`、`DressLatest`、`Shadow`。
- `ScrollEase`：使用方注入的补间（旧 ScrollTween）。

### 证据与旧测试

覆盖旧用例：`CraftsmanshipPolishTests` 回到最新显隐与读屏名、`EndpointScrollTests` / `InteractabilityPolishTests` 平滑滚动与滚轮打断、分页前置锚点。本库无头行为用例覆盖 Invoke、两拍到达、滚轮打断、前置补偿。

- PinOffset(y)：立即写偏移并经 ease.Pin 记为自己写的（旧 tween.Pin），钉住期间宿主重新对齐不会报 UserUp。

## 成员 `ExchangeNav`

## ExchangeNav

阅读导航端点态与「更早一页取失败」行内提示（迁自旧 `Views/ChatView.fs` 的 `canJumpPrevious` / `canJumpNext`、`olderFailureBanner`）。跳转状态机仍归 `ExchangeJump`；平滑滚动与挂载窗口扩容由宿主做。重试按钮是本库 Spot，带读屏 Invoke。根规则见项目根目录 AGENTS.md。

### 外观

- `ExchangeNavLook`：`Ink`、`Gauge`、`BodyFont`、`DressRetry`、`Shadow`。

### 证据与旧测试

覆盖旧用例：`WindowedNavigationTests` / `ExchangeNavTests` 中失败横幅与端点态部分。本库无头行为用例覆盖端点读写、重试 Invoke 后隐藏。

## 成员 `SkeletonList`

## SkeletonList

历史加载骨架（迁自旧 `Views/ChatView.fs` 的 `EnsureSkeletonBuilt` / `ShowSkeletonLoading` / `HideSkeletonLoading` 与骨架呼吸）：助手 → 用户 → 助手三块占位；显示且挂在可视树上、未减弱动效时按帧呼吸整块透明度。相位记账由使用方注入（旧 `BreathPhase`），计时器属于控件自己。根规则见项目根目录 AGENTS.md。

### 外观

- `SkeletonListLook`：`Ink`（`SkeletonInk.Bar` / `Edge`）、`Gauge`（间距、阅读宽、头像、圆角、透明度档）、`Frame`、`Reduced`，都是活值。
- `SkeletonBreath`：`Begin` 相位归零，`Step` 走一拍返回透明度（旧 BreathPhase 曲线）。

### 证据与旧测试

覆盖旧用例：`CraftsmanshipPolishTests.ChatView skeleton loading transitions toggle skeletonPanel and messagePanel visibility` 中骨架树与呼吸部分（显隐切换、挂载才转、减弱动效静态透明度）。本库金样 `tests/golden/skeleton.txt` 由旧 `ChatView.ShowSkeletonLoading`（减弱动效）生成；行为用例覆盖挂上才呼吸、减弱即时切换。
