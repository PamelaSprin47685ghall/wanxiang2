# ChatEmpty

对话区空态（迁自旧 `Views/ChatView.fs` 的 `emptyPanel` / `ShowEmpty` / `ShowEmbeddedEmpty` / `HideEmpty`）：对话起点提示、品牌空态卡、起手式与预设芯片、或内嵌内容。文案表与 `ChatEmptyState` 仍归 `ChatEmptyText`；本库只接收标题、提示与动作。主按钮与芯片是本库自带的 Spot，带读屏 Invoke。根规则见项目根目录 AGENTS.md。

## 外观

- `ChatEmptyLook`：`Ink`、`Gauge`（字号、间距、按钮几何、字距、行高）、`BodyFont`、`DressPrimary` / `DressSecondary` / `DressCard`（样式类），都是活值或主题样式。

## 证据与旧测试

覆盖旧空态渲染分支（对话起点 / 品牌卡 / 起手式与预设 / 内嵌 / 起步贴底）。本库无头行为用例覆盖显隐、重复显示不重建、主按钮与芯片的读屏 Invoke、无起手式回调时不画行。
