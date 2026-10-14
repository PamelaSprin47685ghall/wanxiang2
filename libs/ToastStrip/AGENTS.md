# ToastStrip

提示条带：渲染提示条（语气强调条、正文、可选行内动作按钮）、同步移除、F6 聚焦最新动作、带焦点条移除时焦点回归宿（迁自旧 `Controls/OverlayHost.fs` 的 `ToastWith` 渲染部分）。根规则见项目根目录 AGENTS.md。

- 只管画与移除。去重与上限（ToastQueue）、停留计时（ToastDwell）、Escape 关最新一条、Ctrl/⌘+Z 触发动作都由组合者 compose/OverlayDesk 接。
- 事件：`Clicked`（点条身）、`ActionInvoked`（点动作按钮，组合者先关条再执行）、`PointerEnteredToast` / `PointerExitedToast`（带动作按钮是否持焦）、`ActionFocused` / `ActionUnfocused`。
- 条身 `Focusable=false`；Failure / Warning 为 Assertive，其余 Polite；动作「撤销」带 `Ctrl+Z` 快捷键与帮助文字。

## 外观

- `ToastStripLook`：`Surface`、`Edge`、`Text`（画笔）、`Accent`（语气 → 画笔）；其余尺寸、阴影、淡入时长都是活的值；`FadeEasing`。
- 用到的样式类（compose/Theme 安装）：动作按钮 `surface`、`button-secondary`、`hover-plain`。

## 证据与旧测试

- OverlayMenuTests：`toast_stays_non_focusable_and_escape_dismisses_it`（非焦点与同步移除部分）
- UiStabilityTests：`toast stays inside narrow viewport and is keyboard dismissible live content`（宽度钳位与 live 部分）
- Escape、减弱动效即时移除（`toast_removes_immediately_under_reduced_motion`）在 compose/OverlayDesk 测。
- 新增：撤销按钮的读屏属性与焦点归宿、Look 实时生效。

## 与旧实现的差异

- `ToastTone` 加了 RequireQualifiedAccess。
- 动作按钮与旧 `Ui.button Secondary` 一样是可聚焦的 Border（读屏角色 Button，Enter/Space/左键触发），外观全由主题类给。
- 点条身由 PointerReleased 发 `Clicked`，旧代码用 `Ui.onClick`。

## 读屏激活

可激活的宿主是 `InvokeBorder`（Border 子类，样式仍按 Border 查）：自动化节点提供 Invoke 模式，角色取 ControlTypeOverride（菜单项 MenuItem、选择键 ComboBox、提示条动作 Button），读屏激活与 Enter / 点击走同一个动作，与旧 ActionBorder 一致。新增测试：Invoke 模式存在且激活生效。
