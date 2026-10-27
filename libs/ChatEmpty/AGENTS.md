# ChatEmpty

合并包：ChatEmpty、ChatScroller、ExchangeNav、SkeletonList。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ChatEmpty`

### ChatEmpty

对话区空态（迁自旧 `Views/ChatView.fs` 的 `emptyPanel` / `ShowEmpty` / `ShowEmbeddedEmpty` / `HideEmpty`）：对话起点提示、品牌空态卡、起手式芯片、或内嵌内容。
P2 去脑化：废除 Look 记录与 Spot 仿制件，改用原生 Button（样式类 primary/secondary）；单入口 `Render(ChatPhase)` 幂等渲染。

W7-G1 收尾（Set* 写入成员清零）：
- 构造入口 `ChatEmpty(starters: (string * string) list, onStarter: (string -> unit) option, ?brandLogo)`：起手式表与点选回调是构造期事实，不再有 `SetStarters` / `SetStarterPick` 写入成员；没有宿主回调就不画起手式行。
- 删除 `SetPresets` 与预设芯片整条路径：唯一调用方（ChatPane.WirePresets）自身无调用方，预设芯片在生产零消费，且 `ShowPrompt` 里 `if not (List.isEmpty presets)` 恒假。
- `SetBottomAligned` 更名 `ApplyBottomAligned`：贴底的垂直对齐与外边距只由 ChatEmpty 一处归属（ChatPane 不再直写 `empty.Host.VerticalAlignment` / `Margin`）。
- 判定口径：本库 `member *.Set[A-Z]` 归零；`grep -rn "WireStarterPick\|WirePresets\|starterPick" libs compose --include='*.fs'` 归零（设置页 `SettingsScreen.WirePresets` 是预设重命名，异义保留）。

#### 外观

统一接入 Avalonia 主题资源（`Palette.*`）与 ControlTheme / StyleClass，不再通过 Look 注入。

#### 证据与旧测试

覆盖旧空态渲染分支（对话起点 / 品牌卡 / 起手式 / 内嵌 / 起步贴底）。本库无头行为用例覆盖显隐、重复显示不重建、主按钮与起手式芯片的读屏 Invoke、无起手式回调时不画行。

## 成员 `ChatScroller`

### ChatScroller

对话滚动区（`scroller`、回到最新浮钮）。废除控件内 DispatcherTimer 补间动画与 Post 队列重试，滚动由宿主 Runner 直设并保证最终状态正确性。贴底跟随、未读由使用方持有；本控件只报滚动观察并执行滚动。浮钮使用原生 Button（样式类 primary / icon-button-accent）。分页前置历史的高度补偿与锚定动作交由 Effect.ScrollChat(PreparePrepend / RestorePrepend) 在入口 Runner 统一执行。

#### 外观

外观接入主题动态资源（`Palette.*`），废除 `ChatScrollerLook`。使用方注入补间判定接口 `ScrollEase`。
#### 证据与旧测试

覆盖旧用例：`CraftsmanshipPolishTests` 回到最新显隐与读屏名、`EndpointScrollTests` / `InteractabilityPolishTests` 平滑滚动与滚轮打断、分页前置锚点。本库无头行为用例覆盖 Invoke、两拍到达、滚轮打断、前置补偿。

- PinOffset(y)：立即写偏移并经 ease.Pin 记为自己写的（旧 tween.Pin），钉住期间宿主重新对齐不会报 UserUp。
- ApplyCompact(value)：窄屏档（触控目标放大、不画宽按钮）。
- ApplyOffset(y)：立即写偏移（不记为自己写的）。

## 成员 `ExchangeNav`

### ExchangeNav

阅读导航端点态与「更早一页取失败」行内提示（迁自旧 `Views/ChatView.fs` 的 `canJumpPrevious` / `canJumpNext`、`olderFailureBanner`）。跳转状态机仍归 `ExchangeJump`；重试按钮改用原生 Button（样式类 secondary）。

#### 外观

废除 `ExchangeNavLook`，颜色使用 `Palette.Text`、`Palette.SurfaceRaised`、`Palette.Warning` 等动态资源。

#### 证据与旧测试

覆盖旧用例：`WindowedNavigationTests` / `ExchangeNavTests` 中失败横幅与端点态部分。本库无头行为用例覆盖端点读写、重试 Invoke 后隐藏。

- ApplyJumpEnds(previous, next)：写「更早 / 更晚」端点可用态。

## 成员 `SkeletonList`

### SkeletonList

历史加载骨架：助手 → 用户 → 助手三块占位。废除 DispatcherTimer 与 Look，视觉呼吸通过声明式动效样式类 `anim-breathe` 实现，减弱动效经 MotionSwitch 自动降级。

#### 外观

废除 `SkeletonListLook`，统一走 Avalonia 资源字典与样式系统。

#### 证据与旧测试

覆盖旧用例：`CraftsmanshipPolishTests.ChatView skeleton loading transitions toggle skeletonPanel and messagePanel visibility` 中骨架树与呼吸部分（显隐切换、挂载才转、减弱动效静态透明度）。行为用例（`tests/SkeletonList.Tests.fs`）覆盖挂上才呼吸、减弱即时切换。

## 成员 `ChatEmptyText`

## ChatEmptyText（库）

旧 `Views/ChatView.fs` 的 `ChatStarters`、`ChatEmptyState` 与 `ShowEmpty` 里的标题和提示文案表迁入（`ChatEmptyText.text`）；`ChatEmptyState.showsBrand` 把 ShowEmpty 里「哪些空态画品牌标」的分支写成函数。控件搭建留在 UI。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **同一空态，标题、提示与起手式与旧实现相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/BreathPhase.ChatEmptyText.Tests.fs` 通过：`brand shows only before a conversation can start`、`connection failure hint carries the reason`。
2. 空态的用例名是具体业务名，不是泛名，联合类型不加 RequireQualifiedAccess，旧调用点不用改。

#### 迁入或覆盖的旧测试
旧仓库的空态用例都要渲染 ChatView，留在旧仓库。本库新增：品牌标分支、失败原因写进提示。

## 成员 `ScrollFollow`

## ScrollFollow（库）

旧 `Views/ChatView.fs` 的贴底跟随记账迁入，成为一个对象，管是否贴底、「回到最新」意图、未读数、上一帧条数、上一帧是否在流式、定位钉住：
- `Rendered` 返回 ScrollToEnd 或 Keep，并累计未读；
- `Scrolled` 返回是否停掉平滑滚动；
- `Wheel`、`ReturnToLatest`、`Arrived`、`LeaveForBeginning`、`Pin`、`JumpedTo`、`Reset`；
- `Button` 给「回到最新」按钮的呈现。

滚动动画、计时器、偏移与尺寸判断（是否贴底、是否超出阈值、是否为动画自己写的偏移）仍归 ChatView。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **同一串事件，状态与决定和旧实现相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/BreathPhase.ScrollFollow.Tests.fs` 通过：`at the bottom a render follows; scrolled up it counts unread`、`older pages and streaming refreshes are not unread; a finished stream is`、`returning to latest stays at bottom through mid-animation frames`、`pin holds until wheel or reset`。
2. 贴底时渲染跟到末尾；不贴底时累计未读，但分页旧消息和流式刷新不算，流式落定记一次；「回到最新」的动画途中仍算贴底，直到用户向上滚。
3. 已知旧行为，原样保留：`Button` 在贴底时顺带把未读清零。

#### 迁入或覆盖的旧测试
旧滚动、未读用例要起窗口，留在旧仓库。本库新增 4 条规则用例。

#### 合并说明

2026-10-21（W7-CLEANBREAK）并入本包：源文件按原工程编译顺序落盘为本包源文件；
测试按 MERGE.md《测试工程的合并约定》改名 `tests/BreathPhase.<原文件名>.fs`，顶层模块改名 `<成员名><模块名>`；
原 `Wanxiang2.BreathPhase` 包不再交付，消费方改引 `Wanxiang2.ChatEmpty`。命名空间与类型名不变，消费方源码零改。
