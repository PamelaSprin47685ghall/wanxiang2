# ConversationRows

侧栏对话列表（迁自旧 `Views/Sidebar.fs` 的 RenderRow、RenderMessageHit、RenderSearchStatus、RenderArchivedToggle、ApplyRowState、焦点与行键盘）。当前对话、多选、搜索会话、菜单内容归组合者；本库只画行、走行内键、发事件。根规则见项目根目录 AGENTS.md。

- `ConversationRows(look)`：`View`（ListBox）；`Render items` 换整份列表（保滚动偏移）。
  - 项：`RowItem.Header` / `Conversation` / `Hit` / `Status` / `ArchivedToggle`。`RowModel` 的标题/预览/读屏名/提示由组合者算好（含搜索分段）；`HitModel.Key` 是命中键。
  - `SetMarks(active, selected, inSelection)`：当前对话与多选高亮、更多键在多选下整批下线。
  - `SetCompact`：更多键命中面桌面 / 紧凑两档。
  - 焦点：`FocusRowAt` / `FocusRow` / `FocusArchivedToggle` / `ScrollTo` / `LeaveTop`（首行再 Up）/ `FocusedRow` / `VisibleIds`。
  - 事件：`RowActivated`、`RowFocused`、`MenuRequested(id, 锚点, 右对齐)`、`RenameKey`/`DeleteKey`/`PinKey`/`SelectAllKey`、`Escape`（`RowsEscapeArgs`，组合者置 `Handled`）、`HitClicked`、`StatusActionClicked`、`ArchivedToggleClicked`。
  - 可点件全是 `Tap`（读屏 Invoke + Enter/Space），与旧 ActionBorder 同义。

## 外观

- `ConversationRowsLook`：公共字段加 `Selected`/`Hover`/`SelectedHover`/`AccentSoft`、行几何（`RowMinHeight`…`RowContentSpacing`）、`IconButtonSize`/`CompactTarget`、`OpacitySubtle`、`Transitions`。`Glyph`：`more`、`pin`、`archive`、`chevron-up`、`chevron-down`。
- 样式类：行与更多键 `surface` + `hover-plain`；状态钮 `button-ghost`。

## 证据与旧测试

- CraftsmanshipPolishTests：`Sidebar conversation row activates on Enter and Space keys`、`Sidebar conversation row right click invokes context menu`、`Sidebar row more action stays reachable in compact mode`
- UiStabilityTests：`sidebar running pin and idle states keep the same title origin`
- InteractabilityPolishTests：`row more button leaves the hit tree in selection mode`（本库呈现部分）
- 新增：行键盘上下/Home/End、归档开关读屏与重建后焦点、命中行与状态动作、主题改行高

## 与旧实现的差异

- Avalonia 12 模板不再传入 `existing` 控件，回收靠虚拟化容器；同 id 快照刷新由组合者再 `Render`（或只 `SetMarks`）完成。
- 搜索高亮分段、预览去重、分叉标记、菜单条目由组合者准备后传入，本库不读 `ConversationSummary` / `SearchHit`。
