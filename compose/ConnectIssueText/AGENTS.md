# ConnectIssueText（组合）

旧 `Views/Dialogs.fs` 的 `describeIssue`（改名 `describe`）与 `retryLabel` 原样迁入：类型化连接错误的用户可见文案，以及重试按钮文案。协作者：ConnectionIssue（包）。要不要出重试按钮仍只看 `ConnectionIssue.retry`，不看文案。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **同一错误，文案与按钮文案与旧实现相同。** [根 AXIOM: 5]

# SPEC
1. [LEMMA: 1] 金样 `tests/golden/connectissuetext.txt` 由旧函数在 13 种错误上生成。

# 迁入或覆盖的旧测试
旧 DevicesUiTests 的下列用例要打开对话框点按钮，**旧用例不删**，这里只迁入其中不需要窗口的断言：
- `each typed connection error maps to its retry action regardless of wording`（部分覆盖：按钮文案表）
- `retry visibility does not depend on message keywords`（部分覆盖：文案不含旧关键词）
- `frozen pairing shows the minutes and offers no retry`（部分覆盖：文案含分钟数）

本组合新增：金样。

# 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。
