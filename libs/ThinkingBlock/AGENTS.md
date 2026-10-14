# ThinkingBlock

思考过程卡（迁自旧 `Views/MessageCard.fs` 的 `reasoningBlock` 外框与正文）：分组卡 + 左缘竖线，头部（折叠头）与正文视口由组装者放进来；正文字号比阅读字号小 1.5，右键不出主题自带的 Copy 浮层。标题文案「正在思考 / 已深度思考（收起 / 展开）」由 `ThinkingBlock.title` 给。根规则见项目根目录 AGENTS.md。

## 外观

- `ThinkingBlockLook`：`Ink`（`ThinkingInk` 底色、描边、竖线、正文、选区）、`Gauge`（`ThinkingGauge` 描边、竖线宽、内外边距、圆角）、`LineHeight`（正文行高随字号，旧 `ReadingRhythm.secondaryLineHeight`）。

## 证据与旧测试

覆盖旧用例：`DeliveryPolishingTests.message card renders user and assistant with reasoning and tools without error`（思考部分）。本库在 headless 下验证外框结构、竖线宽实时生效与退订、正文字号与标题文案。整卡控件树金样在组合者 MessageCardView。
