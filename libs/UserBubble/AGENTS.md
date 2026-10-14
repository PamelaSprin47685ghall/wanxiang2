# UserBubble

用户消息（迁自旧 `Views/MessageCard.fs` 的 `renderWithStream` 用户分支）：右对齐的深色气泡（细描边、右下角收小、可聚焦、读屏名「用户消息」）+「你」头像；气泡内容由组装者放进来。`UserBubble.text` 给气泡正文。根规则见项目根目录 AGENTS.md。

## 外观

- `UserBubbleLook`：`Ink`（`BubbleInk` 气泡底、描边、文字、选区、头像底与字）、`Gauge`（`BubbleGauge` 描边、三档圆角、内边距、最大宽、头像边长与字号、间距）、`Dress`（可聚焦表面的焦点环，只走外阴影不改几何）、`LineHeight`（正文行高随字号）。

## 证据与旧测试

覆盖旧用例：`A11yRegressionTests.强调色悬停态与用户气泡上的文字过 AA`（配色由主题给，本库只取实例）、`DeliveryPolishingTests.message card renders user and assistant with reasoning and tools without error`（用户部分）。本库在 headless 下验证气泡几何、读屏名、头像位置、正文配色与行高、最大宽实时生效。
