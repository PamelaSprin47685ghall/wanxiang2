# BatchBar

多选模式的头条与底部批量操作条（迁自旧 `Views/Sidebar.fs` 的 selectionHeader、selectionActionBar 与 RefreshSelectionChrome 的控件部分）。选中集合与文案规则在 `SidebarSelection`；批量命令与确认归组合者。根规则见项目根目录 AGENTS.md。

- `BatchBar(look)`：`Header`、`ActionBar`；`Render BatchBarState`（计数、全选/置顶文案与启用、可见性）。
  - 事件：`ExitClicked`、`SelectAllClicked`、`PinClicked`、`ArchiveClicked`、`DeleteClicked`。
  - 底条三键左右方向键互跳（不可用邻键不接焦点）。
  - 可点件全是 `Tap`（Invoke）。

## 外观

- `BatchBarLook`：公共字段加 `HeaderSurface`/`BarSurface`/`Hairline`、`DividerWidth`、`SelectButtonPaddingY`。`Glyph`：`close`。
- 样式类：`button-ghost` / `button-secondary` / `button-danger`、图标钮 `icon-button`。

## 证据与旧测试

- SelectionOperabilityTests：`selection action buttons disable themselves while nothing is selected`、`selection buttons rename themselves as the selection changes`
- InteractabilityPolish8Tests：`selection action bar arrows walk the batch buttons`
- 新增：五键 Invoke 事件、分隔线跟主题

## 与旧实现的差异

- 文案与启用由组合者经 `SidebarSelection.Chrome` 算好再 `Render`；本库不持有选中集合。
