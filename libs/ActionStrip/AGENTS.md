# ActionStrip

合并包：ActionStrip、AttachmentChip、ErrorPanel、FoldHeader、RevealPane、StreamCaret、ThinkingBlock、ToolCallCard、UsageFoot、UserBubble。已彻底拔除 Wanxiang2.UiKit 依赖与手搓 Look/Follow 原语，全套控件使用 Avalonia 原生控件 + 语义样式类 + DynamicResource 资源绑定，提供单入口幂等 Render(VS) 接入 ViewState，移除内部 DispatcherTimer（StreamCaret 呼吸改 anim-breathe 动效类，ErrorPanel 倒计时由 VS/宿主驱动，RevealPane 移除 Dispatcher.UIThread.Post），完全达成原生化。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ActionStrip`

## ActionStrip

消息脚注里的操作条（迁自旧 `Views/MessageCard.fs` 的 `actionButtons`）：图标按钮随文档流排布，整行恒定占一个命中区高（流式期间撤掉按钮也不缩，完成时脚注行不跳）；显隐只改不透明度，不动几何。闲置档分「隐去」与「淡显」（最新一条助手回复用淡显作可发现锚点）。复制按钮的读屏名规则在 `ActionStrip.copyName`。根规则见项目根目录 AGENTS.md。

### 外观

- `ActionStripLook`：`Gauge`（`StripGauge` 间距、命中区边长、淡显不透明度、显隐补间毫秒）、`Glyph`（复制 / 对勾 / 编辑 / 重新生成 / 删除）、`Muted`（图标默认笔刷）、`Dress`（图标按钮样式类）、`Reduced`。

### 证据与旧测试

覆盖旧用例：`UiStabilityTests.streaming completion keeps the message footnote row geometry constant`（行恒定占位部分）。本库在 headless 下验证：空行占位、两档闲置与显示、方形命中区与读屏名与动作、换图标、命中区边长实时生效与退订。哪些按钮出现（流式不挂、用户消息才有编辑、最后一条助手才有重新生成、有 commitId 才能删）由组合者 MessageCardView 决定并测试。

### 与旧实现的差异

旧代码用模块级可变量记触屏选中的那一条；本库只提供 `SetShown`，选中与悬停的联动放在组合者。

## 成员 `AttachmentChip`

## AttachmentChip

消息里的附件条（迁自旧 `Views/MessageCard.fs` 的 `attachmentRow`）：图标、文件名、大小与下载图标，点下发 `Invoked`；内容丢失时变灰、不可点，显示「内容已丢失」，提示与读屏名仍自描述。图片的提示是「查看或下载图片」，普通文件是「下载附件」。根规则见项目根目录 AGENTS.md。

### 外观

- `AttachmentChipLook`：`Ink`（`ChipInk` 底色、描边、三档文字）、`Gauge`（`ChipGauge` 描边、圆角、内外边距、行间距、两档字号、文件名最大宽）、`Glyph`（图片 / 文件 / 下载图标）、`Dress`（可点时挂抬起面的状态反馈，旧 `Interaction.surface raised`）。
- 数据 `ChipData`：文件名、已格式化的大小、是否图片、是否丢失（由使用方用 MessageView 的 AttachmentRef 规则算好）。

### 证据与旧测试

覆盖旧用例：`DeliveryPolishingTests.message card renders user and assistant with reasoning and tools without error`（附件部分）。本库在 headless 下验证：可点与提示、普通文件提示、丢失态不可点且自描述、字号实时生效与退订。整卡控件树金样在组合者 MessageCardView。

### 与旧实现的差异

点下不再直接调宿主的 `downloadAttachment`，改发 `Invoked`，由组合者接到宿主动作。

## 成员 `ErrorPanel`

## ErrorPanel

生成失败卡（迁自旧 `Views/MessageCard.fs` 的 `errorCard` / `errorCardWith`）：危险图标 + 标题 + 说明，可选倒计时，动作行依次是「继续生成」（有半截回复时）、「重试」（有继续时降为幽灵档）、恢复动作、「技术细节」披露（限高视口 + 复制）。根规则见项目根目录 AGENTS.md。

### 外观与件

- `ErrorPanelLook`：`Ink`（`ErrorInk`）、`Gauge`（`ErrorGauge` 间距、圆角、字号、按钮内边距与重试最小宽、箭头、阅读宽、倒计时间隔毫秒）、`BodyFont` / `MonoFont`、`Glyph`（警示、箭头）、`Dress secondary`（次要 / 幽灵按钮样式类）、`DressFold`（披露头状态反馈）、`TechLineHeight`、`HelperLineHeight`、`Reduced`。
- `ErrorPanelParts`：复制按钮、限高视口、倒计时文案（旧 `ProviderRateLimit.countdownText`）。
- 倒计时：`SetSeconds left announce` 推送剩余秒数（>0 时重试不可按、按钮写「重试（N 秒）」、读屏名带剩余时间；=0 时显示「现在可以重试了」并在 announce 时以 polite 播报一次）。`SetTicking true` 后挂在树上才按间隔发 `Tick`，组合者据此用 CountdownTicker 与宿主的 RetryCountdown 算下一帧。

### 证据与旧测试

覆盖旧用例：`JourneyR9Tests.262 限流倒计时未结束时重试按钮不可按并显示剩余秒数`、`JourneyR9Tests.262 没有倒计时的可重试错误照常可按`、`A11yRegressionTests.限流倒计时读屏名带剩余时间、走完才播报一次`、`ContinueLinkUndoTests.230 错误卡有半截回复时先给「继续生成」，没有时不给`（按钮排序部分）、`DeliveryPolishingTests.error card toggles technical detail and triggers retry action`。本库在 headless 下验证上述行为与计时器只在挂上且要求时运行。

### 与旧实现的差异

旧代码在卡里直接读 `RetryCountdown` 与模块级 `recoverySink`；现在倒计时由 `SetSeconds` 推、恢复动作由 `AddRecovery label action` 加，是否出某个按钮由组合者按 `GenerationError.retryable`、`ErrorRecovery.forKind` 决定。披露头照旧写控件类型覆盖（按钮）与 ItemStatus（旧 `SetRole / SetExpanded` 扩展的实际效果）。

## 成员 `FoldHeader`

## FoldHeader

折叠披露头（迁自旧 `Views/MessageCard.fs` 里思考过程、工具活动、错误技术细节三处共用的头部）：右指箭头展开时转到 90°，键盘 Enter/Space 与点击同效；标题由使用方往 `Caption` 里加 Run，开合后发 `Toggled`。根规则见项目根目录 AGENTS.md。

### 外观

- `FoldHeaderLook`：`Muted`（箭头与标题的笔刷实例）、`Gauge`（`FoldGauge` 各档：箭头槽、行间距、内边距、圆角、描边、标题字号与行高、箭头转动毫秒，都是活值）、`Chevron`（按笔刷给箭头图形）、`Dress`（挂表面状态反馈样式类，旧 `Interaction.surface bordered`）、`Reduced`（减弱动效时箭头瞬转）。

### 证据与旧测试

覆盖旧用例：`PresentationRefreshTests.disclosure headers share one bordered transition`、`PresentationRefreshTests.disclosure chevron and copy confirmation ride the shared easeOutCubic`（箭头部分）。本库在 headless 下验证：开合转角与通知、前置/尾随件的位置、键盘开合、度量实时生效与退订。

### 与旧实现的差异

旧代码的 `SetRole / SetExpanded` 是旧 Primitives 里的扩展：角色写控件类型覆盖（按钮），展开状态写 ItemStatus「已展开 / 已折叠」；本库照做。旧工具活动头不报展开状态，用 `ReportState <- false` 关掉。读屏名与提示由使用方在 `Toggled` 里按旧文案设置。

## 成员 `RevealPane`

## RevealPane

限高视口（迁自旧 `Views/MessageCard.fs` 的 `detailViewport`）：内容超出给定高度时，下方露出「展开全部 / 收起」幽灵按钮；首次布局前视口高度为 0，不据此判定裁切。根规则见项目根目录 AGENTS.md。

### 外观

- `RevealPaneLook`：`Gauge`（`RevealGauge.Gap` 视口与按钮间距、`Gutter` 右侧滚动条槽，都是活值），`Dress`（给按钮挂幽灵按钮样式类）。
- 限高值由使用方按布局规则传入（旧 `LayoutPolicy.expandedDetailMaxHeight`）。

### 证据与旧测试

覆盖旧用例：`UiStabilityTests.large tool detail is bounded first and can explicitly expand fully`、`SelectionOperabilityTests.long user messages get a clipped viewport with an expand entry`、`ScrollLayoutTests.嵌套 ScrollViewer 与草稿/待发送滚动视口具备右侧安全留白与视觉分层`（右侧留白部分）。本库在 headless 下验证：短内容不出按钮、超高出按钮、全文态切换与读屏名、隐藏时退出全文态、度量实时生效且摘下后退订。整卡的控件树金样在组合者 MessageCardView。

### 与旧实现的差异

旧代码返回 `(Control * (bool -> unit))` 二元组，现在是一个对象（`Host`、`SetVisible`、`ToggleFull`）。

## 成员 `StreamCaret`

## StreamCaret

流式光标（迁自旧 `Views/MessageCard.fs` 的流式 caret）：只做透明度呼吸，尺寸几何不动。当前不透明度由使用方的 `breath` 函数给（相位挂在跨重建持久的时钟上，流式卡每次重建也不从峰值重启，见组合者 MessageCardView）；本控件只在挂在树上时按帧重算，减弱动效时静态取上限、不起计时器。根规则见项目根目录 AGENTS.md。

### 外观

- `StreamCaretLook`：`Ink`（光标笔刷实例）、`Gauge`（`CaretGauge` 宽、高、圆角、呼吸上限）、`Frame`（帧间隔）、`Reduced`。

### 证据与旧测试

覆盖旧用例：`PresentationRefreshTests.streaming caret keeps opacity inside the breath band and pins to max under reduced motion`（控件部分；相位纯函数由 CaretBreath 库与组合者覆盖）。本库在 headless 下验证：挂上才起表、按呼吸函数取值、摘下停表、减弱动效钉在上限与即时切换。

### 与旧实现的差异

旧代码的呼吸原点是 MessageCard 模块级可变量；现在由组合者持有一个呼吸时钟对象传进来。

## 成员 `ThinkingBlock`

## ThinkingBlock

思考过程卡（迁自旧 `Views/MessageCard.fs` 的 `reasoningBlock` 外框与正文）：分组卡 + 左缘竖线，头部（折叠头）与正文视口由组装者放进来；正文字号比阅读字号小 1.5，右键不出主题自带的 Copy 浮层。标题文案「正在思考 / 已深度思考（收起 / 展开）」由 `ThinkingBlock.title` 给。根规则见项目根目录 AGENTS.md。

### 外观

- `ThinkingBlockLook`：`Ink`（`ThinkingInk` 底色、描边、竖线、正文、选区）、`Gauge`（`ThinkingGauge` 描边、竖线宽、内外边距、圆角）、`LineHeight`（正文行高随字号，旧 `ReadingRhythm.secondaryLineHeight`）。

### 证据与旧测试

覆盖旧用例：`DeliveryPolishingTests.message card renders user and assistant with reasoning and tools without error`（思考部分）。本库在 headless 下验证外框结构、竖线宽实时生效与退订、正文字号与标题文案。整卡控件树金样在组合者 MessageCardView。

## 成员 `ToolCallCard`

## ToolCallCard

工具调用卡（迁自旧 `Views/MessageCard.fs` 的 `toolCallCard`）：头部是唯一展开入口，显示状态图标、显示名、状态药丸、人话摘要（AutomationId `tool-human-summary`）、一行参数摘要与失败横幅；展开后参数摘要让位，限高视口里列出「调用参数」「执行结果 / 执行结果 (异常)」各带复制，状态未知时补一节「未收到工具执行结果（生成已结束）」。根规则见项目根目录 AGENTS.md。

### 外观与件

- `ToolCallCardLook`：`Ink`（`ToolInk` 各色）、`Gauge`（`ToolGauge` 间距、圆角、字号、箭头槽、箭头转动毫秒）、`BodyFont` / `MonoFont`、`Glyph`（扳手、警示、信息、对勾、箭头、两档转圈）、`Dress`（头部表面状态反馈）、`TechLineHeight`、`Reduced`。
- `ToolCardParts`：使用方给复制按钮（`Copy tip name getText`）与限高视口（`Reveal content visible`），本库不引用别的库。
- 数据 `ToolCardData`：文案都由使用方用 ToolCallState、ToolSummary、ToolArgsPreview、MessageView 算好；`ToolCallCard.sections`、`ToolCallCard.rawPayload` 给出旧的取舍与整段复制格式。

### 证据与旧测试

覆盖旧用例：`CraftsmanshipPolishTests.MessageCard toolCallCard header toggles argument and result details`、`VisualPassR3Tests.等审批的工具调用显示「等待批准」而不是「执行中」`。本库在 headless 下验证：等审批的配色与图标、开合与读屏名、失败横幅与异常节、未知状态补节与整段复制格式、复制按钮文案。整卡控件树金样在组合者 MessageCardView。

### 与旧实现的差异

旧代码对头部连设两次 HelpText，以后一次为准（「展开查看参数与结果 / 收起参数与结果」），本库只设这一次。

## 成员 `UsageFoot`

## UsageFoot

消息脚注里的时间与用量（迁自旧 `Views/MessageCard.fs` 的 `renderUsage` 与 `footer`）：「入 / 出 / 总 / tokens / 耗时 / 缓存」一行，标签用最淡档、数字用等宽中档，悬停提亮数字；时间旁悬停给完整时间戳，读屏取同一份完整文本。根规则见项目根目录 AGENTS.md。

### 外观与数据

- `UsageFootLook`：`Ink`（`FootInk` 三档文字）、`Gauge`（`FootGauge` 字号、间距）、`MonoFont`。
- `UsageFigures`：字段对应旧 GenerationUsage（缓存数由 `GenerationUsage.cachedCount` 算好，明细由 `formatDetail` 算好）；`StampText`：简短时刻（旧 `MessageTimestamp.format`）与完整时间。时刻由组合者用注入的时钟算，本库不读系统时间。

### 证据与旧测试

覆盖旧用例：`UiStabilityTests.streaming completion keeps the message footnote row geometry constant`（脚注部分）。本库在 headless 下验证：合计规则、只有合计与耗时、空用量、读屏名、悬停只提亮数字、时间与用量合一行与对齐。

## 成员 `UserBubble`

## UserBubble

用户消息（迁自旧 `Views/MessageCard.fs` 的 `renderWithStream` 用户分支）：右对齐的深色气泡（细描边、右下角收小、可聚焦、读屏名「用户消息」）+「你」头像；气泡内容由组装者放进来。`UserBubble.text` 给气泡正文。根规则见项目根目录 AGENTS.md。

### 外观

- `UserBubbleLook`：`Ink`（`BubbleInk` 气泡底、描边、文字、选区、头像底与字）、`Gauge`（`BubbleGauge` 描边、三档圆角、内边距、最大宽、头像边长与字号、间距）、`Dress`（可聚焦表面的焦点环，只走外阴影不改几何）、`LineHeight`（正文行高随字号）。

### 证据与旧测试

覆盖旧用例：`A11yRegressionTests.强调色悬停态与用户气泡上的文字过 AA`（配色由主题给，本库只取实例）、`DeliveryPolishingTests.message card renders user and assistant with reasoning and tools without error`（用户部分）。本库在 headless 下验证气泡几何、读屏名、头像位置、正文配色与行高、最大宽实时生效。
