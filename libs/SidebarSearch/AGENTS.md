# SidebarSearch

侧栏搜索框与「未找到」提示（迁自旧 `Views/Sidebar.fs` 的 searchShell、clearSearchButton、searchEmptyHint、searchDebounce 与搜索框 KeyDown）。`SearchSession` / `SearchRequery` / `RowFocus` 与列表重建归组合者。根规则见项目根目录 AGENTS.md。

- `SidebarSearch(look)`：`View`（搜索区）、`Box`、`ClearButton`、`EmptyHint`。
  - `Text` 读写；`StopDebounce`；`Focus(selectAll)`；`SetEmptyHint` / `FocusEmptyHint`；`SetCompact`。
  - 事件：`Changed`（每次输入）、`Debounced`（防抖到点）、`Keys`（Down/Enter/Escape，`SearchKeyArgs.Handled`）、`ClearRequested`（清空键与空结果提示）。
  - 清空键槽位常驻：无字时透明度 0、不可点、读屏 Raw（与旧 `setReservedActionVisible` 同义）。
  - 可点件全是 `Tap`（Invoke）。

## 外观

- `SidebarSearchLook`：公共字段加 `DebounceMs`、`GlyphSize`、`IconButtonSize`/`CompactTarget`、`Space6xl`。`Glyph`：`search`、`close`。
- 样式类：输入壳 `surface hover-raised focus-soft`；图标钮与空结果提示 `surface icon-button/hover-plain`。

## 证据与旧测试

- CraftsmanshipPolishTests：`Sidebar search clear button has accessible tooltip and automation properties`、`Sidebar search navigation moves focus and handles escape`
- 新增：防抖合并输入、空结果提示点击/Enter/Esc 清空

## 与旧实现的差异

- 防抖到点只发 `Debounced` 事件；是否 `SearchRequery.shouldQuery`、是否 `BeginMessageSearch` 由组合者定。
