# FoldHeader

折叠披露头（迁自旧 `Views/MessageCard.fs` 里思考过程、工具活动、错误技术细节三处共用的头部）：右指箭头展开时转到 90°，键盘 Enter/Space 与点击同效；标题由使用方往 `Caption` 里加 Run，开合后发 `Toggled`。根规则见项目根目录 AGENTS.md。

## 外观

- `FoldHeaderLook`：`Muted`（箭头与标题的笔刷实例）、`Gauge`（`FoldGauge` 各档：箭头槽、行间距、内边距、圆角、描边、标题字号与行高、箭头转动毫秒，都是活值）、`Chevron`（按笔刷给箭头图形）、`Dress`（挂表面状态反馈样式类，旧 `Interaction.surface bordered`）、`Reduced`（减弱动效时箭头瞬转）。

## 证据与旧测试

覆盖旧用例：`PresentationRefreshTests.disclosure headers share one bordered transition`、`PresentationRefreshTests.disclosure chevron and copy confirmation ride the shared easeOutCubic`（箭头部分）。本库在 headless 下验证：开合转角与通知、前置/尾随件的位置、键盘开合、度量实时生效与退订。

## 与旧实现的差异

旧代码的 `SetRole / SetExpanded` 是旧 Primitives 里的扩展：角色写控件类型覆盖（按钮），展开状态写 ItemStatus「已展开 / 已折叠」；本库照做。旧工具活动头不报展开状态，用 `ReportState <- false` 关掉。读屏名与提示由使用方在 `Toggled` 里按旧文案设置。
