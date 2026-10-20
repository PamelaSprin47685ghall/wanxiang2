# CardReconcile

合并包（原 CardReconcile 包，手搓 DOM 对账器 Reconciler 已按 Clean-break 架构铁律退役删除，仅保留其余纯函数/纯状态工具模块）：CountdownTicker、DurationText、GenerationErrorTitle、MessageMenu、MessageTimestamp、ToolArgsPreview、ToolCallState、ToolErrorSummary、ToolOverview。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。手搓光标呼吸 CaretBreath 已退役删除，改由 Avalonia 声明式动效类 anim-breathe 原生驱动。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `CountdownTicker`

## CountdownTicker（库）

旧 `Views/MessageCard.fs` 限流倒计时（1109-1150）的刷新记账迁入：`Refresh(left)` 返回 `CountdownFrame`（剩余秒数、是否走完、是否该播报、计时器是否继续），`Left` 记最近一次剩余秒数，`IntervalMs` 为 1000。剩余秒数由宿主用 `RetryCountdown.remainingSeconds` 按截止时刻算并以事件驱动；无内部 DispatcherTimer。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **走完只播报一次，走完就停表。** [根 AXIOM: 5] 读屏用户只在可以重试那一刻听到一次，之后反复刷新不再打扰。

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/countdownticker.txt`；`tests/Tests.fs` 行为用例。

## 迁入或覆盖的旧测试
旧 `A11yRegressionTests.限流倒计时读屏名带剩余时间、走完才播报一次`、`ModelChipUiTests.rate limited error card shows a live countdown instead of a static hint`、`JourneyR9Tests.262 限流倒计时未结束时重试按钮不可按并显示剩余秒数` 要起窗口查控件，留在旧仓库；其中「只播报一次、走完停表」的规则由本库覆盖。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/OldCountdownTicker.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样证据较弱；规则另由 `tests/Tests.fs` 直接断言。

## 成员 `DurationText`

## DurationText（库）

旧 `Views/MessageCard.fs` 的 `formatDuration` 原样迁入（改名 `format`）：思考与工具耗时角标。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一毫秒数，文案与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/durationtext.txt` 由旧函数生成。

## 迁入或覆盖的旧测试
旧仓库没有直接测它的用例。本库新增：金样、零不显示、秒保留一位小数。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

## 成员 `GenerationErrorTitle`

## GenerationErrorTitle（库）

旧 `Views/MessageCard.fs` 的 `errorTitle` 迁入（改名 `ofMessage`，参数由 GenerationError 改为它的 message 字符串）：原始异常文本降级为固定标题「生成失败」。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一 message，标题与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/generationerrortitle.txt` 由旧函数（外包一个只有 message 的记录）生成。

## 迁入或覆盖的旧测试
旧仓库没有直接测它的用例。本库新增：金样、异常文本降级、人话去首尾空白。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

## 成员 `MessageMenu`

## MessageMenu（库）

旧 `Views/MessageCard.fs` messageMenuEntries 的门控迁入：`entries` 给出菜单项（`MessageMenuItem` 加了 RequireQualifiedAccess），`label`、`hint`、`isDanger` 给文案。执行体、图标、ContextMenu 仍归 MessageCard。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组状态，菜单项、次序、文案与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/messagemenu.txt`（96 种组合）。
2. 生成中整个菜单为空；复制要有非空文本；编辑并分叉只给用户消息；重新生成只给最新回复；复制链接要有提交号和链接出口；删除只要有提交号。

## 迁入或覆盖的旧测试
旧右键菜单用例要起窗口，留在旧仓库。本库新增：金样与 3 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。

## 成员 `MessageTimestamp`

## MessageTimestamp（库）

旧 `Views/MessageCard.fs` 的 `formatTimestamp` 迁入（改名 `format`）：今天只给时刻，昨天加前缀，今年给月日，更早给年月日。旧函数自己读 `DateTimeOffset.Now` 并 `ToLocalTime()`；新函数由调用方传入本地的 now 与本地化后的时刻，调用方写 `MessageTimestamp.format DateTimeOffset.Now (at.ToLocalTime())`，结果与旧实现相同。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一 now 与时刻，文案与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/messagetimestamp.txt` 由旧函数（把 Now 换成参数）在不变文化下生成。格式串沿用当前文化（`:` 是文化的时间分隔符），与旧实现一致。

## 迁入或覆盖的旧测试
旧 OperabilityTests 的 `footer timestamp carries the full timestamp for hover and screen readers` 要渲染消息卡，留在旧仓库。本库新增：金样、四档文案。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

## 成员 `ToolArgsPreview`

## ToolArgsPreview（库）

旧 `Views/MessageCard.fs` 的 `prettyFormatJson`（改名 `prettyJson`）与 `formatToolArgsPreview`（改名 `preview`）原样迁入：工具卡上的参数一行摘要与展开后的缩进 JSON。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一段参数，摘要（键数与预览）和缩进结果与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/toolargspreview.txt` 由旧函数在 16 段参数上生成。

## 迁入或覆盖的旧测试
旧仓库没有直接测这两个私有函数的用例（MessageCardTests 经渲染间接覆盖，留在旧仓库）。本库新增：金样、空对象、96 字截断、非 JSON 原样。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

## 成员 `ToolCallState`

## ToolCallState（库）

旧 `Views/MessageCard.fs` 的 `resolveToolCallStatusWith` 迁入：`ToolCallState.resolve`。状态联合类型用例名是泛名，加了 RequireQualifiedAccess。从工具结果里认出错误由调用方用 ToolErrorSummary 做，结果以 `string option option` 传入。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组输入，状态与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/toolcallstate.txt`（36 种组合）。

## 迁入或覆盖的旧测试
旧 MessageCard 的 `resolveStatus` 用例要构造 MessageContext，留在旧仓库。本库新增：金样与 2 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。

## 成员 `ToolErrorSummary`

## ToolErrorSummary（库）

旧 `Views/MessageCard.fs` 的 `tryExtractToolError` 原样迁入：从工具结果文本里认出失败并给出一句摘要（`error:` 前缀、JSON 的 error/isError/success/status、Python traceback、shell 失败、HTTP 401/403/404、500、fatal、权限不足、命令不存在）。工具卡状态与角标仍归 MessageModel。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一段结果文本，认定与摘要和旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/toolerrorsummary.txt` 由旧函数在 38 段结果上生成。
2. 已知旧行为，原样保留：非首行的 HTTP 状态行只认整行精确匹配（`404 Not Found extra` 不算失败）；`{"error":5}` 摘要为 `5`。

## 迁入或覆盖的旧测试
旧 ToolCallStatusTests 的两个 Theory 只迁入错误摘要部分（改名为 `no error pattern yields no summary`、`error patterns yield their summary`，数据行原样）。原用例同时断言 ToolCallStatus 的状态与角标（MessageModel），**旧用例不删**：
- `resolveToolCallStatus returns Success when result has no error pattern`（部分覆盖）
- `resolveToolCallStatus returns Failed when result matches error patterns`（部分覆盖）
- `resolveToolCallStatus extracts last line from python traceback as failure summary`（部分覆盖）

本库新增：金样、无结果、JSON 嵌套 message、traceback 末行。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（旧代码）重写金样；输出应与提交的金样逐字节相同。

## 成员 `ToolOverview`

## ToolOverview（库）

旧 `Views/MessageCard.fs` toolActivityBlock 的概览文案（`text`）、颜色档（`tone`，`OverviewTone`）与图标（`icon`，`OverviewIcon`）迁入，两个联合类型都加了 RequireQualifiedAccess。输入是各状态的计数，由调用方从 ToolCallState 统计。控件仍归 MessageCard。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组计数，结果与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 金样 `tests/golden/tooloverview.txt`（48 种组合）。
2. 等待批准优先于执行中；有待定的就绝不显示完成。

## 迁入或覆盖的旧测试
旧工具概览用例（第 206 节）要起窗口，留在旧仓库。本库新增：金样与 2 条规则用例。

## 金样生成
`dotnet fsi tools/gen-golden.fsx` 用 `tools/Old*.fs`（照抄旧分支）重写金样；输出应与提交的金样逐字节相同。金样只证明照抄的分支与新实现一致，证据较弱；规则另由 `tests/Tests.fs` 的「真实行为」测试直接断言。
