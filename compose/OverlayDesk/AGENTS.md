# OverlayDesk（组合者）

OverlayLayer + ToastStrip + PopupMenu + FocusOrigin + 内部模块（ToastQueue、ToastDwell）= 旧 `Controls/OverlayHost.fs` 与 `Controls/Menu.fs`。根规则见项目根目录 AGENTS.md。

- 协作都在这里：Escape 次序（浮层 → 对话框 → 最新提示条）；提示条去重与上限（ToastQueue）；单入口幂等 `Render(OverlayVS)` 与 `RenderToasts(ToastVS list)`；点动作或 `TryInvokeToastAction` 先关条再执行；菜单激活先关浮层再执行；菜单项挂 FocusOrigin；浮层落点交由原生约束或内置 nativePlace。
- 废除手写 DispatcherTimer 计时器与轮询，提示条生命周期受 WorkspaceState 的 MsgTick/ExpiresAt 统一调度。
- 提示条动作按钮的焦点归宿由组合者填：`WireFocusHome(home)`（转发给 ToastStrip.WireFocusHome）。

## 外观值
- `OverlayDeskMetrics`（W7 外观退役后的普通记录）：`Layer`（OverlayLayerAppearance）、`Toasts`（ToastStripAppearance）、`Menu`（PopupMenuAppearance）三个外观记录，加上组合层自用的静态量 `MenuMinWidth`（旧代码 200）、`GroupedMenuMinWidth`（旧代码 240）、`DwellFailureMs` / `DwellWarningMs` / `DwellDefaultMs`（停留时长，入口壳从主题的 MotionSwitch 时长解析）、`DismissTick`。
- 提示条带的留白跟覆盖层走：层按主题资源 `Margin.OverlayInset` 钳位，留白变了经 `Layer.InsetChanged` 重排。

## 接口
- `OverlayDesk(root, look)`：`Layer`、`Toasts`、`ShowDialog` / `CloseDialog` / `ClosePopup` / `TrapTab`、`WireDismiss`、`Render(OverlayVS)` / `RenderToasts(ToastVS list)`、`Toast` / `ToastWith`、`TryInvokeToastAction` / `HasToastAction` / `FocusToastAction` / `ToastActionButtons`、`WireFocusHome`、`HandleEscape`、`ShowMenu` / `ShowGroupedMenu` / `SelectButton`。
- 命名规约：单槽写接口用 `Apply*`（本组合者只有 `WireFocusHome` 这一处回调槽）；本组合者**不持有**意图派发器，浮层关闭不向上报 `UserMsg`。

## 内部模块
- `ToastQueue`：提示条队列容量与同文去重（迁自原 PopupPlacement 库）。
- `ToastDwell`：单条提示停留计时与焦点/悬停挂起逻辑（迁自原 PopupPlacement 库）。

## 证据与旧测试

- OverlayMenuTests：`toast_stays_non_focusable_and_escape_dismisses_it`、`toast_removes_immediately_under_reduced_motion`
- UiStabilityTests：`toast stays inside narrow viewport and is keyboard dismissible live content`、`forty item menu keeps long labels bounded and supports directional focus`、`escape closes popup then dialog then newest toast`（提示条那一层，迁自旧 `escape precedence closes topmost overlay first and guards pending dialog`）
- 新增：菜单激活先关浮层、选择器 Down 打开分组菜单、撤销动作只跑最新的同文条、上限挤掉最旧、带焦点动作关条后焦点回归宿。
