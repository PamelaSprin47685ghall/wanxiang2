# ShellCommands（组合者）

壳层命令，取代旧 extract-ledger `src/Wanxiang.UI/AppShell.fs` 的 `HandleShortcut`、`RunPaletteItem`、`ShowCommandPalette`、`CycleTheme`。协作者：ShortcutKeys（ShortcutAction）、EscapeChain、CommandPalette（ShellPalette、pushRecent）、UiPrefs（ThemePreference）。不依赖 Avalonia。根规则见项目根目录 AGENTS.md。

- 宿主入口：`ShellCommands(surface: ShellSurface, initialRecent)`。
  - `HandleShortcut(action) : bool`：宿主把 KeyEventArgs 映射成 ShortcutKey、`ShortcutRouter.resolveKey` 后调用，返回 true 就设 `e.Handled`。
  - `RunPaletteItem(item, query)`、`ShowPalette()`、`CycleTheme()`、`Recent`。
- 本组合者拥有命令面板的最近使用清单（落盘经 `surface.SaveRecent`）。
- `ShellSurface` 分两类：事实（OverlayEscape、IsDialogOpen、IsPopupOpen、IsSettingsOpen、IsDrawerOpen、IsGenerating、HasConversation、VisibleOrder、HasPreset、PaletteInput、Theme）与界面面（CloseSettings、CloseDrawer、StopGeneration、ToggleSidebar、CloseWindow、NewConversation、FocusSearch、OpenSettings、OpenModelPicker、JumpExchange、ScrollToBeginning/End、FocusComposer、CopyLastReply、RegenerateLast、TryUndo、FocusToast、ExportConversation、OpenConversation、ShowShortcuts、ApplyPreset、SavePreset、Fork、Import、AddProvider、ShowPalette、SaveRecent、SaveTheme、Toast）。
- 本组合者做：Escape 链（浮层 → 设置页 → 抽屉 → 停止生成）、对话框/弹出菜单打开时其余快捷键不接管、Ctrl+1..9、面板分派与收抽屉（失效预设只提示不收）、主题三态轮转与提示、没有对话时导出只提示。
- 留在宿主：焦点在有字的输入框时不接管撤销（TryUndo 内判断）、关窗走 Window.Close（PWA 返回 false）、模型选择器锚点、设置分区查找、分叉种子与无对话提示、偏好落盘与换色。

## 与旧实现的差异
- `PaletteCommand.OpenSettings key` 统一交 `OpenSettings(Some key)`，宿主找不到分区时只打开设置页（与旧代码同结果）。
- 撤销是否接管、关窗是否接管由宿主返回值决定，判断本身没变。

## 证据与旧测试（`tests/Tests.fs`）
- AppShellTests：`HandleShortcut ToggleTheme cycles through FollowSystem, AlwaysLight and AlwaysDark`、`HandleShortcut ToggleSidebar triggers sidebar collapse toggle`、`HandleShortcut OpenSettings shows settingsHost and hides workspace`、`HandleShortcut FocusSearch ensures sidebar visible and focuses search`、`HandleShortcut StopGeneration when run is generating invokes generation cancellation`、`HandleShortcut JumpExchange and Scroll dispatches smoothly without errors`（意图部分；界面效果在宿主与各 desk）
- ContinueLinkUndoTests：`232 Ctrl+Z 映射到撤销，并只触发可见提示条上的「撤销」`（接管判断部分）
- 新增：`Escape 依次落到浮层、设置页、抽屉`、`对话框开着时其余快捷键不接管`、`Ctrl+1..9 按侧栏可见顺序打开，超出就不接管`、`没有打开的对话时导出只提示`、`面板选中：记最近使用，收抽屉，失效预设只提示不收抽屉`、`Ctrl+K 用现取的条目和最近使用打开面板`
