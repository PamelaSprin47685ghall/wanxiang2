# BatchBar

合并包：BatchBar、ConversationRows、HostSwitcher、ListEmptyCard、SidebarBrand、SidebarSearch。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `BatchBar`

## BatchBar

多选模式的头条与底部批量操作条（迁自旧 `Views/Sidebar.fs` 的 selectionHeader、selectionActionBar 与 RefreshSelectionChrome 的控件部分）。**选中集合与多选模式的事实都在状态树**（`libs/WorkspaceState` 的 `Chrome.BatchMode` / `Chrome.SelectedRowIds`），本包不保留第二份记账；`SidebarSelection` 只剩「按可见行过滤已选项」这一个不产生新事实的派生读法。批量命令与确认归组合者。根规则见项目根目录 AGENTS.md。

- `BatchBar()`：`Header`、`ActionBar`；单入口幂等 `Render(BatchBarVS option)`（None 隐藏；计数、全选/置顶文案由 ViewState 驱动）。
  - 事件：`ExitClicked`、`SelectAllClicked`、`PinClicked`、`ArchiveClicked`、`DeleteClicked`。
  - 底条三键左右方向键互跳（不可用邻键不接焦点）。
  - 原生 `Button` 与标准样式类（`secondary` / `ghost` / `danger`）。

### 外观

- 样式类：`button-ghost` / `button-secondary` / `button-danger`、图标钮 `icon-button`。

### 证据与旧测试

- SelectionOperabilityTests：`selection action buttons disable themselves while nothing is selected`、`selection buttons rename themselves as the selection changes`
- InteractabilityPolish8Tests：`selection action bar arrows walk the batch buttons`
- 新增：五键 Invoke 事件、分隔线跟主题

### 与旧实现的差异

- 废除 `BatchBarLook` 记录与 `Follow.live` 逐属性订阅，全部接入 Avalonia 样式类与主题资源。
- 暴露单入口 `Render(BatchBarVS option)`，None 时自动收起。

## 成员 `ConversationRows`

## ConversationRows

侧栏对话列表（迁自旧 `Views/Sidebar.fs` 的 RenderRow、RenderMessageHit、RenderSearchStatus、RenderArchivedToggle、ApplyRowState、焦点与行键盘）。

- `ConversationRows()`：`View`（ListBox）；`Render items` 换整份列表；支持单行适配 `Render(SidebarRowVS)`。
  - 项：`RowItem.Header` / `Conversation` / `Hit` / `Status` / `ArchivedToggle`。
  - 焦点：`FocusRowAt` / `FocusRow` / `FocusArchivedToggle` / `ScrollTo` / `LeaveTop`（首行再 Up）/ `FocusedRow` / `VisibleIds`。
  - 事件：`RowActivated`、`RowFocused`、`MenuRequested(id, 锚点, 右对齐)`、`RenameKey`/`DeleteKey`/`PinKey`/`SelectAllKey`、`Escape`（`RowsEscapeArgs`，组合者置 `Handled`）、`HitClicked`、`StatusActionClicked`、`ArchivedToggleClicked`。
  - ApplyMarks(active, selected, inSelection)：写当前对话高亮、多选高亮与多选态。
  - 去脑化：废除内部 Dispatcher.UIThread.Post 争夺焦点，所有按钮用原生 Button 与样式类。

### 外观

- 样式类：行与更多键 `surface` + `hover-plain`；状态钮 `button-ghost`。
- CraftsmanshipPolishTests：`Sidebar conversation row activates on Enter and Space keys`、`Sidebar conversation row right click invokes context menu`、`Sidebar row more action stays reachable`
- UiStabilityTests：`sidebar running pin and idle states keep the same title origin`
- InteractabilityPolishTests：`row more button leaves the hit tree in selection mode`（本库呈现部分）
- 新增：行键盘上下/Home/End、归档开关读屏与重建后焦点、命中行与状态动作、主题改行高

### 与旧实现的差异

- 废除 `ConversationRowsLook` 记录与 `Follow.live`。
- 搜索高亮分段、预览去重、分叉标记、菜单条目由组合者准备后传入，本库不读 `ConversationSummary` / `SearchHit`。

## 成员 `HostSwitcher`

## HostSwitcher

侧栏页脚的主机状态行与设置/命令面板入口（迁自旧 `Views/Sidebar.fs` 的 statusRow、serverCaption、statusDot、settingsButton、paletteButton）。重连与打开设置/面板的动作归组合者。根规则见项目根目录 AGENTS.md。

- `HostSwitcher()`：`View`；单入口 `Render(HostSwitcherVS)`（`ServerUrl` / `Connected` / `StatusText`）。
  - 事件：`ReconnectClicked`、`SettingsClicked`、`PaletteClicked`。
  - 原生 `Button` 与 `Classes`。
- 例外插槽（命名避开 Set*）：`SlotPalette(visible)`（命令面板键是否出现，由宿主自己知道，不在每帧投影里）。

### 外观

- 样式类：状态行 `surface hover-plain`；图标钮 `icon-button`。

### 证据与旧测试

- 覆盖旧侧栏连接行呈现与点击重连的行为（组合者接 `actions.reconnect`）。
- 新增：Authority 解析、设置/面板事件、紧凑命中面、页脚高跟主题

### 与旧实现的差异
- 废除 `HostSwitcherLook` 与 `Follow.live`。

- 导入按钮已移出页脚（决策 363）；本库不含导入入口。
- 品牌行与「新建对话」在组合者，不在本库。

## 成员 `ListEmptyCard`

## ListEmptyCard

列表空态卡，迁自旧 `Views/Sidebar.fs` 的 emptyStateHost、emptySkeleton、emptyActionButton，以及 Rebuild 末段的三张卡。

- `ListEmptyCard()` 暴露 `View`、`ActionButton`、`Skeleton`。单入口 `Render(SidebarEmptyKind option)`（行动键可见性由 `SidebarEmptyKind.NotConnected` 携带）。其余方法有 `Show`、`Hide`、`FocusAction`。
- 事件：`ActionClicked`。
- 原生 `Button`、样式类。废除 `ListEmptyCardLook`。

### 与旧实现的差异
- 废除 `ListEmptyCardLook` 与 `Follow.live`。

## 成员 `SidebarBrand`

## SidebarBrand

侧栏顶部的品牌行，迁自旧 `Views/Sidebar.fs` 的 brandHeader、compactBackButton、newConversationButton、presetMenuButton。新建对话、预设菜单内容、关抽屉都由组合者负责。根规则见项目根目录 AGENTS.md。

- `SidebarBrand()` 暴露 `View`、`BackButton`、`NewButton`、`PresetButton`、`LogoSlot`；单入口 `Render(SidebarBrandVS)`（`PresetsVisible` / `Compact`），图标挂载走例外插槽 `SlotLogo`。
- 事件：`NewClicked`、`PresetClicked`（参数是锚点）、`BackClicked`。所有可点控件用原生 `Button`。
### 与旧实现的差异
- 废除 `SidebarBrandLook` 与 `Follow.live`。

## 成员 `SidebarSearch`

## 成员 `SidebarSearch`

## SidebarSearch

侧栏搜索框与「未找到」提示（迁自旧 `Views/Sidebar.fs` 的 searchShell、clearSearchButton、searchEmptyHint、searchDebounce 与搜索框 KeyDown）。`SearchSession` / `SearchRequery` / `RowFocus` 与列表重建归组合者。根规则见项目根目录 AGENTS.md。

- `SidebarSearch()`：`View`（搜索区）、`Box`、`ClearButton`、`EmptyHint`。单入口 `Render(SearchBoxVS)`。
  - `Text` 读写；`Focus(selectAll)`；`ApplyEmptyHint` / `FocusEmptyHint`。
  - 事件：`Changed`（即时上报，不自开定时器）、`Debounced`、`Keys`（Down/Enter/Escape，`SearchKeyArgs.Handled`）、`ClearRequested`。
  - 清空键槽位常驻：无字时透明度 0、不可点、读屏 Raw（与旧 `setReservedActionVisible` 同义）。
  - 原生 `Button`、`TextBox`（挂 `control-stroke`）。

### 外观
- 样式类：输入壳 `surface hover-raised focus-soft`；图标钮与空结果提示 `surface icon-button/hover-plain`。
- 废除 `SidebarSearchLook`。

- CraftsmanshipPolishTests：`Sidebar search clear button has accessible tooltip and automation properties`、`Sidebar search navigation moves focus and handles escape`
- 新增：防抖合并输入、空结果提示点击/Enter/Esc 清空

### 与旧实现的差异

- 防抖到点只发 `Debounced` 事件；是否 `SearchRequery.shouldQuery`、是否 `BeginMessageSearch` 由组合者定。

## 成员 `RowFocus`

### RowFocus

侧栏行焦点（迁自旧 `Views/Sidebar.fs` 的 `visibleRowIds`、`focusedRowId`、`focusedRowIndex`、`pendingFocusId`，方法 `NeighborIndexAfterDelete`、`NeighborAfterDelete`、`FocusAfterDelete`，行 GotFocus 的记录与 `Rebuild` 的焦点还原）。公开成员：`ApplyVisible`、`Visible`、`FocusedId`、`Focused`、`NeighborIndexAfterDelete`、`NeighborAfterDelete`、`FocusAfterDelete`、`RestoreAfterRebuild`。控件聚焦与 `GetVisibleOrder` 的字符串化归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **打开谁与焦点落谁同一个答案**（契约 C3）：下一行优先，其次上一行；只剩一行时没有邻位。
2. **删除意图只用一次**：重建后优先聚焦意图行；意图行不在了就作废，不落到别处。

### SPEC
1. [LEMMA: 1, 2] `tests/RowFocus.RowFocus.Tests.fs` 通过。

### 迁入或覆盖的旧测试
- KeyConvergenceTests：`neighbor after delete matches the row focus falls to`、`no neighbor when the list becomes empty`（行序直接给出；原用例经真实侧栏排序）

### 与旧实现的差异
无。意图行不在了时，旧 Rebuild 仍会还原重建前正聚焦且仍可见的行，本库相同（第四个用例末段）。

## 成员 `SearchSegments`

### SearchSegments（库）

旧 `Views/Sidebar.fs` 里的 `SidebarText` 模块迁入（区间合并按 SPEC 2 修正）：侧栏搜索的分词（terms）和命中分段（segments）。上色仍由侧栏视图做。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **分词和分段结果与旧实现相同。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/RowFocus.SearchSegments.Tests.fs` 通过：`empty query yields one plain segment covering the whole text`、`whitespace-only query is treated as no query`、`single term highlights only the matched substring, keeping original casing`、`segments always reassemble the original text exactly`、`every term that matched the row is highlighted`、`adjacent matches cover the whole text without gaps`、`overlapping occurrences of one term are not double counted`、`overlapping hits of different terms do not duplicate characters`、`a hit contained in another is absorbed`、`separated hits stay separate`、`a term that does not occur leaves the text unstyled`、`terms helper splits on spaces and tabs and drops duplicates`、`null text is one empty plain segment`。
2. 有意改动（只影响屏幕显示，不涉及磁盘和网络字节）：旧实现的合并 fold 写成 `(sorted, []) ||> List.fold`，状态和列表位置颠倒，从不合并，不同词的命中互相重叠时还会重复取字。现在命中区间排序后，重叠或紧邻的合并为一段命中；拼回的文本恒等于原文，每个字只出现一次。

### 迁入或覆盖的旧测试
旧 SearchHighlightTests 第 1 节（去掉 Headless 包装）：
- `empty query yields one plain segment covering the whole text`
- `whitespace-only query is treated as no query`
- `single term highlights only the matched substring, keeping original casing`
- `segments always reassemble the original text exactly`
- `every term that matched the row is highlighted`（去掉了对 ConversationSummary.matches 的断言，那是写手 2 的模型；期望改为一段 `模型选型`）
- `adjacent matches cover the whole text without gaps`（期望由两段改为一段）
- `overlapping occurrences of one term are not double counted`（期望由两段命中改为一段）
- `a term that does not occur leaves the text unstyled`
- `terms helper splits on spaces and tabs and drops duplicates`

本包新增：null 文本、不同词重叠不重复取字、被包含的命中并入、分离的命中保持分离、片段恒能拼回原文。

## 成员 `SearchSession`

### SearchSession

消息正文搜索的会话状态（迁自旧 `Views/Sidebar.fs` 的 `searchHits`、`searchHitsQuery`、`searchPending`、`searchHasMore`、`searchTruncated`、`searchError`，方法 `BeginMessageSearch`、`LoadMoreMessageHits`、`RetryMessageSearch`、`ApplySearchResults`、`ApplySearchFailure`、`MessageHits`，`SetConnection` 断线清在途，`Rebuild` 命中区与状态行文字）。命中类型是类型参数，不认识 SearchHit。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

与 SearchRequery 的边界：SearchRequery 只回答「防抖到点要不要重发」，本库拥有会话状态；宿主把 `Query` 当作 SearchRequery 的上次查询词。防抖计时器、发请求、行控件归宿主。

### LEMMA
1. **命中只属于发起它的词。** 词变了旧命中不显示；迟到的旧词结果与失败都丢弃。
2. **在途时不重复请求「显示更多」**；断线清掉在途标志。
3. **状态行文字与旧 Rebuild 逐字相同。**

### SPEC
1. [LEMMA: 1, 2, 3] `tests/RowFocus.SearchSession.Tests.fs` 通过。

### 迁入或覆盖的旧测试
- LibraryTests：`sidebar shows hit snippets under message content and clicking opens the hit`（状态部分；片段着色、点击打开要起窗口，留旧仓库）

## 成员 `SidebarSelection`

### SidebarSelection（库）

**多选的事实在状态树，不在这里。** `Chrome.BatchMode`（是否多选）与 `Chrome.SelectedRowIds`（选中了哪些对话）是唯一事实，由 `libs/WorkspaceState` 持有，经 `SidebarVS.BatchMode` / `SidebarVS.SelectedRowIds` 投影给侧栏。

本类型因此**不再持有任何状态**——原来�� `Enter`、`Toggle`、`Exit`、`ToggleAllVisible`、`SelectedIds`、`IsSelected`、`Chrome` 全都删掉了。两份记账会漂：状态树说选了 A、B，侧栏自己那份说只选了 A，批量条与行高亮就对不上，而且没有任何一处能解释谁对。

留下来的只有一个**不产生新事实**的派生读法：`SidebarSelection.SelectedIds visible selected`——按可见行过滤已选项，顺序跟可见行走。旧 `Chrome`（计数、全选与置顶文案、启用、条可见）也删掉了：计数与文案是**可见范围**的派生，而「可见行」是侧栏的事实（搜索框与归档开关都在侧栏），状态树看不到，所以由侧栏组装 `BatchBarVS`。不引用任何其他库。根规则见项目根目录 AGENTS.md。

### LEMMA
1. **多选只有一个事实来源，派生读法不产生第二份记账。** [根 AXIOM: 5]

### SPEC
1. [LEMMA: 1] `tests/RowFocus.SidebarSelection.Tests.fs` 通过：`selected ids follow the visible rows only`、`selected ids keep the visible row order`、`nothing selected yields nothing`。
2. 多选的状态机规则（进多选 / 切换 / 全选可见 / 退出；取消最后一项即退出；全选可见会换掉不可见的旧选择）现在在 `libs/WorkspaceState/tests/WorkspaceStateTests.fs` 里，用例名以 `ToggleBatch` / `ToggleBatchAllVisible` / `ExitBatch` / `多选投影` 开头。

### 迁入或覆盖的旧测试
旧侧栏多选用例要起窗口，留在旧仓库。本包新增 3 条**派生读法**规则用例；多选状态机规则迁到 `libs/WorkspaceState`。

### 合并说明

2026-10-21（W7-CLEANBREAK）并入本包：源文件按各成员**原工程的编译顺序**落盘为本包源文件；
测试按 MERGE.md《测试工程的合并约定》改名 `tests/RowFocus.<原文件名>.fs`，顶层模块改名 `<成员名><模块名>`；
原 `Wanxiang2.RowFocus` 包不再交付，消费方改引 `Wanxiang2.BatchBar`。命名空间与类型名不变，消费方源码零改。
