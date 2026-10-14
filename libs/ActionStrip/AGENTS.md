# ActionStrip

消息脚注里的操作条（迁自旧 `Views/MessageCard.fs` 的 `actionButtons`）：图标按钮随文档流排布，整行恒定占一个命中区高（流式期间撤掉按钮也不缩，完成时脚注行不跳）；显隐只改不透明度，不动几何。闲置档分「隐去」与「淡显」（最新一条助手回复用淡显作可发现锚点）。复制按钮的读屏名规则在 `ActionStrip.copyName`。根规则见项目根目录 AGENTS.md。

## 外观

- `ActionStripLook`：`Gauge`（`StripGauge` 间距、命中区边长、淡显不透明度、显隐补间毫秒）、`Glyph`（复制 / 对勾 / 编辑 / 重新生成 / 删除）、`Muted`（图标默认笔刷）、`Dress`（图标按钮样式类）、`Reduced`。

## 证据与旧测试

覆盖旧用例：`UiStabilityTests.streaming completion keeps the message footnote row geometry constant`（行恒定占位部分）。本库在 headless 下验证：空行占位、两档闲置与显示、方形命中区与读屏名与动作、换图标、命中区边长实时生效与退订。哪些按钮出现（流式不挂、用户消息才有编辑、最后一条助手才有重新生成、有 commitId 才能删）由组合者 MessageCardView 决定并测试。

## 与旧实现的差异

旧代码用模块级可变量记触屏选中的那一条；本库只提供 `SetShown`，选中与悬停的联动放在组合者。
