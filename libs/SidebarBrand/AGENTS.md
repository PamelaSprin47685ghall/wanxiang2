# SidebarBrand

侧栏顶部的品牌行，迁自旧 `Views/Sidebar.fs` 的 brandHeader、compactBackButton、newConversationButton、presetMenuButton。新建对话、预设菜单内容、关抽屉都由组合者负责。根规则见项目根目录 AGENTS.md。

- `SidebarBrand(look)` 暴露 `View`、`BackButton`、`NewButton`、`PresetButton`；方法 `SetPresetsVisible`（有预设时才出现）、`SetCompact`（显示返回键，各键抬到触控下限）。
- 事件：`NewClicked`、`PresetClicked`（参数是锚点）、`BackClicked`。所有可点的控件都用 `Tap`，读屏可 Invoke，Enter 和 Space 也能触发。

## 外观

- `SidebarBrandLook` 在公共字段之外加了 `Logo`（宿主给的图形，参数是尺寸）、`LogoSize`、`DisplayTracking`、`BarHeight`、`IconButtonSize`、`CompactTarget`、`TextButtonMinHeight`。`Glyph` 用到 `arrow-left`、`chevron-down`。

## 证据与旧测试

- 旧用例需要起完整侧栏，由 SidebarDesk 承接。本库新增两个用例：三键事件；紧凑态与主题改值。
