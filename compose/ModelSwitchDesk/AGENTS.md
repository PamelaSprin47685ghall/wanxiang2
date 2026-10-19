# ModelSwitchDesk（组合者）

ModelSwitchPanel + ModelSwitcher + ModelPickerList + OverlayDesk + Theme + IconSet = 旧 `Views/ModelSwitcherView.fs`。根规则见项目根目录 AGENTS.md。

- 宿主入口：`ModelSwitchDesk(theme: ThemeManager, overlay: OverlayDesk)`；`Show(anchor, alignRight, options: ModelOption list, title, pick: ModelOption -> unit)`（浮层宽 300）。`Build` 只建内容。
- 协作：搜索走 `ModelSwitcher.filter`，分组走 `ModelPickerList.groups`；任何激活先关浮层，再按 `ModelPickerList.shouldPick` 决定是否调 `pick`（选中当前模型只关不切）。
- 废除 `ModelSwitchLook`，外观全走 Avalonia 样式类与主题资源；打勾图标取 IconSet；浮层宽 300。

## 证据与旧测试
- ModelChipUiTests：`switcher shows provider, default and current, plus the next reply note`、`typing filters rows and enter picks the first hit`、`picking the current model is a no-op`（均经 OverlayDesk 打开，并断言浮层已关）
- A11yRegressionTests：`模型切换器写明键盘路径且空结果会播报`

## 与旧实现的差异
- 无行为差异；宿主不再传 OverlayHost 给视图。
