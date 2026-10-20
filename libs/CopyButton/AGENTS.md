# CopyButton

复制按钮的确认反馈（迁自旧 `Controls/CopyFeedback.fs`）。单入口幂等 `Render(CopyButtonVS)`，确认记账由 VS 状态驱动（update 记 expiresAt），移除内置 DispatcherTimer。图标、提示文字、读屏名支持按 `StateChanged` 或 VS 状态响应。根规则见项目根目录 AGENTS.md。

## 外观

- `CopyButtonAppearance`（W7 外观退役后的普通记录）：`FadeOpacity`（度量常量 `Metric.OpacityCopyConfirmFade` 的当前值）、`Hold`、`Fade`（MotionLedger 时长档的当前值）。支持 Render(CopyButtonVS)。
- 用到的样式类：无。

## 证据与旧测试

旧仓库没有直接测 CopyFeedback 的用例（MessageCard 复制用例经整张卡片）。本库在 headless 下用可控时钟验证：淡出→保持→复位、减弱动效、重新起算、离开可视树复位、淡出到期回到确认态。

## 与旧实现的差异

`CopyState` 取代 `CopyVisual`（加了 RequireQualifiedAccess），使用方改按 `StateChanged` 订阅。
