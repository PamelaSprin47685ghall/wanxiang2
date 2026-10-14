# PalettePanel

命令面板内容：搜索框、结果列表（最近使用 / 操作分组）、polite 状态行、键位提示（迁自旧 `Views/CommandPaletteView.fs`）。根规则见项目根目录 AGENTS.md。

- 插槽 `PaletteSlots`：`Query`（组合者用 CommandPalette 的 rank 与 fullText 行得出 `PaletteResult`）、`CompactKeys`（CommandPalette.compactKeys）、`PrefersTouch`（InputSense）。放进对话层、执行前关面板、IME 组合中不执行都由组合者接（ImeShield 装在顶层）。
- 键盘：输入框里 Up/Down 循环选择（焦点留在输入框，状态行播报「标题，种类，第 n 项，共 m 项」），Enter 执行所选；Tab 进列表后 Up/Down 在行间走，首行再上回输入框。行本身 Enter / Space / 左键执行。
- 读屏：行角色 ListItem，名字是标题，HelpText 是「种类，副标题，快捷键 …」；触屏不显示键位列。

## 外观

- `PalettePanelLook`：画笔 `Text`、`Muted`、`Faint`、`Selected`、`FieldSurface`、`FieldEdge`、`Caret`；字号、等宽字体、行与输入框的尺寸边距都是活的值。
- 用到的样式类（compose/Theme）：行 `palette-row menu-item surface hover-plain`（选中时另挂 `selected` 并设 `Selected` 底）；分组标题 `section-label`；分隔 `hairline`；输入壳 `surface focus-soft`。

## 证据与旧测试

- R20PaletteTests：`376 面板键盘：方向键选择、回车执行所选项、全文交接在末尾`
- R21ImeTests：`386 命令面板：组合中回车不执行、方向键不移动` 依赖 ImeShield，留给组合者测。
- 新增：播报文字与循环、读屏 HelpText 与键位缩写、最近使用分组、列表内方向键。

## 与旧实现的差异

- 排序、全文交接行、最近使用判断都由组合者给出（`PaletteResult`），本库不认识 CommandPalette 的类型。
- 未选中行清掉本地底色（旧代码写 Transparent），让主题的悬停样式能生效。
