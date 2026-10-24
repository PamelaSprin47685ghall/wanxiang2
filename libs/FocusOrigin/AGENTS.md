# FocusOrigin

合并包：FocusOrigin、OverlayLayer。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `FocusOrigin`

## FocusOrigin

聚焦来源报告（迁自旧 `Controls/Interaction.fs` 的 `reportFocusOrigin`）：键盘或程序化聚焦给控件挂 `focus-by-program` 类，指针聚焦与失焦去掉。根规则见项目根目录 AGENTS.md。

### 外观

无 Look 记录。用到的样式类：`focus-by-program`（只挂类名；焦点环的样式由主题安装）。

### 证据与旧测试

旧 FocusRingTests 检查的是焦点环的画法（主题样式），留旧仓库与主题一起迁。本库在 headless 下用真实焦点切换验证类名。

## 成员 `OverlayLayer`

## OverlayLayer

覆盖层：对话框（嵌套栈、关闭守卫）、浮层、遮罩、焦点记忆与恢复、Tab 圈定、层外点击与层外滚轮关闭、视口钳位（迁自旧 `Controls/OverlayHost.fs` 除提示条以外的部分）。根规则见项目根目录 AGENTS.md。

- 提示条在 ToastStrip；Esc 的顺序（浮层 → 对话框 → 最新提示条）由组合者接：`OverlayLayer.HandleEscape` 返回 false 时再交给提示条。
- 浮层落点是插槽 `PopupPlace`，组合者填 PopupPlacement 库的 `place`。
- 遮罩淡出时长来自 Look；减弱动效时主题给 0，等同旧代码的 `MotionPolicy.isReduced` 分支。
- 浮层关闭只在层内归还焦点（`restorePopupFocus`），不向上报事件：层没有「关闭回调」这类出口，组合者不靠它拿用户意图。
- 层的次序与旧代码相同：遮罩、对话框卡、浮层捕获层、浮层卡（提示条层由组合者随后加入）。

### 原生菜单也算「浮层开着」（2026-10-24）

- `IsPopupOpen` = 本层浮层卡可见 **或** 有原生菜单/Flyout 处于 `IsOpen`。原生判定只认对象自己的账本（`MenuBase.IsOpen` / `FlyoutBase.IsOpen`），不从宿主（PopupRoot / OverlayPopupHost）是否残留推断。
- 判定范围（`openNativeMenus`，私有）：当前焦点祖先链所在宿主里的 `MenuBase`，加根与逻辑子孙上挂着的 `Control.ContextMenu` / `Control.ContextFlyout` / `FlyoutBase.GetAttachedFlyout`。生产入口对照：`compose/MessageCardView/Card.fs` 用 `ContextMenu = this.Menu(...)`（消息卡右键菜单），`libs/PopupMenu/PopupMenu.fs` 的原生下拉用 `MenuFlyout`；本层卡片菜单（`OverlayDesk.ShowMenu`）走 `popupCard`，不依赖这段判定。
- `HandleEscape` 的退栈顺序改为：本层浮层卡 → 最上层原生菜单 → 对话框（受 `dialogGuard`）。原生关闭优先调用具体对象自带的契约：`ContextMenu.Close()` → 其它 `MenuBase.Close()` → `FlyoutBase.Hide()`，不反射、不直接 Hide 宿主（会把原生 `IsOpen` 账本改歪）。
- 实测边界（Avalonia 12.1.2 + headless，编程式 `menu.Open(anchor)` 打开的 `ContextMenu`）：`Escape` 走 `ContextMenu.Close()` 后 `menu.IsOpen` 仍为 `true`（具体类与基类两种调法都一样；`MenuBase.IsOpen` 无公开 setter）——本层只保证「按键被原生处理器认领、不外溢到设置/抽屉/生成」，不宣称已关闭。`FlyoutBase.Hide()` 会翻转 `IsOpen`，账本同步。真实指针/键盘触发的 `ContextRequested` 路径不在本层的测试可达范围内（rig 只提供 `Root: Grid`，无 Window）。
- 证据：`compose/AppShell/tests/ShortcutInterceptTests.fs` 三条原生/自研浮层拦截用例；`libs/FocusOrigin/tests` 8 条（含边界）全绿。修复前用修复前的交付包跑：AppShell 50 条里 2 条失败（两条原生用例报「独立宿主应被识别为浮层」），修复后 50/50 通过；日志 `artifacts/logs/appshell-BEFORE-fix-2026-10-24.log` / `artifacts/logs/appshell-AFTER-fix-2026-10-24.log`。

### 外观

- `OverlayLayerAppearance`（W7 外观退役后的普通记录）：`Scrim`、`Surface`、`Edge`（画笔）；`DialogShadow`、`PopupShadow`（阴影）；`PopupGap`、`PopupMinWidth`（浮层几何）；`ScrimFade`（遮罩淡入淡出时长，减弱动效时组合者给 0）；`ScrimEasing`。
- 主题管得住的均匀几何走控件侧资源键声明式绑定：描边 `Thickness.BorderThin`、对话框圆角 `CornerRadius.RadiusLg`、浮层圆角 `CornerRadius.RadiusMd`、对话框内边距 `Padding.OverlayDialog`、浮层内边距 `Padding.OverlayPopup`、视口留白 `Margin.OverlayInset`。本库不再有 `IObservable` 字段与手工 `Subscribe`。
- 视口留白（钳位基准）就是对话框卡的均匀外边距：主题改 `Margin.OverlayInset` 即改留白，层随即重新钳位（`InsetChanged` 事件通知组合者重排随留白变化的部分）。
- 用到的样式类：无。

### 证据与旧测试

- UiStabilityTests：`dialog is constrained to the live viewport and restores focus`、`popup flips and clamps inside the viewport then restores anchor focus`、`escape precedence closes topmost overlay first and guards pending dialog`（提示条那一层在组合者里测）
- DismissFocusTests：`wheel outside an open popup dismisses it without scrolling the content`、`wheel inside an open popup is left to the popup itself`
- 新增：Tab 圈定与嵌套对话框、视口留白实时生效。
- 测试里的落点函数照 PopupPlacement 的规则写了一份（库不能引用兄弟库）；真实组合在 compose/OverlayDesk 里测。

### 与旧实现的差异

- 阴影改为 Look 里的活值绑定，取代旧代码订阅 `Tokens.Changed` 重新赋值。
- 浮层默认最小宽度来自 Look（旧代码写死 180）。
- `OverlayLayer` 内部保留的焦点与定位 Post（`Dispatcher.UIThread.Post`，见 `restoreDialogFocus` / `restorePopupFocus` / `focusFirst` / `ShowDialog` / `ShowPopup`）：浮层/对话框挂载后等待首帧排版及关闭后归还焦点（布局时序排队，非焦点竞态）。
