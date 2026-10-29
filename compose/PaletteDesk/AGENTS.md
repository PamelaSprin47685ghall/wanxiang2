# PaletteDesk（组合者）

PalettePanel + CommandPalette + ImeShield + OverlayDesk + Theme = 旧 `Views/CommandPaletteView.fs` 与 `AppShell.ShowCommandPalette`。根规则见项目根目录 AGENTS.md。

- 宿主入口：`PaletteDesk(theme: ThemeManager, overlay: OverlayDesk, prefersTouch: unit -> bool)`；`Show(items: unit -> PaletteItem list, recent: unit -> string list)`（Ctrl/⌘+K；已有对话框不开，有浮层先关）；事件 `Run`（条目, 当时输入）在面板关闭后的下一拍发出，宿主照旧用 `ShellPalette.commandOf` 分派并 `pushRecent`。`Build` 只建内容（测试用）。
- 协作：每次输入重新取条目，经 `CommandPalette.rank` 排序，有查询时末尾追加全文交接行；对话层宽 560、搜索框初始焦点、不整体滚动；构造时 `ImeShield.install()`（幂等），输入法组合中回车 / 方向键归输入法。
- 废除 `PaletteLook`，外观全走 Avalonia 样式类与主题资源；删除内部 `Dispatcher.UIThread.Post`。

## 证据与旧测试
- R20PaletteTests：`376 面板键盘：方向键选择、回车执行所选项、全文交接在末尾`
- R21ImeTests：`386 命令面板：组合中回车不执行、方向键不移动`
- 新增：`执行先关面板再在下一拍运行，再按一次收起`（旧 ShowCommandPalette）。

## 与旧实现的差异
- 旧 `run` 回调改为 `Run` 事件；关面板与投递下一拍由本组合者做。
