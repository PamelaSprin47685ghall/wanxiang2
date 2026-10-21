# EntryShells（组合）

桌面 / PWA 共用入口壳，迁自旧 `src/Wanxiang.UI/{App.fs, Views/MainWindow.fs, Views/Composer.fs, Design/SharedTheme.fs, Design/Icons.fs}`（旧仓库 bfdda85 删除）。命名空间与程序集名沿用 `Wanxiang.UI`，包 id 是 `Wanxiang2.EntryShells`；`app/Desktop` 与 `app/Pwa` 只经交付包消费它。根规则见项目根目录 AGENTS.md。

公开面：
- `Effects`（`EffectSink` / `Effect` / `FocusTarget` / `ScrollCommand` / `ScrollAnchorMath`）：全应用唯一的副作用执行器宿主接收面与 prepend 锚定偏移数学。
- `SharedTheme`：全应用唯一的 `ThemeManager` / `ThemeStyles` / `InputSense` 与品牌标 `logo`（`Wanxiang2.Theme`、`Wanxiang2.ShellDesk`）。必须最先编译。
- `App`（Avalonia `Application`）：把 Fluent 的强调色 / 选中色 / 输入框描边压成万象的活画笔，并给弹出层（ToolTip / ContextMenu / MenuFlyoutPresenter）套万象字体与底色；静态注入端口 `Root` / `Build` / `FlushDrafts` / `LoadPrefs` / `SavePrefs` / `MarkFirstFrame`。桌面分支组装 `MainWindow`，PWA 分支直接挂 `SingleView`。
- `MainWindow(MainWindowPort)`：窗口尺寸 / 位置与几何持久化（Q195）；`Root` / `Build` / `FlushDrafts` / `LoadPrefs` / `SavePrefs` / `MarkFirstFrame` 全部由入口注入。
- 输入区适配器（缩略图、上下文摘要）；控件本体与载荷在 `Wanxiang2.ComposerDesk`，看图徽标经 `Wanxiang2.ComposerDesk` / `Wanxiang2.MediaTypes`（看图判断 `ModelVision` 已并入 `Wanxiang2.HostCatalog`，本壳不直接引用）。
- **发送历史召回（`PromptHistory`）不在本壳**：2026-10-21 起由输入区的真正所有者 `compose/ComposerDesk` 接线（`Up`/`Down`/`Escape` 挂 `ComposerBox` 的 `WireKeyHook`），本壳既不引用该包也不持有该对象。
- `Icons`：宿主语义名 → `Wanxiang2.IconSet`（Lucide 官方集）的映射，三档尺寸 14 / 16 / 20。

引用的包：组合者 `Wanxiang2.Theme`、`Wanxiang2.ShellDesk`、`Wanxiang2.ComposerDesk`、`Wanxiang2.DesignRhythm`、`Wanxiang2.Outbox`；库 `Wanxiang2.ThemePalette`、`Wanxiang2.ThemeMetrics`、`Wanxiang2.InputSense`、`Wanxiang2.ImeShield`、`Wanxiang2.IconSet`、`Wanxiang2.UiPrefs`、`Wanxiang2.ComposerDrafts`、`Wanxiang2.MediaTypes`、`Wanxiang2.Wire`；外加 `Avalonia` / `Avalonia.Themes.Fluent`（见 `EntryShells.fsproj`）。

本壳不持 DispatcherTimer、不做文件 IO、不执行进程退出：计时器（`After` / `Every`）、偏好与草稿落盘、剪贴板、链接打开、`StartupTrace.requestExit` 都由入口注入（`app/Desktop/Program.fs`、`app/Pwa/Program.fs` 的接线）。

# LEMMA
1. **公开面与旧壳同名，旧调用点不用改。** [根 AXIOM: 1]
    - 推理：`Wanxiang.UI.App` / `MainWindow` / `Composer` / `Icons` / `SharedTheme` 与旧宿主同名；app 双入口与旧 `src/Wanxiang.App`、`src/Wanxiang.Pwa` 的接线逐段对应。
2. **副作用归入口，壳只做装配。** [根 AXIOM: 3]
    - 推理：计时器、文件、进程退出是宿主的事；壳把需要它们的地方留成端口，桌面与 PWA 各自注入（决策 48：两者共用同一套视图）。
3. **壳不重做上游已解决的事。** [根 AXIOM: 3]
    - 推理：图标几何与缓存归 `IconSet`、控件与载荷归 `ComposerDesk`、主题活画笔归 `Theme`、品牌标归 `ShellDesk`；本壳只留旧签名与语义映射。

# SPEC
1. [LEMMA: 1, 3] 五个文件与旧实现逐段对照：逻辑差异只有设计层转调（`Tokens` → 活画笔 / `IconSet` / `ComposerDesk`）与注入端口，其余是注释（对照命令见「证据」）。
2. [LEMMA: 2] 两个入口只经包消费本壳：`app/Desktop/Wanxiang.App.fsproj` 与 `app/Pwa/Wanxiang.Pwa.fsproj` 各有一条 `Wanxiang2.EntryShells` 的 `PackageReference`；`./run-desktop.sh` / `./run-pwa.sh` 产包、构建并启动（真实界面验收要人看，仓库内未自动化）。
3. 本组合者有独立测试工程 `compose/EntryShells/tests/EntryShells.Tests.fsproj`（`./x compose` 自动发现并运行）：覆盖弹出层字体契约（`PopupFontTests`：应用样式命中 / 窗口内实际生效 / 复合回落覆盖界面符号）与内嵌正文字体的基本可用性（`EmbeddedFontTests`）。
4. [LEMMA: 1] 源级回归：`compose/Theme/tests/A11yRegression.fs`（迁自旧 `A11yRegressionTests`）的 `模型选择不再保留隐藏顶栏入口` 读 `compose/EntryShells/Composer.fs`，验证旧入口符号（`HeaderModelAnchor` / `SetHeaderModel`）不存在、新入口（`ModelAnchor()`）在；`./x compose` 运行。

# 迁入或覆盖的旧测试
- 旧入口壳没有独立单元测试；驱动这些文件的用例挂在旧整套 UI 测试上（如 `IconCenteringTests` 读 `src/Wanxiang.UI/Design/Icons.fs` 的墨迹断言、`FocusRingTests` 经 `Icons.*`、`CloseWindowTests` 走 `MainWindow.Closing` 的几何持久化），随旧壳退役（旧仓库 16904f0 删 `src/` 与 `tests/`）。
- 行为面由各自的库 / 组合者测试覆盖：`libs/IconSet`（尺寸活值跟随、图像复用、画笔实例）、`compose/ComposerDesk`（headless，含发送历史召回的回归用例）、`libs/HostCatalog`（含看图判断 ModelVision 的用例）、`libs/MediaTypes`、`libs/ImeShield`、`libs/InputSense`、`compose/Theme`（控件类样式与无障碍）。
- 旧 `IconCenteringTests` 要整套旧视图，留在旧仓库（`libs/IconSet/AGENTS.md` 同结论）。

# 拆分与复核
- 入口壳只留装配与宿主语义映射：控件与载荷归 `ComposerDesk`、图标几何与缓存归 `IconSet`、主题活画笔与样式类归 `Theme`、品牌标归 `ShellDesk`；`Icons` 只保留 `IBrush -> Control` 的语义函数签名。
- 副作用（计时器、偏好 / 草稿落盘、剪贴板、链接打开、采样退出）全部改成注入端口，由 `app/Desktop`、`app/Pwa` 各自接线；对照命令见「证据」。

# 证据
- 与旧文件对照（旧仓库已物理退役；本机退役检出在 `/tmp/wx-old`，其 HEAD 16904f0 已删 `src/` 与 `tests/`，旧文件用 `bfdda85^` 的历史取）：
  - `git -C /tmp/wx-old show bfdda85^:src/Wanxiang.UI/App.fs | diff - compose/EntryShells/App.fs`
  - 同法对照 `Views/MainWindow.fs`、`Views/Composer.fs`、`Design/SharedTheme.fs`、`Design/Icons.fs`。
- 编译与打包：`./x pack`，包在 `artifacts/feed/Wanxiang2.EntryShells.0.0.0-local.nupkg`。
- 入口消费：`grep -n Wanxiang2.EntryShells app/Desktop/Wanxiang.App.fsproj app/Pwa/Wanxiang.Pwa.fsproj`。
- 运行：`./run-desktop.sh`、`./run-pwa.sh`（真实界面验收）。
