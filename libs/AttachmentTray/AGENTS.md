# AttachmentTray

合并包：AttachmentTray、ComposerBox、ModelChip。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `AttachmentTray`

## AttachmentTray

输入区附件托盘（迁自旧 `Views/Composer.fs` 的 `attachmentStrip` / `attachmentScroller` / `RenderAttachments` / `SnapAttachmentScroller`）：每个附件一张可删除小卡（图标或缩略图、名称、状态字、可选「已缩小」）；移除后焦点落到下一张；窄屏横向滚。状态字与读屏名由宿主经 `ComposerSendState` 折好传入（`TrayItem`）。移除按钮是本库 Spot，带读屏 Invoke。根规则见项目根目录 AGENTS.md。

### 外观

- `AttachmentTrayLook`：`Ink`、`Gauge`、`BodyFont`、`Glyph`、`DressChip`、`DressRemove`、`Reduced`。

### 证据与旧测试

覆盖旧 `ComposerAttachmentTests` 类用例中移除后焦点归宿、失败态与缩小提示的呈现。本库无头行为用例覆盖读屏 Invoke、焦点迁移、显隐。

- 新芯片从 0 淡入（旧 controlRowFade，减弱动效瞬时）；圆角 RadiusSm，失败芯片 HairlineStrong 描边；滚动窗整行取高只在渲染后投递一次（同旧）。

## 成员 `ComposerBox`

## ComposerBox

输入壳（迁自旧 `Views/Composer.fs` 的输入框、shell 外观与焦点环、发送 / 停止 / 排队 / 附件 / 上下文键、拖入文件、禁用说明、看图提示、附属位、Enter 与 Ctrl+Enter 发送）。附件托盘与模型芯片由宿主放进插槽；发送门控由宿主用 `ComposerSendState.decide` 折成 `SendView` 写入；历史召回与输入法探针经钩子注入（旧 PromptHistory、ImeGuard）。各键是本库 Spot，带读屏 Invoke。根规则见项目根目录 AGENTS.md。

### 外观

- `ComposerBoxLook`：`Ink`、`Gauge`、`BodyFont`、`Glyph`、`DressSend` / `DressQueue` / `DressAttach` / `DressGhost`。输入壳的静止/禁用边界描边走 `BoxInk.ControlStroke`（≥3:1，见 `libs/ThemePalette` 与 `docs/ux/a11y-audit.md` §2），聚焦与拖入仍是 `Accent`。

### 证据与旧测试

覆盖旧 `ComposerTests` 类用例中发送 / 停止 / 排队切换、Enter 规则、输入法组合不提交、禁用说明与占位文字、看图受阻提示。本库无头行为用例覆盖读屏 Invoke、按键规则、禁用与看图提示。

- 键序：输入法组合 → 宿主钩子 → Esc 停止 → Enter 发送判定；Enter 发送档里 Ctrl+Enter 交给输入框换行。发送/排队键不读门控缓存，直接交宿主同步判定。窄屏收起对话上下文按钮（决策 364）。拖放挂在整个根上。

## 成员 `ModelChip`

## ModelChip

输入区模型芯片（迁自旧 `Views/Composer.fs` 的 `modelChip` / `SetModelLabel`）：显示当前模型名，点开切换；未选时用 muted 色。芯片是本库 Spot，带读屏 Invoke。根规则见项目根目录 AGENTS.md。

- 右侧下拉箭头经 `Chevron: IBrush -> Control`（旧 Icons.chevronDown，ChipInk.TextFaint）；无障碍名由组合者固定为「切换本对话使用的模型」。

### 外观

- `ModelChipLook`：`Ink`、`Gauge`（字号、最大宽、内边距、圆角、窄屏最小高）、`BodyFont`、`Chevron`、`Dress`。

### 证据与旧测试

覆盖旧 `SetModelLabel` 与点击打开选择器。本库无头行为用例覆盖标签换色与读屏 Invoke。
