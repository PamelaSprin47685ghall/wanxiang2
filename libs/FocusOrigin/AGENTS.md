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
- 层的次序与旧代码相同：遮罩、对话框卡、浮层捕获层、浮层卡（提示条层由组合者随后加入）。

### 外观

- `OverlayLayerAppearance`：`Scrim`、`Surface`、`Edge`（画笔）；`EdgeWidth`、`DialogRadius`、`PopupRadius`、`DialogPadding`、`PopupPadding`、`DialogShadow`、`PopupShadow`、`ViewportInset`、`PopupGap`、`PopupMinWidth`、`ScrimFade`（活的值）；`ScrimEasing`。
- 用到的样式类：无。

### 证据与旧测试

- UiStabilityTests：`dialog is constrained to the live viewport and restores focus`、`popup flips and clamps inside the viewport then restores anchor focus`、`escape precedence closes topmost overlay first and guards pending dialog`（提示条那一层在组合者里测）
- DismissFocusTests：`wheel outside an open popup dismisses it without scrolling the content`、`wheel inside an open popup is left to the popup itself`
- 新增：Tab 圈定与嵌套对话框、视口留白实时生效。
- 测试里的落点函数照 PopupPlacement 的规则写了一份（库不能引用兄弟库）；真实组合在 compose/OverlayDesk 里测。

### 与旧实现的差异

- 阴影改为 Look 里的活值绑定，取代旧代码订阅 `Tokens.Changed` 重新赋值。
- 浮层默认最小宽度来自 Look（旧代码写死 180）。
- `OverlayLayer` 内部保留的焦点与定位 Post（第 103/110/137/316/318 行）：浮层/对话框挂载后等待首帧排版及关闭后归还焦点（布局时序排队，非焦点竞态）。
