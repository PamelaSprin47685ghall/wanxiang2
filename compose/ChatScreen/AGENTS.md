# ChatScreen（组合者）

ChatHeader + ChatPane + MessageCardView（MessageCards、CardDeck）+ ComposerDesk + ApprovalDesk + OverlayDesk + MountWindow + RevealFlash + BreadcrumbAction + ExchangeJump + MessageView + ForkLineage = 旧 `Views/ChatView.fs` 整体，以及旧 AppShell 里对话列的接线（审批卡放进输入区附属位、待批准集合变了就重绘）。根规则见项目根目录 AGENTS.md。

- 宿主入口：`ChatScreen(ChatScreenLooks.ofTheme manager styles, cards: MessageCards, composer: ComposerDesk, approvals: ApprovalDesk, overlay: OverlayDesk, brandLogo, actions: ChatScreenActions)`。
- `ChatScreenActions`：`Card`（CardActions）、`RenameTitle`、`ToggleSidebar`、`OpenSessionSettings`、`ForkFromHere`、`ExportConversation`、`StopGeneration`、`RequestOlderHistory`、`FocusHome`、`OpenParent`、`SiblingSelected`、`After`（延时回调，定位高亮停留用；计时器归宿主）。
- 渲染：`Render(ScreenFrame)` 同步完成整条次序：挂载窗口规划（决策 293）→ 卡片按身份对账（ScreenKey，同旧 CardKey）→ 流式卡外形不变时原位 `MarkdownStream.Update`，换形重建 → 半截回复与错误卡追加 → 跳转锚点校验，需要续跳时在前置锚点恢复之后跳 → 贴底跟随（ChatPane.Rendered）。`InvalidateCards()` 丢缓存。
- 滚动：`ScrollToMessage`、`RevealAndHighlight`（高亮承载面按 `MessageCards.HighlightName` 找，停留由 `actions.After` 回调结束）、`EndRevealHighlight`、`SmoothScrollToEnd`、`SmoothScrollToBeginning`、`ScrollToBeginning`、`ScrollToEndDeferred`、`JumpExchange`、`ExpandMountWindow`、`Begin/Restore/CancelHistoryPrependAnchor`、`ResetScrollState`。钉住期间内容高度变了用 `ChatPane.PinOffset` 重新对齐（决策 460），不被当成用户上滚。到顶时本机还有未挂消息先挂（只认用户上推），否则向主机要更早一页；上一页刚失败时不静默重发（决策 310）。
- 顶栏：`SetTitle`、`SetGenerating`、`SetCanStop`、`SetConversationChrome`、`SetBreadcrumb(ForkBreadcrumb option)`、`BreadcrumbText`、`ShowMenu()`（「对话操作」分组菜单与旧 ConfigureMenu 同项同序）、`SetSavePresetAction`、`SetCompactMode`、`SetStartMode`。
- 其他：`SetSiblingProvider`（版本条）、`SetAwaitingApproval`、`SetPartialReply`、`Header`、`Pane`、`Deck`、`Mounted`、`HiddenMessageCount`、`HighlightedCommit`、`LastFlashedCommit`。

## 证据与旧测试
- 无头行为用例四条：渲染次序（复用、流式原位更新、换形重建、错误卡）、长对话挂载窗口与定位高亮（宿主计时结束高亮）、顶栏改名 / 侧栏 / 面包屑、版本条。
- 覆盖旧 ChatViewTests 中 RenderMessages、RevealAndHighlight、ScrollToMessage、SetBreadcrumb / ActivateBreadcrumb、ConfigureMenu 的状态部分；顶栏控件的行为用例在 libs/ChatHeader，滚动 / 空态 / 骨架的在 ChatPane。

## 与旧实现的差异
- 定位高亮停留不再在这里起 DispatcherTimer，改由宿主经 `actions.After` 计时。
- 顶栏模型芯片按决策 350 恒隐藏，没有迁移；宿主找模型锚点一律用 `ComposerDesk.ModelAnchor()`。
- 渲染参数收成 `ScreenFrame` 记录；`SiblingProvider` / `SiblingSelected` 属性改为 `SetSiblingProvider` 与动作字段。

## 整树金样

- `tests/golden/screen.txt`：旧 ChatView（extract-ledger 533b85b）用 `tools/screengold/run.sh <旧仓>` 生成，10 个状态：空态、对话、宽屏 1000、改名中、生成中、流式、面包屑可点 / 父对话已删、窄屏 400（compact + 生成中 + 面包屑）、只读标题。顶栏部分即 ChatHeader 的整树证据。
- 两边都装共享 ThemeStyles、减弱动效（状态过渡瞬时落定）、内嵌字体；旧顶栏恒不可见的模型芯片（决策 350）在生成器里摘掉。
- 对齐时修过的库差异：顶栏高度挂在外层 Border、右组 / 状态行 / 面包屑行垂直居中、生成中芯片初始不写 AccessibilityView、可点面包屑读屏报按钮、根面为 Border 派生类型。

- 性能：tests/PerfTests.fs 迁自旧 IncrementalRenderTests「长对话的流式刷新代价与消息条数无关」（200 条历史，流式每帧 < 20ms）；CardDeck 身份泛型化，直接用 ScreenKey 结构相等。
