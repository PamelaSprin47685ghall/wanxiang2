# ToolCallCard

工具调用卡（迁自旧 `Views/MessageCard.fs` 的 `toolCallCard`）：头部是唯一展开入口，显示状态图标、显示名、状态药丸、人话摘要（AutomationId `tool-human-summary`）、一行参数摘要与失败横幅；展开后参数摘要让位，限高视口里列出「调用参数」「执行结果 / 执行结果 (异常)」各带复制，状态未知时补一节「未收到工具执行结果（生成已结束）」。根规则见项目根目录 AGENTS.md。

## 外观与件

- `ToolCallCardLook`：`Ink`（`ToolInk` 各色）、`Gauge`（`ToolGauge` 间距、圆角、字号、箭头槽、箭头转动毫秒）、`BodyFont` / `MonoFont`、`Glyph`（扳手、警示、信息、对勾、箭头、两档转圈）、`Dress`（头部表面状态反馈）、`TechLineHeight`、`Reduced`。
- `ToolCardParts`：使用方给复制按钮（`Copy tip name getText`）与限高视口（`Reveal content visible`），本库不引用别的库。
- 数据 `ToolCardData`：文案都由使用方用 ToolCallState、ToolSummary、ToolArgsPreview、MessageView 算好；`ToolCallCard.sections`、`ToolCallCard.rawPayload` 给出旧的取舍与整段复制格式。

## 证据与旧测试

覆盖旧用例：`CraftsmanshipPolishTests.MessageCard toolCallCard header toggles argument and result details`、`VisualPassR3Tests.等审批的工具调用显示「等待批准」而不是「执行中」`。本库在 headless 下验证：等审批的配色与图标、开合与读屏名、失败横幅与异常节、未知状态补节与整段复制格式、复制按钮文案。整卡控件树金样在组合者 MessageCardView。

## 与旧实现的差异

Avalonia 12 没有 `SetExpanded`；旧代码对头部连设两次 HelpText，以后一次为准（「展开查看参数与结果 / 收起参数与结果」），本库只设这一次。
