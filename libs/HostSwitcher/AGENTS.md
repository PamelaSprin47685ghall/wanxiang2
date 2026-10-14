# HostSwitcher

侧栏页脚的主机状态行与设置/命令面板入口（迁自旧 `Views/Sidebar.fs` 的 statusRow、serverCaption、statusDot、settingsButton、paletteButton、SetServerUrl、SetConnection）。重连与打开设置/面板的动作归组合者。根规则见项目根目录 AGENTS.md。

- `HostSwitcher(look)`：`View`；`SetServerUrl`（显示 Authority，非法则「选择主机」）、`SetConnection`、`ConfigurePalette`、`SetCompact`。
  - 事件：`ReconnectClicked`、`SettingsClicked`、`PaletteClicked`。
  - 可点件全是 `Tap`（Invoke）。

## 外观

- `HostSwitcherLook`：公共字段加 `DotSize`、`BarHeight`、`DividerWidth`、`IconButtonSize`/`CompactTarget`、`TextButtonMinHeight`、`Hairline`、`RadiusSm`。`Glyph`：`gear`、`search`。
- 样式类：状态行 `surface hover-plain`；图标钮 `icon-button`。

## 证据与旧测试

- 覆盖旧侧栏连接行呈现与点击重连的行为（组合者接 `actions.reconnect`）。
- 新增：Authority 解析、设置/面板事件、紧凑命中面、页脚高跟主题

## 与旧实现的差异

- 导入按钮已移出页脚（决策 363）；本库不含导入入口。
- 品牌行与「新建对话」在组合者，不在本库。
