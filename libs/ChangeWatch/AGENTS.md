# ChangeWatch

合并包：ChangeWatch、DeferredReveal、ReconnectLoop、StartupTrace。（注：ConversationSurface/IdleSurface/StartReady 碎布尔逻辑已退役并由 WorkspaceState DU 状态树取代；MessageLink/DraftSummaryText/ForkAnchor/OlderPageLoad/RevealTarget/ReconnectBackoff 纯函数已移入 libs/WorkspaceState 作为内部模块）。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ChangeWatch`

## ChangeWatch

轮询值的变化检测（迁自旧 `AppShell.fs` `WatchHostViewport` 的 `hostViewportWidth`、`lastResumeEpoch`）。轮询计时、`css > 0` 的过滤与后续动作仍归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **与上次不同才算变化**，并记下新值。初值可给（恢复代号从 0 起）或不给（视口宽度起初未知）。

## SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

## 迁入或覆盖的旧测试
旧仓库没有直接测这段的用例。本库新增 2 条行为用例。

## 成员 `ConversationSurface`

## ConversationSurface（库）

旧 `AppShell.fs` 刷新时「已打开对话」分支（空对话卡、末尾错误卡来源、生成中文案、输入框可用、能否停止）与 UpdateHostStatusLine 的文案迁入。末尾错误来源 `TailError` 加了 RequireQualifiedAccess；ReplyTail.isUnanswered 由调用方算好传入。控件调用仍归 AppShell。没打开对话的分支在 IdleSurface。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组状态，决定与旧分支相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 旧仓库没有直接测这段的用例；本包新增 3 条规则用例（见「迁入或覆盖的旧测试」）。
2. 运行错误优先于未回复；未回复只在不生成、不流式、没有半截回复时才显示。

## 迁入或覆盖的旧测试
旧对话区用例要起窗口，留在旧仓库。本包新增 3 条规则用例。

## 成员 `DeferredReveal`

## DeferredReveal

延迟显示的武装状态（迁自旧 `AppShell.fs` 的 `ShowConversationLoadingDeferred`、`DisarmSkeletonLoading`，字段 `skeletonArmedFor`）。计时器、骨架与标题仍归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一个键只武装一次。** [根 AXIOM: 5] 已为同一键武装时，再次武装不重启计时（D1：后台渲染不能把骨架无限顺延）；到点不解除武装，与旧实现相同。
2. **过期的计时不做事。** 解除或换键之后，旧键到点返回 false。

## SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 通过。延迟 300ms 与旧 `MotionLedger.conversationSkeletonDelay` 相同。

## 迁入或覆盖的旧测试
旧仓库没有直接测这段的用例（骨架相关用例测的是 Tokens 与 ChatView 控件）。本库新增 3 条行为用例。

## 成员 `DraftSummaryText`

## DraftSummaryText（库）

旧 `AppShell.fs` 的 `UpdateDraftSummary` 里的文案分支迁入：`DraftSummaryText.text`。数草稿与未确认消息仍归 AppShell（用 DraftStore 快照）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组计数，文案与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 旧仓库没有直接测它的用例；本包新增 2 条规则用例：文案分支与关闭保存优先。

## 迁入或覆盖的旧测试
旧仓库没有直接测它的用例。本包新增：文案分支、关闭保存优先。

## 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。

## 成员 `ForkAnchor`

## ForkAnchor

「编辑并分叉」的客户端事实：分叉点（forkAfterId）怎么选、被编辑的用户消息怎样拼成上线 JSON，以及没有可分叉对话时的提示。迁自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs `ForkFrom`。发 ForkConversation、选中新对话、聚焦输入框留在宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **分叉点与上线字节与旧代码一致。** [根 AXIOM: 1]

## SPEC
1. [LEMMA: 1] `tests/Tests.fs` 覆盖分叉点选择与上线 JSON：新分叉点与已编辑的用户消息拼成的上线字节一致。
2. 没有可分叉对话时给出提示。

## 迁入或覆盖的旧测试
- 旧仓库没有直接覆盖 ForkFrom 私有逻辑的单元测试；新增分叉点与上线字节两条行为测试。

## 成员 `IdleSurface`

## IdleSurface（库）

旧 `AppShell.fs` 刷新界面时「没有打开对话」分支的决定迁入：`IdleSurface.decide` 给出空态（`IdleEmpty`，泛名，加了 RequireQualifiedAccess）、空态主按钮文案、输入框是否可用及说明、模型标签；另有加载中与已打开对话的输入框说明。控件调用、目录查询（模型描述、看图能力）仍归 AppShell。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组状态，决定与旧分支相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 旧仓库的空态与输入框用例都要起窗口，留在旧仓库；本包新增 4 条规则用例。

## 迁入或覆盖的旧测试
旧仓库的空态与输入框用例都要起窗口，留在旧仓库。本包新增：连接失败入口、首次与重连、首次运行卡片、可输入。

## 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。

## 成员 `MessageLink`

## MessageLink

单条消息链接（`wanxiang://open?c=…&m=…` 与 PWA `#open?…`）的生成与识别，原样迁自旧 `Model/MessageLink.fs`。只依赖 BCL。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **链接文本不变。** [根 AXIOM: 1] 用户已复制、分享出去的链接必须还能打开。

## SPEC
1. [LEMMA: 1] `tests/Tests.fs` 按字节钉住桌面写法，并覆盖拒绝情况。

## 迁入或覆盖的旧测试
- ContinueLinkUndoTests：`231 消息链接两种写法往返，普通搜索词不误认`

## 成员 `OlderPageLoad`

## OlderPageLoad

「加载更早消息」请求的在途判断、超时判断与两条失败提示，迁自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs `CheckOlderPageTimeout` / `FailOlderPage` / `ShowOlderPageRetry`。超时时长（旧 MotionLedger.olderPageTimeout）由宿主传入；计时器、请求、失败条留在宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **超时与提示与旧代码一致。** [根 AXIOM: 1]

## SPEC
1. [LEMMA: 1] 测试内的旧代码原样摘录与新实现在 loading × requestedAt × 时刻上逐例一致。

## 迁入或覆盖的旧测试
- 旧仓库没有直接覆盖这些私有成员的单元测试；新增上述差分测试与在途判断测试。

## 成员 `ReconnectBackoff`

## ReconnectBackoff

断线重连的等待时长：从 1 秒起翻倍，封顶 30 秒（从旧 `Client/ReconnectBackoff.fs` 原样搬来）。只依赖 BCL。根规则见项目根目录 AGENTS.md。

公开面：`ReconnectBackoff.baseDelayMs`、`maxDelayMs`、`next`、`schedule`。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
2. **重置时机归调用方（连接成功、用户手动连接），本库只算下一次等多久。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1] 下列迁入的旧测试与本库补充的边界测试通过。

## 迁入或覆盖的旧测试
- 旧 ReconnectBackoffTests：`等待时长逐次翻倍`、`等待时长封顶在三十秒`、`连续失败时真的退避而不是原地打转`、`异常输入不会退出合法区间`。
- 旧 ClientStateAndWsClientTests：`ReconnectBackoff base delay is 1000ms and max delay is 30000ms`、`ReconnectBackoff next doubles delay under cap`、`ReconnectBackoff next clamps at maxDelayMs`、`ReconnectBackoff next handles smaller than baseDelayMs values safely`、`ReconnectBackoff schedule produces correct sequence for failure counts`、`ReconnectBackoff schedule and state machine correctly clamps at max delay and resets`。
- 补充：`失败次数为负是调用方错误，直接抛出`、`长时间宕机时一直停在上限`。

## 成员 `ReconnectLoop`

## ReconnectLoop（库）

旧 `AppShell.fs` 连接与重连流程里能独立成立的一块记账迁入，成为一个对象：
- 连接尝试编号：`Connect`、`IsCurrent`，用来丢掉过期的异步回调；
- 至多一个挂起的重连：`Schedule` 返回票据和等待时长，`Fire` 在到点时校验票据；
- 网络恢复立刻重连：`ResumeNow`；
- 退避归零：`ResetDelay`，用于手动连接和连接成功。

退避规则由构造参数给（通常是 ReconnectBackoff.baseDelayMs 与 next）。计时、真正的连接、认证、断线时的清理与界面文案仍归 AppShell。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一串操作，编号、挂起、等待时长与旧记账相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] `tests/ReconnectLoop.Tests.fs` 通过：`backoff grows across failed reconnects and only resets on success or manual`、`at most one pending reconnect, none without a token`、`a new connect voids the pending timer and stale callbacks`、`resume runs the pending reconnect now and resets backoff`。
2. 主机一直不在时等待时长逐次翻倍到上限，只在连接成功或用户手动连接时归零；没有令牌不排重连；新的连接让挂起的计时作废；ResumeNow 只在有挂起时才动作。

## 迁入或覆盖的旧测试
旧 ClientStateAndWsClientTests 的退避用例已在 ReconnectBackoff；AppShell 的重连用例要起窗口，留在旧仓库。本包新增 4 条规则用例。

## 成员 `RevealTarget`

## RevealTarget

打开对话后要滚动到的消息（迁自旧 `Model/ConversationSummary.fs`）。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **目标不在已加载窗口时返回 None，由调用方决定是否加载更多。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

## 迁入或覆盖的旧测试
- LibraryTests：`reveal targets resolve against the loaded window`

## 成员 `StartReady`

## StartReady（库）

旧 `AppShell.fs` 刷新界面时的 `startReady` 判断迁入：`StartReady.isReady`。当前对话状态用 `ActiveConversation`（泛名，加了 RequireQualifiedAccess）：没打开、加载中、已加载（附消息条数）。不引用任何其他库。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一组状态，结论与旧实现相同。** [根 AXIOM: 5]

## SPEC
1. [LEMMA: 1] 旧 ConversationWorkspaceTests 的用例要起窗口，留在旧仓库；本包新增 2 条规则用例。

## 迁入或覆盖的旧测试
旧 ConversationWorkspaceTests 的用例要起窗口，留在旧仓库。本包新增：新对话或空对话才就绪。

## 真实行为测试
金样来自照抄的旧分支，证据较弱；`tests/Tests.fs` 末节另按上面写明的规则直接断言新实现（不经金样），规则改了要同时改这里。

## 成员 `StartupTrace`

## StartupTrace

启动感知性能打点（决策 290）的纯部分：`StartupTrace.modeOf`（环境变量值 → 是否打点、是否采样后退出）、`StartupTrace.line`（stderr 行格式）、`StartupMilestones.Mark`（每个里程碑只记一次），迁自旧 `Model/StartupTrace.fs`。执行侧（2026-10-17 增补，迁自旧 `Model/StartupTrace.fs`）：读 `WANXIANG_STARTUP_TRACE`、进程启动时间、stderr 输出、`mark` 去重与 `requestExit` 回调都在本库的 `StartupTrace` 模块内（同名模块自然合并）；入口只安装 `requestExit`（桌面 = `Environment.Exit 0`）。`LocalHostGate` 仍是同目录独立 fsproj。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **行为与旧代码一致。** [根 AXIOM: 1] 模式、去重结果与输出行格式与旧程序集相同。

## SPEC
1. [LEMMA: 1] `tests/StartupTrace.Tests.fs` 通过：`290 未设置环境变量时不打点；值为 exit 时打点并退出`、`290 未启用时 mark 不记录；requestExit 可替换`。

## 迁入或覆盖的旧测试
- 旧仓库没有 StartupTrace 的单元测试（只有仓库根 `tools/perf_desktop.sh` 采样）；本包新增用例 `290 未设置环境变量时不打点；值为 exit 时打点并退出` 与 `290 未启用时 mark 不记录；requestExit 可替换`。
