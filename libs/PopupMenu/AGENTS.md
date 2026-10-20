# PopupMenu

浮层菜单内容与下拉选择器按钮（迁自旧 `Controls/Menu.fs`）。根规则见项目根目录 AGENTS.md。

- `PopupMenu.build` / `PopupMenu.buildGrouped` 只造内容（ScrollViewer 包 StackPanel），条目为空时返回 None。放进浮层、激活后关浮层再执行条目动作，由组合者 compose/OverlayDesk 接（旧代码在菜单里直接调 `overlay.ClosePopup()`）。
- 菜单项：读屏角色 MenuItem、名字与 tooltip 都是标签；Enter / Space / 左键激活；Up/Down 首尾回绕、Home/PageUp 到首、End/PageDown 到尾（方向键落点规则在本库内联）。
- `SelectButton`：原生 Button + MenuFlyout 构成；名字跟随显示文本，调用方另起名后不再覆盖；tooltip 跟随文本；点击、Enter/Space、Down/Up 发 `OpenRequested`，焦点与按键行为由原生控件底座承担。换显示文本走幂等单槽 `ApplyText(text: string)`。
- 菜单项与下拉按键废除手写 InvokeBorder 与 AutomationPeer，走原生控件及 ControlTheme / 样式类。

## 外观

- `PopupMenuAppearance`（W7 外观退役后的普通记录）：画笔 `Text`、`Muted`、`Faint`、`Danger`、`Accent`；图标插槽 `Check`、`Chevron`；组合者自定的静态量 `IconSlotWidth`（图标槽宽）、`ItemMinHeight`（菜单项最小高）、`MaxHeight`（菜单滚动上限）。
- 主题管得住的排版走控件侧资源键声明式绑定：字号 `Metric.FontBody` / `Metric.FontCaption`、图标高 `Metric.IconGlyph`、右侧槽宽 `Metric.MenuRightSlotMinWidth`、列间距 `Space.Sm`、项间距 `Space.XXs`、项内边距 `Padding.MenuItem`、项圆角 `CornerRadius.RadiusXs`、提示外边距 `Margin.MenuHint`、分隔外边距 `Margin.MenuDivider`、组标题外边距 `Margin.MenuHeader`、下拉标题最大宽 `Metric.SelectCaptionMaxWidth`。本库不再有 `IObservable` 字段与手工 `Subscribe`。
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
