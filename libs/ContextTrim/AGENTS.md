# ContextTrim

合并包：ContextTrim、UsageTally。生产消费者集合、外部依赖与 Wanxiang2 依赖按 MERGE.md 判据一致，合并后消费者不会多拿到它本来不用的 API。

测试工程 `<包名>.Tests.fsproj` 合并各成员用例（`<成员名>.<文件>.fs`），golden 目录按成员分目录；控件库的测试工程用 `AssemblySettings.fs` 关掉并行（Avalonia 平台状态是进程级全局量）。

## 成员 `ContextTrim`

## ContextTrim

按条数与 token 粗估预算裁剪上下文，保留最近的消息，开头不留孤立工具结果（旧 GenerationContext.trim）。只依赖 Microsoft.Extensions.AI。根规则见项目根目录 AGENTS.md。

## LEMMA
1. **行为与旧实现一致。** [根 AXIOM: 5]
    - 推理：模型看到的上下文与用户看到的标题是外部可见行为。

## SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过；端到端由 ChatOrchestrator 金样与迁入的旧测试钉住。

## 迁入或覆盖的旧测试
- 旧测试迁在 compose/ChatOrchestrator/tests（逐条列在 ChatOrchestrator AGENTS.md）。

## 成员 `UsageTally`

## UsageTally

一次生成里逐轮累加的 token 用量（从旧 ChatOrchestrator 的 addUsage / usageFromDetails 拆出）。依赖 Microsoft.Extensions.AI.Abstractions。根规则见项目根目录 AGENTS.md。

公开面：`UsageTally()`、`Add(UsageDetails)`、`PromptTokens`、`CompletionTokens`、`CachedTokens`、`TotalTokens`。

## LEMMA
1. **行为与旧实现一致：某项有一轮报告过就有值，从没报告过保持 None。** [根 AXIOM: 5]
2. **对象自己拥有累加状态；耗时、何时计入、怎么拼成线上 GenerationUsage 归组合者 ChatOrchestrator。** [根 AXIOM: 3]

## SPEC
1. [LEMMA: 1, 2] tests/Tests.fs 通过（空、多轮、缺项、0 与 None 的区别）。

## 迁入或覆盖的旧测试
- 经 compose/ChatOrchestrator 的金样（逐字节，含工具循环多轮 usage）与迁入的 ChatOrchestratorTests 覆盖。
