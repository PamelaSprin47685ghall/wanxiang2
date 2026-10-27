# ChangeWatch

合并包：ChangeWatch、DeferredReveal、ReconnectLoop、StartupTrace。（注：ConversationSurface/IdleSurface/StartReady 碎布尔逻辑已退役并由 WorkspaceState DU 状态树取代；MessageLink/DraftSummaryText/ForkAnchor/OlderPageLoad/RevealTarget/ReconnectBackoff 纯函数已移入 libs/WorkspaceState 作为内部模块，其规格与测试登记于 `libs/WorkspaceState/AGENTS.md`）。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ChangeWatch`

## ChangeWatch

轮询值的变化检测（迁自旧 `AppShell.fs` `WatchHostViewport` 的 `hostViewportWidth`、`lastResumeEpoch`）。轮询计时、`css > 0` 的过滤与后续动作仍归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **与上次不同才算变化**，并记下新值。初值可给（恢复代号从 0 起）或不给（视口宽度起初未知）。

## SPEC
1. [LEMMA: 1] `tests/ChangeWatch.Tests.fs` 通过：`视口宽度：首次读到算变化，相同不算，再变才算`、`恢复代号：从初值 0 起，同值不触发重连`。

## 迁入或覆盖的旧测试
旧仓库没有直接测这段的用例。本库新增 2 条行为用例。

## 成员 `DeferredReveal`

## DeferredReveal

延迟显示的武装状态（迁自旧 `AppShell.fs` 的 `ShowConversationLoadingDeferred`、`DisarmSkeletonLoading`，字段 `skeletonArmedFor`）。计时器、骨架与标题仍归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **同一个键只武装一次。** [根 AXIOM: 5] 已为同一键武装时，再次武装不重启计时（D1：后台渲染不能把骨架无限顺延）；到点不解除武装，与旧实现相同。
2. **过期的计时不做事。** 解除或换键之后，旧键到点返回 false。

## SPEC
1. [LEMMA: 1, 2] `tests/DeferredReveal.Tests.fs` 通过：`同一对话只武装一次，反复渲染不顺延`、`到点后不再武装同一对话；解除后可以重新武装`、`快照先到或换了对话，旧计时到点不做事`。延迟 300ms 与旧 `MotionLedger.conversationSkeletonDelay` 相同。

## 迁入或覆盖的旧测试
旧仓库没有直接测这段的用例（骨架相关用例测的是 Tokens 与 ChatView 控件）。本库新增 3 条行为用例。

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
2. 主机一直不在时等待时长逐次翻倍到上限，只在连接成功或用户手动连接时归零（`backoff grows across failed reconnects and only resets on success or manual`）；没有令牌不排重连（`at most one pending reconnect, none without a token`）；新的连接让挂起的计时作废（`a new connect voids the pending timer and stale callbacks`）；ResumeNow 只在有挂起时才动作（`resume runs the pending reconnect now and resets backoff`）。

## 迁入或覆盖的旧测试
旧 ClientStateAndWsClientTests 的退避用例已在 `libs/WorkspaceState`；AppShell 的重连用例要起窗口，留在旧仓库。本包新增 4 条规则用例。

## 成员 `StartupTrace`

## StartupTrace

启动感知性能打点（决策 290）的纯部分：`StartupTrace.modeOf`（环境变量值 → 是否打点、是否采样后退出）、`StartupTrace.line`（stderr 行格式）、`StartupMilestones.Mark`（每个里程碑只记一次），迁自旧 `Model/StartupTrace.fs`。执行侧（2026-10-17 增补，迁自旧 `Model/StartupTrace.fs`）：读 `WANXIANG_STARTUP_TRACE`、进程启动时间、stderr 输出、`mark` 去重与 `requestExit` 回调都在本库的 `StartupTrace` 模块内（同名模块自然合并）；入口只安装 `requestExit`（桌面 = `Environment.Exit 0`）。`LocalHostGate` 仍是同目录独立 fsproj。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **行为与旧代码一致。** [根 AXIOM: 1] 模式、去重结果与输出行格式与旧程序集相同。

## SPEC
1. [LEMMA: 1] `tests/StartupTrace.Tests.fs` 通过：`290 未设置环境变量时不打点；值为 exit 时打点并退出`、`290 未启用时 mark 不记录；requestExit 可替换`。

## 迁入或覆盖的旧测试
- 旧仓库没有 StartupTrace 的单元测试（只有仓库根 `tools/perf_desktop.sh` 采样）；本包新增用例 `290 未设置环境变量时不打点；值为 exit 时打点并退出` 与 `290 未启用时 mark 不记录；requestExit 可替换`。
