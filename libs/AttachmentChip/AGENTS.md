# AttachmentChip

消息里的附件条（迁自旧 `Views/MessageCard.fs` 的 `attachmentRow`）：图标、文件名、大小与下载图标，点下发 `Invoked`；内容丢失时变灰、不可点，显示「内容已丢失」，提示与读屏名仍自描述。图片的提示是「查看或下载图片」，普通文件是「下载附件」。根规则见项目根目录 AGENTS.md。

## 外观

- `AttachmentChipLook`：`Ink`（`ChipInk` 底色、描边、三档文字）、`Gauge`（`ChipGauge` 描边、圆角、内外边距、行间距、两档字号、文件名最大宽）、`Glyph`（图片 / 文件 / 下载图标）、`Dress`（可点时挂抬起面的状态反馈，旧 `Interaction.surface raised`）。
- 数据 `ChipData`：文件名、已格式化的大小、是否图片、是否丢失（由使用方用 MessageView 的 AttachmentRef 规则算好）。

## 证据与旧测试

覆盖旧用例：`DeliveryPolishingTests.message card renders user and assistant with reasoning and tools without error`（附件部分）。本库在 headless 下验证：可点与提示、普通文件提示、丢失态不可点且自描述、字号实时生效与退订。整卡控件树金样在组合者 MessageCardView。

## 与旧实现的差异

点下不再直接调宿主的 `downloadAttachment`，改发 `Invoked`，由组合者接到宿主动作。
