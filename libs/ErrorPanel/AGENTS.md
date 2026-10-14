# ErrorPanel

生成失败卡（迁自旧 `Views/MessageCard.fs` 的 `errorCard` / `errorCardWith`）：危险图标 + 标题 + 说明，可选倒计时，动作行依次是「继续生成」（有半截回复时）、「重试」（有继续时降为幽灵档）、恢复动作、「技术细节」披露（限高视口 + 复制）。根规则见项目根目录 AGENTS.md。

## 外观与件

- `ErrorPanelLook`：`Ink`（`ErrorInk`）、`Gauge`（`ErrorGauge` 间距、圆角、字号、按钮内边距与重试最小宽、箭头、阅读宽、倒计时间隔毫秒）、`BodyFont` / `MonoFont`、`Glyph`（警示、箭头）、`Dress secondary`（次要 / 幽灵按钮样式类）、`DressFold`（披露头状态反馈）、`TechLineHeight`、`HelperLineHeight`、`Reduced`。
- `ErrorPanelParts`：复制按钮、限高视口、倒计时文案（旧 `ProviderRateLimit.countdownText`）。
- 倒计时：`SetSeconds left announce` 推送剩余秒数（>0 时重试不可按、按钮写「重试（N 秒）」、读屏名带剩余时间；=0 时显示「现在可以重试了」并在 announce 时以 polite 播报一次）。`SetTicking true` 后挂在树上才按间隔发 `Tick`，组合者据此用 CountdownTicker 与宿主的 RetryCountdown 算下一帧。

## 证据与旧测试

覆盖旧用例：`JourneyR9Tests.262 限流倒计时未结束时重试按钮不可按并显示剩余秒数`、`JourneyR9Tests.262 没有倒计时的可重试错误照常可按`、`A11yRegressionTests.限流倒计时读屏名带剩余时间、走完才播报一次`、`ContinueLinkUndoTests.230 错误卡有半截回复时先给「继续生成」，没有时不给`（按钮排序部分）、`DeliveryPolishingTests.error card toggles technical detail and triggers retry action`。本库在 headless 下验证上述行为与计时器只在挂上且要求时运行。

## 与旧实现的差异

旧代码在卡里直接读 `RetryCountdown` 与模块级 `recoverySink`；现在倒计时由 `SetSeconds` 推、恢复动作由 `AddRecovery label action` 加，是否出某个按钮由组合者按 `GenerationError.retryable`、`ErrorRecovery.forKind` 决定。Avalonia 12 没有 `SetExpanded / SetRole`，披露头改报 ItemStatus。
