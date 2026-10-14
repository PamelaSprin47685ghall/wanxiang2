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
