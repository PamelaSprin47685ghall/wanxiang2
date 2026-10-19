# BatchBar

合并包：BatchBar、ConversationRows、HostSwitcher、ListEmptyCard、SidebarBrand、SidebarSearch。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `BatchBar`

## BatchBar

多选模式的头条与底部批量操作条（迁自旧 `Views/Sidebar.fs` 的 selectionHeader、selectionActionBar 与 RefreshSelectionChrome 的控件部分）。选中集合与文案规则在 `SidebarSelection`；批量命令与确认归组合者。根规则见项目根目录 AGENTS.md。

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
  - 去脑化：废除内部 Dispatcher.UIThread.Post 争夺焦点，所有按钮用原生 Button 与样式类。

### 外观

- 样式类：行与更多键 `surface` + `hover-plain`；状态钮 `button-ghost`。
- CraftsmanshipPolishTests：`Sidebar conversation row activates on Enter and Space keys`、`Sidebar conversation row right click invokes context menu`、`Sidebar row more action stays reachable in compact mode`
- UiStabilityTests：`sidebar running pin and idle states keep the same title origin`
- InteractabilityPolishTests：`row more button leaves the hit tree in selection mode`（本库呈现部分）
- 新增：行键盘上下/Home/End、归档开关读屏与重建后焦点、命中行与状态动作、主题改行高

### 与旧实现的差异

- 废除 `ConversationRowsLook` 记录与 `Follow.live`。
- 搜索高亮分段、预览去重、分叉标记、菜单条目由组合者准备后传入，本库不读 `ConversationSummary` / `SearchHit`。

## 成员 `HostSwitcher`

## HostSwitcher

侧栏页脚的主机状态行与设置/命令面板入口（迁自旧 `Views/Sidebar.fs` 的 statusRow、serverCaption、statusDot、settingsButton、paletteButton、SetServerUrl、SetConnection）。重连与打开设置/面板的动作归组合者。根规则见项目根目录 AGENTS.md。

- `HostSwitcher()`：`View`；单入口 `Render(HostSwitcherVS)`，并保留 `SetServerUrl`、`SetConnection` 等接口。
  - 事件：`ReconnectClicked`、`SettingsClicked`、`PaletteClicked`。
  - 原生 `Button` 与 `Classes`。

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

- `ListEmptyCard()` 暴露 `View`、`ActionButton`、`Skeleton`。单入口 `Render(SidebarEmptyKind option)`。其余方法有 `Show`、`Hide`、`SetAction`、`SetActionVisible`、`FocusAction`。
- 事件：`ActionClicked`。
- 原生 `Button`、样式类。废除 `ListEmptyCardLook`。

### 与旧实现的差异
- 废除 `ListEmptyCardLook` 与 `Follow.live`。

## 成员 `SidebarBrand`

## SidebarBrand

侧栏顶部的品牌行，迁自旧 `Views/Sidebar.fs` 的 brandHeader、compactBackButton、newConversationButton、presetMenuButton。新建对话、预设菜单内容、关抽屉都由组合者负责。根规则见项目根目录 AGENTS.md。

- `SidebarBrand()` 暴露 `View`、`BackButton`、`NewButton`、`PresetButton`；单入口 `Render(SidebarBrandVS)`，保留 `SetPresetsVisible`、`SetCompact`。
- 事件：`NewClicked`、`PresetClicked`（参数是锚点）、`BackClicked`。所有可点控件用原生 `Button`。
### 与旧实现的差异
- 废除 `SidebarBrandLook` 与 `Follow.live`。

## 成员 `SidebarSearch`

## 成员 `SidebarSearch`

## SidebarSearch

侧栏搜索框与「未找到」提示（迁自旧 `Views/Sidebar.fs` 的 searchShell、clearSearchButton、searchEmptyHint、searchDebounce 与搜索框 KeyDown）。`SearchSession` / `SearchRequery` / `RowFocus` 与列表重建归组合者。根规则见项目根目录 AGENTS.md。

- `SidebarSearch()`：`View`（搜索区）、`Box`、`ClearButton`、`EmptyHint`。单入口 `Render(SearchBoxVS)`。
  - `Text` 读写；`Focus(selectAll)`；`SetEmptyHint` / `FocusEmptyHint`；`SetCompact`。
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
