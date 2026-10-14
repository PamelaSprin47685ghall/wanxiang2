# AttachmentTray

输入区附件托盘（迁自旧 `Views/Composer.fs` 的 `attachmentStrip` / `attachmentScroller` / `RenderAttachments` / `SnapAttachmentScroller`）：每个附件一张可删除小卡（图标或缩略图、名称、状态字、可选「已缩小」）；移除后焦点落到下一张；窄屏横向滚。状态字与读屏名由宿主经 `ComposerSendState` 折好传入（`TrayItem`）。移除按钮是本库 Spot，带读屏 Invoke。根规则见项目根目录 AGENTS.md。

## 外观

- `AttachmentTrayLook`：`Ink`、`Gauge`、`BodyFont`、`Glyph`、`DressChip`、`DressRemove`、`Reduced`。

## 证据与旧测试

覆盖旧 `ComposerAttachmentTests` 类用例中移除后焦点归宿、失败态与缩小提示的呈现。本库无头行为用例覆盖读屏 Invoke、焦点迁移、显隐。
