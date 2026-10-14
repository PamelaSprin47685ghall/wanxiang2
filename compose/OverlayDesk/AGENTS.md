# OverlayDesk（组合者）

OverlayLayer + ToastStrip + PopupMenu + ToastQueue + ToastDwell + PopupPlacement + FocusOrigin = 旧 `Controls/OverlayHost.fs` 与 `Controls/Menu.fs`。根规则见项目根目录 AGENTS.md。

- 协作都在这里：Escape 次序（浮层 → 对话框 → 最新提示条）；提示条去重与上限（ToastQueue）；按语气停留与悬停或动作持焦时暂停（ToastDwell，每条一个 DispatcherTimer）；点动作或 `TryInvokeToastAction` 先关条再执行；菜单激活先关浮层再执行；菜单项挂 FocusOrigin；浮层落点填 PopupPlacement.place。
- `OverlayDeskLook` 由各控件的 Look 加上组合层自己的值组成：菜单最小宽度、三档停留时长、计时步长。

## 证据与旧测试

- OverlayMenuTests：`toast_stays_non_focusable_and_escape_dismisses_it`、`toast_removes_immediately_under_reduced_motion`
- UiStabilityTests：`toast stays inside narrow viewport and is keyboard dismissible live content`、`forty item menu keeps long labels bounded and supports directional focus`、`escape precedence closes topmost overlay first and guards pending dialog`（提示条那一层）
- 新增：菜单激活先关浮层、选择器 Down 打开分组菜单、撤销动作只跑最新的同文条、上限挤掉最旧、带焦点动作关条后焦点回归宿。
