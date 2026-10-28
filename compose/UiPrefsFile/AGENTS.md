# UiPrefsFile（组合）

本机界面偏好 `ui.json` 的 IO 壳，迁自旧 `src/Wanxiang.UI/Model/UiPrefs.fs`（旧仓库 bfdda85 删除）。解析、渲染与主题判断在 `Wanxiang2.WorkspaceState`（`UiPrefs` 模块），配置目录在 `Wanxiang2.LocalState`，两者经交付包引用。公开面沿用旧名：`limits`、`defaults`、`filePath`、`load`、`save`、`resolveTheme`。命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

取值边界与旧设计层同值：`sidebarWidth/sidebarMinWidth/sidebarMaxWidth = 284/232/420`（旧 `Tokens.sidebarWidth/sidebarMinWidth/sidebarMaxWidth`；wanxiang2 侧同源 `libs/ThemeMetrics` 的 `SidebarWidth/SidebarMinWidth/SidebarMaxWidth`）、`windowMinWidth = 700`（旧 `LayoutPolicy.desktopMinWidth`；同源 `compose/DesignRhythm/LayoutPolicy.fs`）。

消费点：`app/Desktop`（`App.LoadPrefs/SavePrefs` 静态端口、`ShellPorts.PrefsLoad/PrefsSave`、`SettingsHooks.SavePrefs`、侧栏宽度初值）；`app/Pwa` 只用 `limits` 与 `defaults`（浏览器偏好落点是 IndexedDB 的 `wx-prefs`，不经本壳）。

# LEMMA
1. **ui.json 的字节不变，未知键原样保留。** [根 AXIOM: 1; WorkspaceState LEMMA: 2]
2. **落盘原子、权限 0600。** [根 AXIOM: 5]
    - 推理：写 `ui.json.tmp` 再 `File.Move(..., overwrite = true)` 替换，随后把权限收紧到 0600（Q118，与 TOML、日志、附件一致）。旧 `LocalText.write` 是直接写；读回的文本不变。
3. **路径先过 Git 边界。** [LocalState LEMMA: 1, 2]
    - 推理：`filePath` 走 `LocalState.configHome()`（`WANXIANG_HOME` 优先，否则 `~/.config/wanxiang`；在 Git 元数据内直接拒绝）。`load` / `save` 把它包在 `try` 里：路径被拒时退化为默认值 / 不写。

# SPEC
1. [LEMMA: 1] 解析与渲染：`libs/WorkspaceState/tests/UiPrefs.Tests.fs` 的三个用例 `ThemePreference conversions and resolveTheme`、`persisting drafts is on by default and an old ui file without the key keeps it on`、`theme cycle and notice match old CycleTheme` 通过；`./x test WorkspaceState` 可重跑。
2. [LEMMA: 2] 文件名与写入步骤：源码对照 `compose/UiPrefsFile/UiPrefsFile.fs`（`filePath` = `LocalState.configHome ()` + `"ui.json"`；`save` = 临时文件 + `File.Move` + `SetUnixFileMode 0600`）。
3. 本组合者无独立测试工程：`./x compose` 跳过它（`x` 的 `compose()` 注释点名）；临时文件 + 原子替换 + 0600 没有自动化用例。旧仓库覆盖旧 `LocalText`（本壳旧实现的写入路径）文件往返的用例是 `R28PwaParityTests: local text roundtrips through a file on desktop`，随旧壳退役。

# 迁入或覆盖的旧测试
- 旧 `UiPureLogicTests`：`ThemePreference conversions and resolveTheme` → `libs/WorkspaceState`（`UiPrefs.isDark`，`ThemeMode` 属旧设计层，调用方自己映射；本壳的 `resolveTheme` 返回 `bool`）。
- 旧 `ComposerReliabilityTests`：`persisting drafts is on by default and an old ui file without the key keeps it on` → `libs/WorkspaceState`（`UiPrefs`）。
- 旧 `R28PwaParityTests` 的文件往返用例测的是旧 `LocalText`，没有迁：新壳直接 `File` 读写 + 原子替换（见 SPEC 2）。

# 拆分与复核
- 旧 `Model/UiPrefs.fs` 按「文本 ⇄ 偏好 / 文件落点」拆：前者归 `libs/WorkspaceState`（`UiPrefs`，旧 load/save 的行为用例），路径、落盘与权限留本壳；`resolveTheme` 的 `ThemeMode` 改成库的 `isDark`（返回 `bool`），主题模式映射归调用方。
- 取值边界原取旧 `Tokens` / `LayoutPolicy`，现按同值字面量固定（见本页首段），本壳不再依赖旧设计层。逐字对照：`git -C /tmp/wx-old show bfdda85^:src/Wanxiang.UI/Model/UiPrefs.fs | diff - compose/UiPrefsFile/UiPrefsFile.fs`（旧仓库退役检出在 `/tmp/wx-old`）。
