# UsageFoot

消息脚注里的时间与用量（迁自旧 `Views/MessageCard.fs` 的 `renderUsage` 与 `footer`）：「入 / 出 / 总 / tokens / 耗时 / 缓存」一行，标签用最淡档、数字用等宽中档，悬停提亮数字；时间旁悬停给完整时间戳，读屏取同一份完整文本。根规则见项目根目录 AGENTS.md。

## 外观与数据

- `UsageFootLook`：`Ink`（`FootInk` 三档文字）、`Gauge`（`FootGauge` 字号、间距）、`MonoFont`。
- `UsageFigures`：字段对应旧 GenerationUsage（缓存数由 `GenerationUsage.cachedCount` 算好，明细由 `formatDetail` 算好）；`StampText`：简短时刻（旧 `MessageTimestamp.format`）与完整时间。时刻由组合者用注入的时钟算，本库不读系统时间。

## 证据与旧测试

覆盖旧用例：`UiStabilityTests.streaming completion keeps the message footnote row geometry constant`（脚注部分）。本库在 headless 下验证：合计规则、只有合计与耗时、空用量、读屏名、悬停只提亮数字、时间与用量合一行与对齐。
