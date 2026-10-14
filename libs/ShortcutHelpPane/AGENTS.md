# ShortcutHelpPane

快捷键帮助的内容（迁自旧 `Views/Dialogs.fs` 的 shortcuts）。分组条目由组合者给（旧原文在组合者）。根规则见项目根目录 AGENTS.md。

- `ShortcutHelpPane(look, sections)`：`View`（可滚动，上限跟主题）、`CloseButton`、`InitialFocus`；事件 `Closed`。唯一停留点是关闭键，无内容级 Enter。

## 外观

- `ShortcutHelpPaneLook`：公共字段加 `KeyCapMinWidth`、`ContentMaxHeight`、`SpaceXXs`、`RadiusSm`。键位块是 `grouping-card`，等宽字取 `MonoFont`。

## 证据与旧测试

- DialogsTests：`shortcuts dialog displays sections and closes on button click`
