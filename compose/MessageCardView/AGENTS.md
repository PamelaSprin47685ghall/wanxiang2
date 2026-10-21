# MessageCardView（组合，消息卡）

把 MessageView 折成各消息卡控件库的数据，拼成一张卡（迁自旧 `Views/MessageCard.fs`）；消息卡内部控件（ActionStrip、AttachmentChip、ErrorPanel、FoldHeader、RevealPane、StreamCaret、ThinkingBlock、ToolCallCard、UsageFoot、UserBubble）全量使用无 Look 原生控件与单入口 Render，外观由 Avalonia 主题样式与 DynamicResource 驱动，废除旧式 CardLooks 传参。转圈加载指示（OverviewIcon.Spinner）采用原生 12x12 弧形 Path + 声明式 `anim-spin` 动画类，流式光标采用 `anim-breathe` 声明式动画类，无 DispatcherTimer。协作者（包）：ActionStrip、MarkdownView、ReadingLook、CodeTint、CopyButton、CountdownTicker、CardBits、MessageMenu、MessageView、ToolCallState、ToolOverview、ToolSummary、ToolArgsPreview、ToolErrorSummary、DurationText、MessageTimestamp、GenerationErrorTitle、ErrorRecovery、ConversationRuns、Wire、Theme、ThemePalette、ThemeMetrics、MotionSwitch、IconSet、DesignRhythm。根规则见项目根目录…

## 接口（给宿主）
- `CardLooks.ofTheme manager styles`：全部外观。
- `CardHost`：`Tint`（CodeTint）、`MathHtml`、`Wrap`（CodeWrapMemory）、`Clock`（`CaretClock`，流式光标呼吸原点，跨重建持久，整个应用一份）、`Touch`（`CardTouch`，触屏选中，整个列表一份）、`Now`（当前时刻）。
- `MessageCards(looks, host)`：
  - `Render(message, ctx: CardContext, actions: CardActions, index) : Control * MarkdownStream option`（旧 `renderWithStream`；流式卡的第二项用来原位更新正文）。
  - `ErrorCard(error, onRetry, actions, onContinue)`（旧 `errorCardWith`；倒计时读 `RetryCountdown.tryGet`，按 `host.Now` 走表）。错误卡内容一次折成一份 `ErrorPanelVS`（标题、说明、技术细节、倒计时秒数/是否走表/是否播报、动作行 `Continue`/`Retry`/`Recovery label`、三个回调），只调 `ErrorPanel.Render(vs)` 一次；不再逐属性直写面板。
  - `IncompleteMarker()`（旧 `incompleteMarker`）、`CopyButton(copy, tip, name, getText)`、`ToolCard`、`ToolActivity`、`Thinking`、`Attachment`、`Menu`、`Actions`、`Footer` 各件也公开。
  - `MessageCards.HighlightName = "reveal-highlight"`：卡外层高亮承载面的名字。
- `CardContext`（旧 MessageContext，同名字段改 PascalCase，加 `Spin: bool` 由 AppShell 依据 MotionSwitch 填充）；`CardActions`（旧 MessageActions，加上旧模块级的 `CopyLink`、`Recover` 两个出口，None 时不出对应入口）。
- `CardDeck(panel)`：`Show [key, factory]` 按身份复用（内部缓存），`CardDeck.key message ctx` 给已提交消息的身份（流式卡为 None，每次重建）；`Clear()` 换对话。

# LEMMA
1. **用真实主题填好的消息卡与错误卡，渲染出的控件树与旧 MessageCard 逐行相同。** [根 AXIOM: 5]
2. **卡片行为与旧代码相同：展开收起、复制、菜单、倒计时、恢复动作、流式光标呼吸、脚注占位、按身份复用。**
3. **改度量，已显示的卡跟着变。**

# SPEC
1. [LEMMA: 1] 旧仓库的整卡控件树金样复刻已退役（金样文件与生成器一并删除，理由见根 AGENTS.md《界面与前端组装》规则 3：单入口幂等渲染、单出口意图事件）。改为行为断言：`tests/BehaviorTests.fs` 覆盖工具卡头开合、错误卡倒计时与重试、消息菜单、右键复制、卡片堆按身份复用、外观来自主题活值、读屏 Invoke 与报按钮。
2. [LEMMA: 2] `tests/BehaviorTests.fs` 的各用例（见下）。
3. [LEMMA: 3] `外观来自主题管理者的活值`。

# 与旧代码的差异
- 旧代码的模块级可变量（copyTextSink、recoverySink、linkSink、呼吸原点、触屏选中）改为显式传入：`CardActions.CopyLink / Recover`、`CardHost.Clock / Touch`。错误卡的复制一律走 `actions.CopyText`，不再有「未注入时退回上次渲染的入口」。
- 脚注时间与倒计时用 `CardHost.Now`，不读系统时间。
- 错误卡不再逐属性直写 ErrorPanel（`SetHint` / `SetSeconds` / `SetTicking` / `AddContinue` / `AddRetry` / `AddRecovery` / `AddDetail` 全已退役）：整张卡折成一份 `ErrorPanelVS` 一次 `Render` 进去；倒计时与播报的语义由控件库保证（剩余秒数只往下走不回跳、播报一次）。
- 旧右键菜单打开时的 ParityProbe 计数、倒计时的 ParityProbe 计数没有迁（探针归宿主）。

# 迁入或覆盖的旧测试
按名迁入：`MessageCard toolCallCard header toggles argument and result details`（CraftsmanshipPolishTests）、`工具概览在等待批准时用警示色，不显示「完成」对勾配色`（VisualPassV4Tests）、`230 错误卡有半截回复时先给「继续生成」，没有时不给`（ContinueLinkUndoTests）、`§215 密钥失效的错误卡给出「打开模型服务设置」并路由到 sink`（LibraryUndoTests）、`262 限流倒计时未结束时重试按钮不可按并显示剩余秒数`（JourneyR9Tests，扩成「走完才可按」）、`262 没有倒计时的可重试错误照常可按`（JourneyR9Tests）、`error card toggles technical detail and triggers retry action`（DeliveryPolishingTests）、`r8 右键消息正文弹消息菜单（含复制消息链接）`（PartialPersistAndUrlSchemeTests）、`streaming completion keeps the message footnote row geometry constant`（UiStabilityTests）、`rebuilt streaming caret continues the breath run instead of restarting at peak`（PresentationRefreshTests）。
由本组合的行为断言覆盖：`message card renders user and assistant with reasoning and tools without error`、`message card renders assistant markdown without extra view mode`（DeliveryPolishingTests）、`long user messages get a clipped viewport with an expand entry`（SelectionOperabilityTests）、`等审批的工具调用显示「等待批准」而不是「执行中」`（VisualPassR3Tests）。
由控件库覆盖：`large tool detail is bounded first and can explicitly expand fully`（RevealPane）、`reduced motion keeps spinner static`（MessageCards 原生 spinner）、`streaming caret keeps opacity inside the breath band and pins to max under reduced motion`（StreamCaret 原生声明式动画）、`限流倒计时读屏名带剩余时间、走完才播报一次`（ErrorPanel）、`disclosure headers share one bordered transition` 与 `disclosure chevron and copy confirmation ride the shared easeOutCubic`（FoldHeader；过渡由主题的 hover-bordered 样式给）。
另加：`生成中的消息没有菜单项也没有操作按钮`、`复制按钮走宿主的复制入口并换成已复制`、`卡片堆按身份复用，流式卡每次重建`、`流式光标挂载声明式 anim-breathe 动效类`、`错误卡重建不重置倒计时，剩余秒数不回跳`、`错误卡倒计时走完只播报一次`。
