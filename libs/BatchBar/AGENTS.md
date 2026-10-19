# BatchBar

合并包：BatchBar、ConversationRows、HostSwitcher、ListEmptyCard、SidebarBrand、SidebarSearch。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `BatchBar`

## BatchBar

多选模式的头条与底部批量操作条（迁自旧 `Views/Sidebar.fs` 的 selectionHeader、selectionActionBar 与 RefreshSelectionChrome 的控件部分）。选中集合与文案规则在 `SidebarSelection`；批量命令与确认归组合者。根规则见项目根目录 AGENTS.md。

- `BatchBar(look)`：`Header`、`ActionBar`；`Render BatchBarState`（计数、全选/置顶文案与启用、可见性）。
  - 事件：`ExitClicked`、`SelectAllClicked`、`PinClicked`、`ArchiveClicked`、`DeleteClicked`。
  - 底条三键左右方向键互跳（不可用邻键不接焦点）。
  - 可点件全是 `Tap`（Invoke）。

### 外观

- `BatchBarLook`：公共字段加 `HeaderSurface`/`BarSurface`/`Hairline`、`DividerWidth`、`SelectButtonPaddingY`。`Glyph`：`close`。
- 样式类：`button-ghost` / `button-secondary` / `button-danger`、图标钮 `icon-button`。

### 证据与旧测试

- SelectionOperabilityTests：`selection action buttons disable themselves while nothing is selected`、`selection buttons rename themselves as the selection changes`
- InteractabilityPolish8Tests：`selection action bar arrows walk the batch buttons`
- 新增：五键 Invoke 事件、分隔线跟主题

### 与旧实现的差异

- 文案与启用由组合者经 `SidebarSelection.Chrome` 算好再 `Render`；本库不持有选中集合。

## 成员 `ConversationRows`

## ConversationRows

侧栏对话列表（迁自旧 `Views/Sidebar.fs` 的 RenderRow、RenderMessageHit、RenderSearchStatus、RenderArchivedToggle、ApplyRowState、焦点与行键盘）。当前对话、多选、搜索会话、菜单内容归组合者；本库只画行、走行内键、发事件。根规则见项目根目录 AGENTS.md。

- `ConversationRows(look)`：`View`（ListBox）；`Render items` 换整份列表（保滚动偏移）。
  - 项：`RowItem.Header` / `Conversation` / `Hit` / `Status` / `ArchivedToggle`。`RowModel` 的标题/预览/读屏名/提示由组合者算好（含搜索分段）；`HitModel.Key` 是命中键。
  - `SetMarks(active, selected, inSelection)`：当前对话与多选高亮、更多键在多选下整批下线。
  - `SetCompact`：更多键命中面桌面 / 紧凑两档。
  - 焦点：`FocusRowAt` / `FocusRow` / `FocusArchivedToggle` / `ScrollTo` / `LeaveTop`（首行再 Up）/ `FocusedRow` / `VisibleIds`。
  - 事件：`RowActivated`、`RowFocused`、`MenuRequested(id, 锚点, 右对齐)`、`RenameKey`/`DeleteKey`/`PinKey`/`SelectAllKey`、`Escape`（`RowsEscapeArgs`，组合者置 `Handled`）、`HitClicked`、`StatusActionClicked`、`ArchivedToggleClicked`。
  - 可点件全是 `Tap`（读屏 Invoke + Enter/Space），与旧 ActionBorder 同义。

### 外观

- `ConversationRowsLook`：公共字段加 `Selected`/`Hover`/`SelectedHover`/`AccentSoft`、行几何（`RowMinHeight`…`RowContentSpacing`）、`IconButtonSize`/`CompactTarget`、`OpacitySubtle`、`Transitions`。`Glyph`：`more`、`pin`、`archive`、`chevron-up`、`chevron-down`。
- 样式类：行与更多键 `surface` + `hover-plain`；状态钮 `button-ghost`。

### 证据与旧测试

- CraftsmanshipPolishTests：`Sidebar conversation row activates on Enter and Space keys`、`Sidebar conversation row right click invokes context menu`、`Sidebar row more action stays reachable in compact mode`
- UiStabilityTests：`sidebar running pin and idle states keep the same title origin`
- InteractabilityPolishTests：`row more button leaves the hit tree in selection mode`（本库呈现部分）
- 新增：行键盘上下/Home/End、归档开关读屏与重建后焦点、命中行与状态动作、主题改行高

### 与旧实现的差异

- Avalonia 12 模板不再传入 `existing` 控件，回收靠虚拟化容器；同 id 快照刷新由组合者再 `Render`（或只 `SetMarks`）完成。
- 搜索高亮分段、预览去重、分叉标记、菜单条目由组合者准备后传入，本库不读 `ConversationSummary` / `SearchHit`。

## 成员 `HostSwitcher`

## HostSwitcher

侧栏页脚的主机状态行与设置/命令面板入口（迁自旧 `Views/Sidebar.fs` 的 statusRow、serverCaption、statusDot、settingsButton、paletteButton、SetServerUrl、SetConnection）。重连与打开设置/面板的动作归组合者。根规则见项目根目录 AGENTS.md。

- `HostSwitcher(look)`：`View`；`SetServerUrl`（显示 Authority，非法则「选择主机」）、`SetConnection`、`ConfigurePalette`、`SetCompact`。
  - 事件：`ReconnectClicked`、`SettingsClicked`、`PaletteClicked`。
  - 可点件全是 `Tap`（Invoke）。

### 外观

- `HostSwitcherLook`：公共字段加 `DotSize`、`BarHeight`、`DividerWidth`、`IconButtonSize`/`CompactTarget`、`TextButtonMinHeight`、`Hairline`、`RadiusSm`。`Glyph`：`gear`、`search`。
- 样式类：状态行 `surface hover-plain`；图标钮 `icon-button`。

### 证据与旧测试

- 覆盖旧侧栏连接行呈现与点击重连的行为（组合者接 `actions.reconnect`）。
- 新增：Authority 解析、设置/面板事件、紧凑命中面、页脚高跟主题

### 与旧实现的差异

- 导入按钮已移出页脚（决策 363）；本库不含导入入口。
- 品牌行与「新建对话」在组合者，不在本库。

## 成员 `ListEmptyCard`

## ListEmptyCard

列表空态卡，迁自旧 `Views/Sidebar.fs` 的 emptyStateHost、emptySkeleton、emptyActionButton，以及 Rebuild 末段的三张卡。要显示哪种空态、用什么文案，由组合者按 `SidebarEmpty.decide` 决定。根规则见项目根目录 AGENTS.md。

- `ListEmptyCard(look)` 暴露 `View`、`ActionButton`、`Skeleton`。`Show(style, title, note, skeletonVisible, actionVisible)` 只在样式变化时换卡；骨架和行动键跨卡复用，所以焦点和读屏名保持连续。其余方法有 `Hide`、`SetAction`、`SetActionVisible`、`FocusAction`。
- `EmptyCardStyle`（限定访问）：`Note` 对应未连接，`Brand` 对应加载中，`Plain` 对应还没有对话。
- 事件：`ActionClicked`。

### 外观

- `ListEmptyCardLook` 在公共字段之外加了 `Logo`、`LogoSize`、`SkeletonFill`、`SkeletonOpacity`、`SkeletonBarHeight`、`RadiusSm`、`Space3xl`、`Space6xl`。

### 证据与旧测试

- 旧用例需要起完整侧栏，由 SidebarDesk 承接。本库新增两个用例：换卡时机与共享行动键；骨架跟随主题改值。

## 成员 `SidebarBrand`

## SidebarBrand

侧栏顶部的品牌行，迁自旧 `Views/Sidebar.fs` 的 brandHeader、compactBackButton、newConversationButton、presetMenuButton。新建对话、预设菜单内容、关抽屉都由组合者负责。根规则见项目根目录 AGENTS.md。

- `SidebarBrand(look)` 暴露 `View`、`BackButton`、`NewButton`、`PresetButton`；方法 `SetPresetsVisible`（有预设时才出现）、`SetCompact`（显示返回键，各键抬到触控下限）。
- 事件：`NewClicked`、`PresetClicked`（参数是锚点）、`BackClicked`。所有可点的控件都用 `Tap`，读屏可 Invoke，Enter 和 Space 也能触发。

### 外观

- `SidebarBrandLook` 在公共字段之外加了 `Logo`（宿主给的图形，参数是尺寸）、`LogoSize`、`DisplayTracking`、`BarHeight`、`IconButtonSize`、`CompactTarget`、`TextButtonMinHeight`。`Glyph` 用到 `arrow-left`、`chevron-down`。

### 证据与旧测试

- 旧用例需要起完整侧栏，由 SidebarDesk 承接。本库新增两个用例：三键事件；紧凑态与主题改值。

## 成员 `SidebarSearch`

## SidebarSearch

侧栏搜索框与「未找到」提示（迁自旧 `Views/Sidebar.fs` 的 searchShell、clearSearchButton、searchEmptyHint、searchDebounce 与搜索框 KeyDown）。`SearchSession` / `SearchRequery` / `RowFocus` 与列表重建归组合者。根规则见项目根目录 AGENTS.md。

- `SidebarSearch(look)`：`View`（搜索区）、`Box`、`ClearButton`、`EmptyHint`。
  - `Text` 读写；`StopDebounce`；`Focus(selectAll)`；`SetEmptyHint` / `FocusEmptyHint`；`SetCompact`。
  - 事件：`Changed`（每次输入）、`Debounced`（防抖到点）、`Keys`（Down/Enter/Escape，`SearchKeyArgs.Handled`）、`ClearRequested`（清空键与空结果提示）。
  - 清空键槽位常驻：无字时透明度 0、不可点、读屏 Raw（与旧 `setReservedActionVisible` 同义）。
  - 可点件全是 `Tap`（Invoke）。

### 外观

- `SidebarSearchLook`：公共字段加 `DebounceMs`、`GlyphSize`、`IconButtonSize`/`CompactTarget`、`Space6xl`。`Glyph`：`search`、`close`。
- 样式类：输入壳 `surface hover-raised focus-soft`；图标钮与空结果提示 `surface icon-button/hover-plain`。

### 证据与旧测试

- CraftsmanshipPolishTests：`Sidebar search clear button has accessible tooltip and automation properties`、`Sidebar search navigation moves focus and handles escape`
- 新增：防抖合并输入、空结果提示点击/Enter/Esc 清空

### 与旧实现的差异

- 防抖到点只发 `Debounced` 事件；是否 `SearchRequery.shouldQuery`、是否 `BeginMessageSearch` 由组合者定。
