# ConversationRuns（组合者）

按对话保存的临时生成状态（ConversationRun、ConversationRuns：开始、流式、结束、半截回复、退休台账）与错误卡重试倒计时（RetryCountdown），迁自旧 `Model/UiControllers.fs`。协作者：MessageView（流式消息）、Wire（GenerationError、GenerationUsage、ProviderRateLimit）、ConversationSummary（运行态）。命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# SPEC
1. 旧用例原样迁入，经三个库的交付包编译运行（`tests/Tests.fs`）。

# 迁入或覆盖的旧测试
- UiControllersTests：`ConversationRuns initial state is empty and idle`、`ConversationRuns start transitions to generating and resets message`、`ConversationRuns delta updates streaming message`、`ConversationRuns finish without error archives entry to retired ledger with capacity limit`、`ConversationRuns finish with error records error in state`、`ConversationRuns disconnect clears in-flight run but preserves retired ledger`
- UiPureLogicTests：`ConversationRuns state machine and FIFO retirement capacity`
- ContinueLinkUndoTests：`230 半截回复编成去尾空白的助手消息`、`230 失败时保留有正文的半截回复，重试或重新开始即清掉`、`230 成功结束或空回复不留半截`（`230 继续生成只接受有正文的助手消息` 要 EventStore/CommandEngine，留在旧仓库）
- PartialPersistAndUrlSchemeTests：`250 刷新后客户端从主机恢复半截与错误卡；正在生成时不覆盖`
- ModelChipRateLimitTests：`retry countdown deadline is fixed at finish time and survives re-reads`、`non retryable errors never get a countdown`
- LibraryUndoTests：`§215 每类不可自愈的错误都有恢复动作`（`ErrorRecovery.forKind` 在本组合者，动作与文案在 ErrorRecovery 库）
- R19SimplifyTests：`366 限流只给「换个模型」并直达切换器`
- 另：`GenerationOutcome.errorOf` / `notice` 与 `FinishNotice`，迁自旧 AppShell HandleEvent 的 GenerationFinished 分支（D2 Fail-closed）；新增 `D2 无错误详情的失败与未知状态都落成可重试的通用失败`。

生成结束的旧分支金样复刻已退役（金样文件、旧代码副本与金样生成器连同渲染脚本一并删除）。改为直接断言真实行为：`tests/Tests.fs` 断言 `errorOf` 对每种结束状态（完成 / 取消 / 失败 / 未知）的判定与错误落成（含未知状态的 detail），断言 `notice` 在当前对话与非当前对话下的提示差异（卡片 / 一句 toast / 已停止），并跑两条真实生成序列（成功与失败）验证 `ConversationRuns` 的状态转移与副作用（用量记下、消息清空、半截回复保留、清错误时半截一并清掉）。
