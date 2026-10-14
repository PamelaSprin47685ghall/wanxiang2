# SettingsFrame

设置页外框（迁自旧 `Views/SettingsView.fs` 的界面部分）：头部「设置」与「返回对话」、分组的分区导航、范围说明、内容区与窄屏布局。根规则见项目根目录 AGENTS.md。

- `SettingsFrame(look, groups)`：本身是 Border。`groups` 是 `(分组名, SettingsNavItem list)`，项为 `{ Key; Label; Glyph }`。
- `Select key`：标出选中分区（选中底、左缘强调条、标题加粗、图标不透明度、读屏 ItemStatus「当前分区」与帮助文字「<名>，当前分区」）并把键滚进视野。
- `SetCaptions(范围, 说明)`、`SetContent(control, restoreY)`、`ContentOffset`、`ScrollTo`、`FocusNav key`、`Current`、`IsCompact`。
- 事件：`SelectRequested key`（点击，或在导航键上按上 / 左 / 下 / 右循环移动、Home / End 跳首尾；键盘移动后焦点跟到目标键）、`CloseClicked`。组合者收到后切内容并回调 `Select`。
- 读屏：导航轴是 Tab「设置分区导航」，每个键是 TabItem。
- 窄于 `CompactBreakpoint`：导航转为顶部横条（分组名隐藏、横向滚动），内容内边距收紧；切换时按新边界钳制滚动位置再落位。断点是活值，改了立即重排。
- 分区内容、缓存与滚动记忆（SectionMemory）、范围文字、设备分区的刷新都由组合者接。

## 外观

- `SettingsFrameLook`：公共字段加 `Space6xl`、`Space8xl`、画笔 `Canvas` / `Rail` / `Chrome` / `Selected`、`BarHeight`、`NavWidth`、`NavMinHeight`、`SelectedEdgeWidth`、`ContentMaxWidth`、`SubtleOpacity`、`CompactBreakpoint`。`Glyph` 用到各分区的图标名（组合者给：`server`、`wrench`、`sliders`、`devices`、`sun`、`info`）。
- 样式类：导航键 `surface menu-item hover-plain`（未选中时清掉本地底，悬停样式才生效）、「返回对话」`surface button-ghost hover-plain`、分组名 `section-label`。

## 证据与旧测试

- SettingsViewTests：`SettingsView navigates sections, remembers scroll positions and invalidates cache`（导航与滚动落位部分；缓存与记忆在组合者）
- SettingsSurfaceTests：`settings window title dominates section headings`
- 新增：`settings nav arrows wrap and home end jump while keeping focus on the nav`、`narrow window turns the nav into a top strip and hides group labels`、`scope captions follow the composer`

## 与旧实现的差异

- 导航键的悬停底由主题样式给（旧实现在指针进出与焦点变化时手动重算底色）；选中底仍是本地值。看到的效果相同。
