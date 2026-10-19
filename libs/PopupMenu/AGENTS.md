# PopupMenu

浮层菜单内容与下拉选择器按钮（迁自旧 `Controls/Menu.fs`）。根规则见项目根目录 AGENTS.md。

- `PopupMenu.build` / `PopupMenu.buildGrouped` 只造内容（ScrollViewer 包 StackPanel），条目为空时返回 None。放进浮层、激活后关浮层再执行条目动作，由组合者 compose/OverlayDesk 接（旧代码在菜单里直接调 `overlay.ClosePopup()`）。
- 菜单项：读屏角色 MenuItem、名字与 tooltip 都是标签；Enter / Space / 左键激活；Up/Down 首尾回绕、Home/PageUp 到首、End/PageDown 到尾（方向键落点规则在本库内联）。
- `SelectButton`：读屏角色 ComboBox；名字跟随显示文本，调用方另起名后不再覆盖；tooltip 跟随文本；点击、Enter/Space、Down/Up 发 `OpenRequested`。
-- 菜单项与下拉按键废除手写 InvokeBorder 与 AutomationPeer，走原生控件及 ControlTheme / 样式类。

## 外观

- `PopupMenuAppearance`：画笔 `Text`、`Muted`、`Faint`、`Danger`、`Accent`；图标插槽 `Check`、`Chevron`；其余尺寸都是活的值。
- 用到的样式类：菜单项 `menu-item`、`surface`、`hover-plain` / `hover-danger`；选择器 `surface`、`select-button`、`hover-bordered`；分组标题 `section-label`；组间分隔 `hairline`。悬停、按压反馈由主题按类名提供。

## 证据与旧测试

- OverlayMenuTests：`select_button_carries_an_automation_name_that_tracks_the_text`、`select_button_and_menu_items_keep_full_opacity_until_pressed`
- UiStabilityTests：`forty item menu keeps long labels bounded and supports directional focus`（菜单本身部分；打开即聚焦首项、落进浮层卡在 compose/OverlayDesk 测）
- 新增：回绕与 PageUp、Enter/Space 激活、分组跳过空组、选择器 Up/Down 打开。

## 与旧实现的差异

- 条目类型改名 `PopupEntry`，字段改为大写开头。
- 菜单宿主是普通 Border 加类名，不再用旧 ActionBorder / Interaction.surface；焦点来源标记由组合者用 FocusOrigin 接。
- 分隔线与分组标题不再调用旧 `Ui.hairline` / `Ui.sectionLabel`，改挂同名外观的 `hairline` / `section-label` 类，由 compose/Theme 给样式。
- 浮层最小宽度（旧代码 200 / 240）交给组合者从主题取。
