# SidebarDesk（组合者）

SidebarBrand、SidebarSearch、ConversationRows、ListEmptyCard、BatchBar、HostSwitcher 这些控件，加上 SearchSession、SearchRequery、RowFocus、SidebarSelection、SidebarEmpty、ConversationSummary、ForkLineage（ConversationList）、SearchSegments、SearchHit 这些协作库，一起构成旧 `Views/Sidebar.fs`。根规则见项目根目录 AGENTS.md。

- 宿主入口是 `SidebarDesk(theme, overlay, actions: SidebarDeskActions, brandLogo)`，`View` 放进主布局。`SidebarDeskActions` 的字段与旧 `SidebarActions` 一一对应。
- 成员名：`ResetSearch`、`SearchHandler`、`OpenSearchHit`、`BeginMessageSearch`、`ApplySearchResults`、`ApplySearchFailure`、`SearchQueryText`、`MessageHits`、`ConfigureImport`（空实现）、`PaletteButton`、`PresetMenuButton`、`ConnectionText`、`FocusSearch`、`GetVisibleOrder`、`NeighborAfterDelete`、`FocusAfterDelete`、`EnterSelection`、`ToggleSelected`、`ExitSelection`、`ToggleSelectAllVisible`、`SelectedIds`、`IsSelectionMode`。另外暴露 `Rows`、`Search`、`Batch`、`Host`、`Brand`、`EmptyCard`，供宿主和测试读取状态。
- 渲染入口已收拢为 `Render(vs: SidebarVS)`，实现单入口幂等投影；品牌行与页脚都经各自的 `Render`（`brand.Render(vs.Brand)` / `hostBar.Render(vs.Host)`），组合者不再旁路写这两个控件。
- 例外插槽/命令（命名避开 Set*，非每帧投影）：`SlotPresets`（预设下拉插槽：改 `lastVS.Brand.PresetsVisible` 后重渲染品牌行）、`SlotPalette`（命令面板入口插槽）、`ApplySearchText`（命令面板全文搜索命令）、`ApplyServerUrl`（服务器 URL 命令：改投影值后重渲染页脚）。
  谱系不再走旁路插槽：分叉谱系随 `SidebarVS.Lineage` 一起进（`Render` 是唯一入口），旧的 `SlotLineage` 已删。
- 在这里完成的协作：
  - 每次重建时按 ConversationSummary 分组筛选，组装行模型（搜索高亮分段、预览去重、分叉前缀、同名区分、谱系提示），追加消息命中区和状态行。
  - 重建后的焦点还原走 RowFocus。
  - 多选时更新批量条，并让品牌行和页脚让位。
  - 行菜单按 id 取最新快照，多选时不打开。
  - Esc 按顺序处理：行在多选中时退出多选；否则先清空搜索词，再退出多选，最后聚焦当前对话。
  - 搜索框的 Down 和 Enter 出口与旧实现相同。
  - 防抖到期时用 SearchRequery 判断词是否变了，没变就不重发。
  - 断线时清掉在途的搜索。
  - 紧凑抽屉中按 Tab 只在侧栏内循环（OverlayDesk.TrapTab）。
- 主题条目：新增 `Space.TrackingDisplay`、`DerivedBrush.HoverSelected`、`Metric.SidebarSkeletonBarHeight`。

## P3 架构演进标记
- `RowFocus`、`SearchSession`、`SidebarSelection`：目前在 SidebarDesk 内部维持实例以支撑行焦点还原、多选判定与消息搜索命中分页。P3 阶段将随单状态树收拢，其时序状态直接交由 `WorkspaceState` 的 `WorkspaceModel.Chrome` / `Effect.Focus` 接管，彻底废除组合者内部可变状态机。

## 证据与旧测试

- CraftsmanshipPolishTests：`Sidebar conversation row right click invokes context menu`、`Sidebar search clear button has accessible tooltip and automation properties`、`Sidebar search navigation moves focus and handles escape`
- SelectionOperabilityTests：`entering selection mode turns row clicks into selection toggles`、`deselecting the last item exits selection mode`、`escape exits selection mode from the archived toggle row`、`batch pin and archive issue one command per selected conversation`、`escape in selection mode exits the mode even with a search query`、`selection action buttons disable themselves while nothing is selected`、`selection mode gives up the brand header and footer`、`row context menu offers the selection entry`、`selection buttons rename themselves as the selection changes`、`setting the active conversation scrolls it into view`、`pin key follows the selected items' pinned state alone`、`batch delete with an empty selection never reaches the batch action`
- KeyConvergenceTests：`p key on a focused row toggles pin`、`ctrl p on a focused row is left for the global table`、`page up on a sidebar row scrolls the list to the first item`
- InteractabilityPolishTests：`row context menu stays closed while the sidebar is in selection mode`、`escape in the search box leaves selection mode first`
- LibraryTests：`sidebar shows hit snippets under message content and clicking opens the hit`、`sidebar marks fork rows and does not search when no handler is wired`
- UiStabilityTests：`changing active sidebar row does not rebuild the conversation list`、`sidebar empty state separates list loading from confirmed empty and embeds primary actions`
- SearchHighlightTests：`title and preview both highlight while searching`、`clearing the query removes every highlight`、`recycled rows keep their highlight after a snapshot refresh`
- 留在旧仓库、需要整窗或长时间运行的用例：5000 行与 20000 行回收压测、soak、MainLayout 相关用例。

## 与旧实现的差别

- 导入入口在决策 363 中已经移到设置页，`ConfigureImport` 只保留签名。
- Avalonia 12 的模板不再传入已有控件，同 id 快照通过重新 Render 刷新。高亮在每次渲染时重新上色，所以不会因复用而丢失。
