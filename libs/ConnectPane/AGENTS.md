# ConnectPane

连接对话框的内容（迁自旧 `Views/Dialogs.fs` 的 connectWithClosed）：地址 + 令牌，或切到配对码。失败归类（ConnectionIssue）、重试种类、自动提交时机（PairingAutoSubmit）归组合者。根规则见项目根目录 AGENTS.md。

- `ConnectPane(look, defaultUrl, rules)`：`CodeRules` 给归一、字距、配对说明三个函数。
  - 成员：`View`、`UrlBox`、`TokenBox`、`CodeBox`、`ConnectButton`、`CodeButton`、`PairingToggle`、`RetryButton`、`InitialFocus`（地址空则地址，否则令牌）、`StatusText`、`IsPairing`、`Code`。
  - 动作：`Announce`（中性，无重试）、`Fail(message, retryLabel, refocusCode)`、`StartPairing`、`Connect`、`SubmitCode`、`ClearToken`。
  - 事件：`ConnectRequested ConnectAsk`、`CodeSubmitted`、`CodeIncomplete`、`CodeChanged`（归一后）、`RetryClicked`、`Cancelled`。
- 配对模式下令牌框与页脚（来自 UiKit 的 `Kit.DialogFooter`）「连接」退场，「提交配对码」是唯一主键；配对码框数字键盘 + Done、最多 6 位。

## 外观

- `ConnectPaneLook`：公共字段加 `SpaceXXs`、`Hairline`。配对区 `grouping-card`。

## 证据与旧测试

- DialogsTests：`connect dialog validates pairing code and token connections`、`pairing code box normalizes pasted text and auto-submits at six digits`（归一部分；自动提交在组合者）
- DismissFocusTests：`connect focuses the first field that still needs input`
- 新增：类型化重试键与中性提示
